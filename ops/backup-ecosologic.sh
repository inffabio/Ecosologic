#!/bin/sh
# ecosologic production backup
#
# Creates timestamped, validated backup *sets* of the PostgreSQL database and of
# the Docker volumes `media-data` and `api-data-protection-keys`. Intended to run
# on the production server as user `hannibal` from /home/hannibal/ecosologic
# (normally triggered by the companion systemd timer at 03:30).
#
# Layout: each run writes into a private staging directory and, only after every
# artifact validates, atomically renames it into a timestamped, complete set:
#
#   BACKUP_ROOT/
#     .backup.lock/                 (lock directory)
#     .staging.XXXXXX/              (in-progress run; never a valid set)
#     20260102-033000/              (complete set)
#       db.dump
#       media.tar.gz
#       keys.tar.gz
#
# The presence of a `YYYYMMDD-HHMMSS` directory containing all three artifacts is
# the completeness marker. Retention removes only complete, expired directories;
# it never removes individual artifacts or foreign files.
#
# Properties:
#   * POSIX sh, no bashisms.
#   * Never reads the `.env` file and never exposes the database password on a
#     command line or in logs: pg_dump runs inside the postgres container and
#     resolves PGPASSWORD from that container's own environment.
#   * The backup root is created with mode 700 and artifacts with mode 600.
#     Volume archives are streamed to stdout inside a throwaway `--network none`
#     container and redirected on the host, so the archive file is always
#     created by the invoking user (never root-owned). The keys volume is read
#     by a tar running as root (the volume is root-owned); the archive file
#     itself is still created by the host user and chmod'd 0600.
#   * A lock directory prevents concurrent runs; stale locks are detected.
#   * Every artifact is validated after creation (`pg_restore --list` for the
#     dump, `tar -tf` for the archives); any failure exits non-zero before the
#     set is published.
#   * Retention (default 14 days, validated as an integer in 1..365) removes
#     only complete, expired directories; its `find` failure aborts the run.
#   * BACKUP_ROOT and LOCK_FILE are canonicalized (`realpath`, symlinks
#     resolved) and validated to be strictly under /home/hannibal; `.`/`..`
#     components are rejected. Cleanup is limited to direct children of the
#     canonical BACKUP_ROOT and never follows symlinks.
#
# Configuration is via environment variables (see defaults below); the systemd
# unit sets none, so the defaults apply unless you add Environment= entries.

set -eu

umask 077

# --- Configuration (overridable through the environment) -------------------
PROJECT_DIR="${PROJECT_DIR:-/home/hannibal/ecosologic}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.server.yml}"

BACKUP_ROOT="${BACKUP_ROOT:-/home/hannibal/backups/ecosologic}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-ecosologic}"
POSTGRES_SERVICE="${POSTGRES_SERVICE:-postgres}"
PG_IMAGE="${PG_IMAGE:-postgres:16-alpine}"
TAR_IMAGE="${TAR_IMAGE:-alpine:3.20}"

# media-data has no explicit Docker name, so it is prefixed with the compose
# project name. api-data-protection-keys has an explicit name (see
# docker-compose.server.yml), so it is referenced by that fixed name.
MEDIA_VOLUME="${MEDIA_VOLUME:-${COMPOSE_PROJECT_NAME}_media-data}"
KEYS_VOLUME="${KEYS_VOLUME:-ecosologic_api_data_protection_keys}"

STAGING=""
retention_list=""
LOCK_FILE=""

# --- Helpers ---------------------------------------------------------------
log() {
    printf '%s\n' "[backup] $(date -u '+%Y-%m-%dT%H:%M:%SZ') $*"
}

die() {
    printf '%s\n' "[backup] ERROR: $*" >&2
    exit 1
}

