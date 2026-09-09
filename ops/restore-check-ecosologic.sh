#!/bin/sh
# Validates a set of ecosologic backups without touching production.
#
# Usage:
#   ops/restore-check-ecosologic.sh [BACKUP_DIR] [STAMP]
#
#   BACKUP_DIR  Directory containing the backup sets. Restricted: must be an
#               absolute, existing, non-symlink, canonical path free of `.`/`..`
#               components under BACKUP_ALLOWED_ROOT (default
#               /home/hannibal/backups). Defaults to BACKUP_ROOT.
#   STAMP       Timestamp of the set to check (default: the most recent complete
#               set).
#
# A "set" is a directory named YYYYMMDD-HHMMSS containing `db.dump`,
# `media.tar.gz` and `keys.tar.gz` (see ops/backup-ecosologic.sh).
#
# PostgreSQL: runs `pg_restore --list` and, by default, spins up a throwaway
# isolated PostgreSQL container (random unique name, ownership label) and
# performs a real restore plus a quick query. The dump is never bind-mounted:
# it is streamed over stdin (`docker run -i` / `docker exec -i`), so the private
# db.dump never needs world-readable permissions. The full restore runs with
# `pg_restore --no-owner --no-privileges`: the production dump records the
# production owner role (e.g. `ecosologic_app`) and its GRANTs, but the
# throwaway container only has the `postgres` superuser with a dummy password,
# so restoring owners/privileges would fail with "role ... does not exist".
# Skipping those statements still restores the full schema and data, which is
# all the restore check needs to prove the dump is restorable. (The alternative
# of pre-creating the production role in the throwaway container would leak the
# production role name/grant layout into an ephemeral environment for no
# benefit.) Volumes: validates integrity with `tar -tf`; archive entry
# validation (traversal/absolute/symlink/hardlink/special file) is ALWAYS
# performed. With VERIFY_EXTRACT=1 a full extraction is also performed.
#
# Nothing here reads or modifies production data:
#   * BACKUP_ALLOWED_ROOT and BACKUP_DIR are required to be absolute, free of
#     `.`/`..` components, non-symlinks, and canonicalized (symlinks resolved)
#     strictly under /home/hannibal (BACKUP_DIR further under the allowed root).
#   * Auto-selection (no STAMP) considers only directories whose name is a
#     strict YYYYMMDD-HHMMSS timestamp that is also semantically valid (checked
#     via `date -d`, not regex alone), iterates them newest-first, and picks the
#     most recent COMPLETE set (db.dump, media.tar.gz and keys.tar.gz all
#     regular, non-symlink files each with exactly one hard link / link count
#     1); foreign directories, impossible timestamps and partial sets are
#     ignored, and it fails if no complete set exists.
#   * At startup the selected set's SOURCE artifacts are validated (regular
#     file, not a symlink, link count 1 via `stat -c %h`) before being copied
#     into a private, mode-700 WORKDIR with `cp --reflink=never -P` (a real,
#     independent copy that never follows symlinks). The private copies are
#     then validated again for symlinks/hard links/canonical paths. Only the
#     private copies are ever read (the source set is never mounted or
#     extracted), and the copy itself is verified: the source's identity
#     (device/inode/size/mtime/birth via `stat -c %d:%i:%s:%Y:%W`) is compared
#     before and after the copy and again after checksumming, and the copy is
#     checksum-compared to the source with sha256sum (not the weak cksum). We do
#     NOT claim atomicity (only
#     a filesystem snapshot can be truly atomic): if the source changes during
#     the copy we fail rather than operate on a torn snapshot.
#   * All work happens in a private temporary directory and in ephemeral
#     containers created by this run (removed only by label ownership; removal
#     is retried and confirmed absent via `docker inspect`, and never touches a
#     pre-existing container). A `docker inspect` daemon/command error is
#     treated as an unknown state, never as "container absent". If the throwaway
#     container cannot be confirmed removed, the run preserves its name and
#     label for diagnosis and exits non-zero (never silent success).
#   * Every temporary container runs with `--network none` (no networking).
#   * The full restore connects over the container-local unix socket via
#     `docker exec`, never over the network.
#   * A throwaway dummy password is used (never the production password).
#   * No production volume is ever mounted.
#   * Tar archives are validated for path traversal, absolute paths, symlinks
#     and hard links before any extraction, and validation/extraction operate on
#     the private copy so the source archive cannot be swapped mid-check.

set -eu

umask 077

BACKUP_ROOT="${BACKUP_ROOT:-/home/hannibal/backups/ecosologic}"
BACKUP_ALLOWED_ROOT="${BACKUP_ALLOWED_ROOT:-/home/hannibal/backups}"
PG_IMAGE="${PG_IMAGE:-postgres:16-alpine}"
VERIFY_FULL_RESTORE="${VERIFY_FULL_RESTORE:-1}"
VERIFY_EXTRACT="${VERIFY_EXTRACT:-0}"

CNAME=""
WORKDIR=""
CONTAINER_LEAKED=0
RESTORE_CHECK_TAG="ecosologic-restore-check-$$"

log() {
    printf '%s\n' "[restore-check] $(date -u '+%Y-%m-%dT%H:%M:%SZ') $*"
}

die() {
    printf '%s\n' "[restore-check] ERROR: $*" >&2
    exit 1
}

# Classify the result of `docker inspect NAME`:
#   returns 0 -> the container exists
#   returns 1 -> the container is definitively absent (docker reports
#                "no such object"/"no such container", matched
#                case-insensitively, OR exits 1 with no output at all)
#   returns 2 -> daemon/command error (state unknown)
# `docker inspect` returns exit 1 both for "no such object" and for several
# daemon/command failures, so the exit status alone is not enough to conclude
# the container is gone. The message is the discriminating signal, matched
# case-insensitively: docker has emitted both "No such object" and the lowercase
# "error: no such object". Some docker versions report a missing object with
# exit 1 and NO output whatsoever; an exit 1 with empty output must therefore
# also be classified as absent, otherwise a successful `docker rm` would be
# followed by a spurious "docker inspect failed after removal" error. A
# daemon/command failure (e.g. "Cannot connect to the Docker daemon...")
# produces a non-empty message that does not match the absent pattern and still
# falls through to the unknown state.
inspect_state() {
    name="$1"
    out=$(docker inspect "$name" 2>&1); rc=$?
    if [ "$rc" -eq 0 ]; then
        return 0
    fi
    if printf '%s\n' "$out" | grep -Eiq 'no such (object|container)'; then
        return 1
    fi
    if [ "$rc" -eq 1 ] && [ -z "$out" ]; then
        return 1
    fi
    return 2
}