# BACKUP_ROOT must be an absolute path, free of `.`/`..` components, that
# canonicalizes (with symlinks resolved) to a location strictly under
# /home/hannibal. Echoes the canonical path on success; dies otherwise.
validate_backup_root() {
    root="$1"
    case "$root" in
        /*) ;;
        *) die "BACKUP_ROOT must be an absolute path (got: $root)" ;;
    esac
    case "/$root/" in
        */../*) die "BACKUP_ROOT must not contain '..' components (got: $root)" ;;
    esac
    case "/$root/" in
        */./*) die "BACKUP_ROOT must not contain '.' components (got: $root)" ;;
    esac
    canonical=$(realpath -m -- "$root" 2>/dev/null) \
        || die "cannot canonicalize BACKUP_ROOT (realpath failed): $root"
    case "$canonical" in
        /home/hannibal/*) ;;
        *) die "BACKUP_ROOT must resolve under /home/hannibal (resolved: $canonical)" ;;
    esac
    [ "$canonical" != "/home/hannibal" ] || die "BACKUP_ROOT must not resolve to /home/hannibal itself"
    printf '%s\n' "$canonical"
}

# RETENTION_DAYS must be a plain integer between 1 and 365.
validate_retention() {
    case "$RETENTION_DAYS" in
        ''|*[!0-9]*) die "RETENTION_DAYS must be an integer (got: $RETENTION_DAYS)" ;;
    esac
    [ "$RETENTION_DAYS" -ge 1 ] && [ "$RETENTION_DAYS" -le 365 ] \
        || die "RETENTION_DAYS must be between 1 and 365 (got: $RETENTION_DAYS)"
}

# Canonicalize BACKUP_ROOT and derive LOCK_FILE from it. LOCK_FILE must be a
# direct child of the canonical root and never the root itself.
validate_config_paths() {
    BACKUP_ROOT=$(validate_backup_root "$BACKUP_ROOT")
    LOCK_FILE="${BACKUP_ROOT}/.backup.lock"
    case "$LOCK_FILE" in
        "$BACKUP_ROOT"/.*) ;;
        *) die "LOCK_FILE must be derived from BACKUP_ROOT (got: $LOCK_FILE)" ;;
    esac
    [ "$LOCK_FILE" != "$BACKUP_ROOT" ] || die "LOCK_FILE must not equal BACKUP_ROOT"
}

# Remove a path only if it is a direct child of the (canonical) BACKUP_ROOT,
# is not a symlink, and still resolves inside the root. Never removes `/`,
# BACKUP_ROOT itself, an empty/relative/nested path, or a path that resolves
# outside the root. This makes cleanup (including error/exit traps) safe even if
# a variable is unexpectedly empty or the tree was tampered with.
safe_rm_rf() {
    path="$1"
    case "$path" in
        ""|"/"|"$BACKUP_ROOT") return 0 ;;
    esac
    case "$path" in
        "$BACKUP_ROOT"/?*) ;;
        *) log "refusing to remove path outside BACKUP_ROOT: $path"; return 0 ;;
    esac
    case "$path" in
        "$BACKUP_ROOT"/*/*) log "refusing to remove nested path: $path"; return 0 ;;
    esac
    if [ -L "$path" ]; then
        log "refusing to remove symlink: $path"
        return 0
    fi
    resolved=$(realpath -m -- "$path" 2>/dev/null) \
        || { log "refusing to remove unresolvable path: $path"; return 0; }
    case "$resolved" in
        "$BACKUP_ROOT"/*) rm -rf -- "$path" ;;
        *) log "refusing to remove path resolving outside BACKUP_ROOT: $path" ;;
    esac
}

cleanup() {
    safe_rm_rf "${STAGING:-}"
    safe_rm_rf "$LOCK_FILE"
    [ -n "${retention_list:-}" ] && rm -f "$retention_list" || true
}

compose() {
    docker compose -f "$COMPOSE_FILE" "$@"
}

# --- Preflight --------------------------------------------------------------
# Fail early and clearly if any required tool, or a tool option this script
# relies on, is missing. Runs before any state is touched (no backup root, no
# lock, no containers), so a broken environment aborts with a clear message
# instead of a cryptic mid-run failure.
preflight() {
    for tool in docker realpath date tar find mktemp id mkdir chmod mv rm cat kill basename; do
        command -v "$tool" >/dev/null 2>&1 || die "required tool '$tool' is not available on PATH"
    done
    docker compose version >/dev/null 2>&1 \
        || die "docker Compose v2 plugin is unavailable ('docker compose version' failed); install docker-compose-plugin"
    realpath -m -- / >/dev/null 2>&1 \
        || die "realpath does not support 'realpath -m --' (GNU realpath required)"
    date -u '+%Y%m%d-%H%M%S' >/dev/null 2>&1 \
        || die "date does not support 'date -u +FORMAT'"
    find / -maxdepth 0 -mtime +0 -print >/dev/null 2>&1 \
        || die "find does not support '-maxdepth'/'-mtime' (GNU find required)"
    _preflight_tmp=$(mktemp -d "${TMPDIR:-/tmp}/ecosologic-backup-preflight.XXXXXX") \
        || die "mktemp does not support a '-d TEMPLATE' form"
    rm -rf -- "$_preflight_tmp"
}

# --- Locking ---------------------------------------------------------------
acquire_lock() {
    if mkdir "$LOCK_FILE" 2>/dev/null; then
        printf '%s\n' "$$" > "$LOCK_FILE/pid"
        return 0
    fi
    if [ -f "$LOCK_FILE/pid" ]; then
        lock_pid=$(cat "$LOCK_FILE/pid" 2>/dev/null || true)
        if [ -n "${lock_pid:-}" ] && ! kill -0 "$lock_pid" 2>/dev/null; then
            log "removing stale lock from pid $lock_pid"
            safe_rm_rf "$LOCK_FILE"
            if mkdir "$LOCK_FILE" 2>/dev/null; then
                printf '%s\n' "$$" > "$LOCK_FILE/pid"
                return 0
            fi
        fi
    fi
    die "another backup is already running (lock: $LOCK_FILE)"
}

# --- Volume backup ---------------------------------------------------------
# Usage: backup_volume VOLUME_NAME LABEL [RUN_AS_ROOT]
# Archives the named volume into $STAGING/LABEL.tar.gz using a throwaway
# container: read-only source mount (production is never stopped) and
# `--network none`. tar writes to stdout and the host redirects it to the
# archive file, so the file is always created by the invoking user (never
# root-owned). By default tar runs as the host user's uid/gid; pass
# RUN_AS_ROOT=1 to run tar as root so it can read a root-owned volume (e.g. the
# protection keys volume). The archive is chmod'd 0600 afterwards.
backup_volume() {
    volume="$1"
    label="$2"
    run_as_root="${3:-0}"
    archive="${STAGING}/${label}.tar.gz"
    tar_user="$(id -u):$(id -g)"

    log "backing up volume $volume"
    if ! docker volume inspect "$volume" >/dev/null 2>&1; then
        die "volume '$volume' does not exist"
    fi
    if [ "$run_as_root" = "1" ]; then
        tar_user="0"
    fi
    umask 077
    if ! docker run --rm \
        --network none \
        --user "$tar_user" \
        -v "${volume}:/source:ro" \
        "$TAR_IMAGE" \
        tar -czf - -C /source . > "$archive"; then
        die "failed to archive volume '$volume'"
    fi
    chmod 600 "$archive"
}

# --- Main ------------------------------------------------------------------
preflight

validate_retention
validate_config_paths

mkdir -p "$BACKUP_ROOT"
chmod 700 "$BACKUP_ROOT"

# Defense in depth: after creation the root must not be a symlink and must still
# resolve under /home/hannibal.
[ ! -L "$BACKUP_ROOT" ] || die "BACKUP_ROOT must not be a symlink: $BACKUP_ROOT"
recheck=$(realpath -- "$BACKUP_ROOT" 2>/dev/null) || die "cannot resolve BACKUP_ROOT: $BACKUP_ROOT"
case "$recheck" in
    /home/hannibal/*) ;;
    *) die "BACKUP_ROOT resolves outside /home/hannibal: $recheck" ;;
esac

acquire_lock
trap 'cleanup' 0 1 2 15

STAGING=$(mktemp -d "${BACKUP_ROOT}/.staging.XXXXXX") || die "cannot create staging dir"
chmod 700 "$STAGING"

STAMP=$(date -u +%Y%m%d-%H%M%S)
DB_FILE="${STAGING}/db.dump"
MEDIA_FILE="${STAGING}/media.tar.gz"
KEYS_FILE="${STAGING}/keys.tar.gz"

cd "$PROJECT_DIR" || die "cannot cd to $PROJECT_DIR"

log "starting backup (stamp=$STAMP, retention=${RETENTION_DAYS}d)"

# 1. PostgreSQL dump (custom/compressed). No password in args or logs.
log "dumping PostgreSQL database"
if ! compose exec -T "$POSTGRES_SERVICE" sh -c \
    'PGPASSWORD="${POSTGRES_PASSWORD:-}" pg_dump -w -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-ecosologic}" -Fc -Z 6' \
    > "$DB_FILE"; then
    die "pg_dump failed"
fi
[ -s "$DB_FILE" ] || die "pg_dump produced an empty file"
chmod 600 "$DB_FILE"

# 2. Volume archives.
backup_volume "$MEDIA_VOLUME" "media"
backup_volume "$KEYS_VOLUME" "keys" 1

# 3. Validate artifacts before publishing the set.
log "validating database dump (pg_restore --list)"
if ! docker run --rm \
    --network none \
    --user "$(id -u):$(id -g)" \
    -v "${DB_FILE}:/tmp/restore.dump:ro" \
    "$PG_IMAGE" \
    pg_restore --list /tmp/restore.dump >/dev/null; then
    die "pg_restore --list validation failed"
fi

for archive in "$MEDIA_FILE" "$KEYS_FILE"; do
    log "validating archive $(basename "$archive") (tar -tf)"
    [ -s "$archive" ] || die "archive $archive is empty"
    tar -tzf "$archive" >/dev/null || die "tar -tf validation failed for $archive"
done

# 4. Publish the validated set atomically: rename staging into a timestamped
#    directory. The directory name (with all three artifacts present) is the
#    completeness marker.
FINAL_DIR="${BACKUP_ROOT}/${STAMP}"
if [ -e "$FINAL_DIR" ]; then
    die "backup set directory already exists: $FINAL_DIR"
fi
mv "$STAGING" "$FINAL_DIR"
STAGING=""

chmod 700 "$FINAL_DIR"
chmod 600 "$FINAL_DIR/db.dump" "$FINAL_DIR/media.tar.gz" "$FINAL_DIR/keys.tar.gz"

# 5. Retention: remove only complete, expired backup directories; never remove
#    individual artifacts or foreign files. A directory is complete iff it
#    contains all three expected artifacts. The set list is collected with a
#    separate `find` whose exit status is checked explicitly (no pipe masks it).
log "pruning complete backup sets older than ${RETENTION_DAYS} days"
retention_list=$(mktemp "${BACKUP_ROOT}/.retention.XXXXXX") || die "cannot create retention list"
chmod 600 "$retention_list"
if ! find "$BACKUP_ROOT" -maxdepth 1 -type d -name '????????-??????' -mtime +"$RETENTION_DAYS" -print > "$retention_list"; then
    die "retention find failed"
fi
while IFS= read -r d; do
    [ -n "$d" ] || continue
    if [ -f "$d/db.dump" ] && [ -f "$d/media.tar.gz" ] && [ -f "$d/keys.tar.gz" ]; then
        log "removing old backup set $(basename "$d")"
        safe_rm_rf "$d"
    else
        log "skipping incomplete directory $(basename "$d")"
    fi
done < "$retention_list"
rm -f "$retention_list"
retention_list=""

log "backup complete: ${FINAL_DIR}"