# Remove the throwaway container this run created. Never touches a container
# this run did not create (verified by the unique ownership label). Retries
# removal and only clears CNAME once removal is CONFIRMED absent via
# `docker inspect` (a successful `docker rm` alone is not enough). A daemon/
# command error while inspecting is treated as an unknown state: CNAME is not
# cleared, CONTAINER_LEAKED is set and a non-zero exit is returned so the run
# can never report silent success.
remove_check_container() {
    if [ -z "${CNAME:-}" ]; then
        return 0
    fi
    cname="$CNAME"

    inspect_state "$cname"
    state=$?
    case "$state" in
        0) ;;
        1) CNAME=""; return 0 ;;
        2)
            printf '%s\n' "[restore-check] ERROR: docker inspect failed while checking throwaway container (kept for diagnosis): name=$cname label=$RESTORE_CHECK_TAG" >&2
            CONTAINER_LEAKED=1
            return 1
            ;;
    esac

    owner_out=$(docker inspect -f '{{ index .Config.Labels "ecosologic.restore-check" }}' "$cname" 2>&1); owner_rc=$?
    if [ "$owner_rc" -ne 0 ]; then
        # The container was present moments ago; re-classify through the shared
        # helper so an exit 1 with "no such object" OR empty output (container
        # vanished between the two commands) is treated as absent, while a
        # daemon/command error remains an unknown state.
        inspect_state "$cname"
        state=$?
        case "$state" in
            1) CNAME=""; return 0 ;;
            *)
                printf '%s\n' "[restore-check] ERROR: docker inspect failed while reading ownership label (kept for diagnosis): name=$cname label=$RESTORE_CHECK_TAG" >&2
                CONTAINER_LEAKED=1
                return 1
                ;;
        esac
    fi
    if [ "$owner_out" != "$RESTORE_CHECK_TAG" ]; then
        printf '%s\n' "[restore-check] ERROR: refusing to remove container not owned by this run (kept for diagnosis): name=$cname label=$owner_out" >&2
        CONTAINER_LEAKED=1
        return 1
    fi

    i=0
    while [ "$i" -lt 5 ]; do
        if docker rm -f "$cname" >/dev/null 2>&1; then
            inspect_state "$cname"
            state=$?
            case "$state" in
                1) CNAME=""; return 0 ;;
                2)
                    printf '%s\n' "[restore-check] ERROR: docker inspect failed after removal (state unknown, kept for diagnosis): name=$cname label=$RESTORE_CHECK_TAG" >&2
                    CONTAINER_LEAKED=1
                    return 1
                    ;;
            esac
        fi
        i=$((i + 1))
        sleep 1
    done
    printf '%s\n' "[restore-check] ERROR: could not remove throwaway container (kept for diagnosis): name=$cname label=$RESTORE_CHECK_TAG" >&2
    CONTAINER_LEAKED=1
    return 1
}

cleanup() {
    remove_check_container || true
    if [ -n "${WORKDIR:-}" ]; then
        rm -rf -- "$WORKDIR"
    fi
    if [ "${CONTAINER_LEAKED:-0}" -ne 0 ]; then
        exit 1
    fi
}
trap 'cleanup' 0 1 2 15

# Generate a non-predictable suffix for the throwaway container name.
random_suffix() {
    if [ -r /proc/sys/kernel/random/uuid ]; then
        cat /proc/sys/kernel/random/uuid
        return 0
    fi
    suffix=$(od -An -N6 -tx1 /dev/urandom 2>/dev/null | tr -d ' \n')
    if [ -z "$suffix" ]; then
        suffix="$(date +%s)-$$"
    fi
    printf '%s' "$suffix"
}

# Restrict BACKUP_DIR to the allowed backup root: absolute, no `..`, existing,
# not a symlink, and canonicalized under BACKUP_ALLOWED_ROOT. Echoes canonical.
resolve_backup_dir() {
    requested="$1"
    case "$requested" in
        /*) ;;
        *) die "backup dir must be an absolute path (got: $requested)" ;;
    esac
    case "/$requested/" in
        */../*) die "backup dir must not contain '..' components (got: $requested)" ;;
    esac
    case "/$requested/" in
        */./*) die "backup dir must not contain '.' components (got: $requested)" ;;
    esac
    [ -d "$requested" ] || die "backup dir does not exist: $requested"
    [ ! -L "$requested" ] || die "backup dir must not be a symlink: $requested"
    canonical=$(realpath -- "$requested" 2>/dev/null) \
        || die "cannot canonicalize backup dir: $requested"
    case "$canonical" in
        "$BACKUP_ALLOWED_ROOT"/*) ;;
        *) die "backup dir must be under $BACKUP_ALLOWED_ROOT (resolved: $canonical)" ;;
    esac
    printf '%s\n' "$canonical"
}

# BACKUP_ALLOWED_ROOT must be an absolute, existing, non-symlink path free of
# `.`/`..` components whose canonical (symlink-resolved) path is strictly under
# /home/hannibal. Echoes the canonical path on success; dies otherwise.
validate_allowed_root() {
    root="$1"
    case "$root" in
        /*) ;;
        *) die "BACKUP_ALLOWED_ROOT must be an absolute path (got: $root)" ;;
    esac
    case "/$root/" in
        */../*) die "BACKUP_ALLOWED_ROOT must not contain '..' components (got: $root)" ;;
    esac
    case "/$root/" in
        */./*) die "BACKUP_ALLOWED_ROOT must not contain '.' components (got: $root)" ;;
    esac
    [ -d "$root" ] || die "BACKUP_ALLOWED_ROOT does not exist or is not a directory: $root"
    [ ! -L "$root" ] || die "BACKUP_ALLOWED_ROOT must not be a symlink: $root"
    canonical=$(realpath -- "$root" 2>/dev/null) \
        || die "cannot canonicalize BACKUP_ALLOWED_ROOT: $root"
    case "$canonical" in
        /home/hannibal/*) ;;
        *) die "BACKUP_ALLOWED_ROOT must resolve under /home/hannibal (resolved: $canonical)" ;;
    esac
    printf '%s\n' "$canonical"
}

# Validate a tar archive's entries: reject absolute paths, `..` components,
# symlinks, hard links and any other non-regular/non-directory entry. Always
# mandatory; runs on a private copy of the archive.
validate_tar_entries() {
    archive="$1"
    if tar -tzf "$archive" | grep -E '(^/|(^|/)\.\.(/|$))' >/dev/null 2>&1; then
        return 1
    fi
    if tar -tvzf "$archive" | awk '{print $1}' | grep -Ev '^[d-]' >/dev/null 2>&1; then
        return 1
    fi
    return 0
}

# Return the hard-link count (st_nlink) of a file (GNU stat), or empty on error.
link_count() {
    stat -c %h -- "$1" 2>/dev/null
}

# Reject symlinks and hard links on a SOURCE artifact before it is copied into
# the private workdir by copy_snapshot: it must be a regular file (not a
# symlink) with exactly one hard link (link count 1).
require_regular_source_artifact() {
    f="$1"
    [ ! -L "$f" ] || die "source backup artifact must not be a symlink: $f"
    [ -f "$f" ] || die "source backup artifact is missing or not a regular file: $f"
    links=$(link_count "$f")
    [ -n "$links" ] || die "cannot stat source backup artifact: $f"
    [ "$links" = "1" ] || die "source backup artifact must not be a hard link: $f"
}

# Reject symlinks and hard links on a (private-copy) artifact: it must be a
# regular file (not a symlink) whose canonical path stays inside the canonical
# PRIV_SET, and it must have exactly one hard link (link count 1).
require_regular_artifact() {
    f="$1"
    [ ! -L "$f" ] || die "backup artifact must not be a symlink: $f"
    [ -f "$f" ] || die "backup artifact is missing or not a regular file: $f"
    canon=$(realpath -- "$f" 2>/dev/null) || die "cannot canonicalize backup artifact: $f"
    case "$canon" in
        "$PRIV_SET"/?*) ;;
        *) die "backup artifact resolves outside the set: $f (resolved: $canon)" ;;
    esac
    links=$(link_count "$f")
    [ -n "$links" ] || die "cannot stat backup artifact: $f"
    [ "$links" = "1" ] || die "backup artifact must not be a hard link: $f"
}

# Copy a source artifact into a private, verified snapshot. We do NOT claim
# atomicity (only a filesystem snapshot can be truly atomic): what we guarantee
# is that the private copy is a complete, self-consistent byte image of the
# source at a stable point, so nothing downstream ever depends on the source.
# The source is copied with `cp --reflink=never` (a real, independent copy that
# never shares data blocks with the source) and never follows symlinks (`-P`).
# The source identity (device/inode/size/mtime/birth) is captured before and
# after the copy and again after the checksum; if it changed the copy may be
# torn and we fail. Finally the copy is checksum-compared to the source with
# sha256sum (a cryptographic digest, not the weak POSIX cksum). If sha256sum is
# unavailable the run fails clearly rather than falling back to a weak check.
copy_snapshot() {
    src="$1"
    dst="$2"
    before=$(stat -c '%d:%i:%s:%Y:%W' -- "$src" 2>/dev/null) \
        || die "cannot stat source artifact: $src"
    cp --reflink=never -P -- "$src" "$dst" \
        || die "cannot copy artifact into private workdir: $src"
    after=$(stat -c '%d:%i:%s:%Y:%W' -- "$src" 2>/dev/null) \
        || die "cannot re-stat source artifact: $src"
    if [ "$before" != "$after" ]; then
        die "source changed during copy (torn snapshot refused): $src"
    fi
    src_sum=$(sha256sum -- "$src" | awk '{print $1}')
    dst_sum=$(sha256sum -- "$dst" | awk '{print $1}')
    [ "$src_sum" = "$dst_sum" ] \
        || die "private copy does not match source (integrity check failed): $src"
    # Re-check the source identity after the checksum read: a concurrent write
    # to the source while sha256sum was reading it could otherwise go unnoticed.
    final=$(stat -c '%d:%i:%s:%Y:%W' -- "$src" 2>/dev/null) \
        || die "cannot re-stat source artifact after checksum: $src"
    [ "$before" = "$final" ] \
        || die "source changed during checksum (torn snapshot refused): $src"
}

# A set is complete when it is a real directory (not a symlink) containing all
# three expected artifacts as regular, non-symlink files, each with exactly one
# hard link (link count 1).
set_is_complete() {
    setdir="$1"
    [ -d "$setdir" ] || return 1
    [ ! -L "$setdir" ] || return 1
    for a in db.dump media.tar.gz keys.tar.gz; do
        f="$setdir/$a"
        [ -f "$f" ] || return 1
        [ ! -L "$f" ] || return 1
        links=$(link_count "$f")
        [ -n "$links" ] || return 1
        [ "$links" = "1" ] || return 1
    done
    return 0
}

# Semantically validate a YYYYMMDD-HHMMSS stamp. The strict regex alone accepts
# impossible dates/times (month 99, hour 25, 30 February); this normalizes the
# stamp with `date -d` (GNU, available on the server) and requires it to
# round-trip to itself, so impossible timestamps are rejected. Returns 0 only
# when the stamp is valid.
is_valid_stamp() {
    s="$1"
    case "$s" in
        [0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]) ;;
        *) return 1 ;;
    esac
    iso=$(printf '%s' "$s" | sed 's/\(....\)\(..\)\(..\)-\(..\)\(..\)\(..\)/\1-\2-\3 \4:\5:\6/')
    norm=$(date -d "$iso" '+%Y%m%d-%H%M%S' 2>/dev/null) || return 1
    [ "$norm" = "$s" ] || return 1
}

# Choose the most recent COMPLETE backup set. Only directories whose name is a
# strict, semantically valid YYYYMMDD-HHMMSS timestamp are considered; foreign
# files/directories, impossible timestamps, partial sets and non-numeric names
# are ignored. Candidates are iterated newest-first and the first complete, safe
# set wins. Prints the stamp on success and nothing otherwise.
auto_select_stamp() {
    d="$1"
    find "$d" -maxdepth 1 -mindepth 1 -type d 2>/dev/null \
        | sed 's|.*/||' | sort -r | {
            while IFS= read -r s; do
                [ -n "$s" ] || continue
                is_valid_stamp "$s" || continue
                if set_is_complete "$d/$s"; then
                    printf '%s\n' "$s"
                    break
                fi
            done
        }
}

# --- Preflight --------------------------------------------------------------
# Fail early and clearly if any required tool, or a tool option this script
# relies on, is missing. Runs before any backup directory is resolved or any
# container/state is touched, so a broken environment aborts with a clear
# message instead of a cryptic mid-run failure.
preflight() {
    for tool in docker realpath date stat cp sha256sum tar find awk sed grep sort mktemp cat od tr wc rm chmod mkdir sleep basename; do
        command -v "$tool" >/dev/null 2>&1 || die "required tool '$tool' is not available on PATH"
    done
    realpath -- / >/dev/null 2>&1 \
        || die "realpath does not support 'realpath --'"
    stat -c '%h' / >/dev/null 2>&1 \
        || die "stat does not support 'stat -c' (GNU stat required)"
    date -d '2026-01-02 03:30:00' >/dev/null 2>&1 \
        || die "date does not support 'date -d' (GNU date required)"
    find / -maxdepth 0 -mindepth 0 -print >/dev/null 2>&1 \
        || die "find does not support '-maxdepth'/'-mindepth' (GNU find required)"
    printf 'x' | sha256sum >/dev/null 2>&1 \
        || die "sha256sum is not available on PATH"
    _preflight_dir=$(mktemp -d "${TMPDIR:-/tmp}/ecosologic-restore-preflight.XXXXXX") \
        || die "mktemp does not support a '-d TEMPLATE' form"
    printf 'x' > "$_preflight_dir/a"
    cp --reflink=never -P -- "$_preflight_dir/a" "$_preflight_dir/b" >/dev/null 2>&1 \
        || { rm -rf -- "$_preflight_dir"; die "cp does not support '--reflink=never' (GNU coreutils cp required)"; }
    rm -rf -- "$_preflight_dir"
}

preflight

# The allowed root must be absolute, free of `.`/`..`, not a symlink, and must
# resolve (symlinks resolved) under /home/hannibal.
BACKUP_ALLOWED_ROOT=$(validate_allowed_root "$BACKUP_ALLOWED_ROOT")

dir_raw="${1:-$BACKUP_ROOT}"
stamp="${2:-}"

dir=$(resolve_backup_dir "$dir_raw")

if [ -z "$stamp" ]; then
    stamp=$(auto_select_stamp "$dir")
    [ -n "$stamp" ] || die "no complete backup sets found in $dir (need db.dump, media.tar.gz and keys.tar.gz)"
fi

# STAMP must be a semantically valid YYYYMMDD-HHMMSS timestamp (the naming used
# by backup-ecosologic.sh); impossible dates/times are rejected.
is_valid_stamp "$stamp" || die "invalid stamp: $stamp"

SET_DIR="$dir/$stamp"

log "checking backup set for stamp=$stamp in $dir"

# The set directory must be a real directory (not a symlink) whose canonical
# path is exactly $dir/$stamp.
[ -d "$SET_DIR" ] || die "backup set directory does not exist: $SET_DIR"
[ ! -L "$SET_DIR" ] || die "backup set directory must not be a symlink: $SET_DIR"
set_dir_canon=$(realpath -- "$SET_DIR" 2>/dev/null) || die "cannot canonicalize backup set: $SET_DIR"
[ "$set_dir_canon" = "$SET_DIR" ] || die "backup set directory resolves outside its path: $set_dir_canon"

# --- Private working copy (verified snapshot) ------------------------------
# Copy the selected set into a private, mode-700 WORKDIR, then validate the
# private copies. Only the private copies are used for pg_restore/tar/extract;
# the source set is never mounted or extracted directly. Each copy is verified
# against its source (see copy_snapshot): the source identity is checked
# before/after and the copy is checksum-compared, so a concurrent change to the
# source causes a failure instead of a torn snapshot.
WORKDIR=$(mktemp -d) || die "cannot create temporary directory"
chmod 700 "$WORKDIR"

PRIV_SET="$WORKDIR/set"
mkdir -p "$PRIV_SET"
for a in db.dump media.tar.gz keys.tar.gz; do
    require_regular_source_artifact "$SET_DIR/$a"
    copy_snapshot "$SET_DIR/$a" "$PRIV_SET/$a"
done

DB_FILE="$PRIV_SET/db.dump"
MEDIA_FILE="$PRIV_SET/media.tar.gz"
KEYS_FILE="$PRIV_SET/keys.tar.gz"

for f in "$DB_FILE" "$MEDIA_FILE" "$KEYS_FILE"; do
    require_regular_artifact "$f"
done

# --- PostgreSQL ------------------------------------------------------------
# The private dump is streamed over stdin (never bind-mounted), so it never
# needs world-readable permissions and is never exposed as a mount inside the
# throwaway container.
log "pg_restore --list ($(basename "$DB_FILE"))"
docker run --rm -i \
    --network none \
    "$PG_IMAGE" \
    pg_restore --list < "$DB_FILE" >/dev/null \
    || die "pg_restore --list failed"

if [ "$VERIFY_FULL_RESTORE" != "0" ]; then
    CNAME="ecosologic-restore-check-$(random_suffix)"
    log "starting isolated PostgreSQL container ($CNAME, no network)"
    docker run -d --name "$CNAME" \
        --network none \
        --label "ecosologic.restore-check=$RESTORE_CHECK_TAG" \
        -e POSTGRES_PASSWORD=restorecheck \
        -e POSTGRES_DB=restorecheck \
        "$PG_IMAGE" >/dev/null \
        || die "failed to start isolated PostgreSQL container"

    log "waiting for isolated PostgreSQL to be ready"
    i=0
    until docker exec "$CNAME" pg_isready -U postgres -d restorecheck >/dev/null 2>&1; do
        i=$((i + 1))
        [ "$i" -lt 60 ] || die "isolated PostgreSQL did not become ready"
        sleep 1
    done

    log "restoring dump into isolated database (docker exec, stdin, no network)"
    # The production dump records the production owner role (e.g. ecosologic_app)
    # and its GRANTs. The throwaway container only has the `postgres` superuser
    # (dummy password), so restoring owners/privileges would fail with
    # "role ... does not exist". `--no-owner --no-privileges` skips ownership and
    # access-control statements while still restoring the full schema and data,
    # which is all this check needs to validate that the dump is restorable.
    docker exec -i -e PGPASSWORD=restorecheck "$CNAME" \
        pg_restore --no-owner --no-privileges -w -U postgres -d restorecheck < "$DB_FILE" \
        || die "full pg_restore failed"

    log "verifying restored database contains application tables"
    table_count=$(docker exec -e PGPASSWORD=restorecheck "$CNAME" \
        psql -U postgres -d restorecheck -tAc \
        "SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');") \
        || die "psql verification query failed"
    [ "${table_count:-0}" -gt 0 ] || die "restored database has no application tables"

    log "full restore check OK (${table_count} tables restored)"

    remove_check_container || die "throwaway container was not removed (kept for diagnosis): name=$CNAME label=$RESTORE_CHECK_TAG"
fi

# --- Volumes ---------------------------------------------------------------
for archive in "$MEDIA_FILE" "$KEYS_FILE"; do
    log "validating archive $(basename "$archive")"
    [ -s "$archive" ] || die "archive $archive is empty"

    # Private copy so entry validation and (optional) extraction run on the same
    # non-racy file inside our mode-700 WORKDIR.
    priv="$WORKDIR/archives/$(basename "$archive")"
    mkdir -p "$WORKDIR/archives"
    cp -- "$archive" "$priv" || die "cannot copy archive $archive"

    log "listing $(basename "$archive") (tar -tf)"
    tar -tzf "$priv" >/dev/null || die "tar -tf failed for $archive"

    log "validating $(basename "$archive") entries (mandatory)"
    if ! validate_tar_entries "$priv"; then
        die "unsafe tar entries in $archive (absolute path, traversal, symlink, hard link or special file)"
    fi

    if [ "$VERIFY_EXTRACT" != "0" ]; then
        extract_dir="$WORKDIR/extract/$(basename "$archive" .tar.gz)"
        mkdir -p "$extract_dir"
        log "extracting $(basename "$archive") into $extract_dir"
        tar -xzf "$priv" -C "$extract_dir" || die "tar extraction failed for $archive"
        count=$(find "$extract_dir" -type f | wc -l | tr -d ' ')
        log "extracted $count files from $(basename "$archive")"
    fi
done

log "restore check passed for stamp=$stamp"
