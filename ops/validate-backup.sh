#!/bin/sh
# Static validation for the backup/restore scripts, the monitoring validate
# script, and their systemd units. Run from the repository root (or anywhere;
# the script resolves its own location). No Docker required.
#
# Checks:
#   * POSIX syntax (sh -n) of every script.
#   * No hardcoded production secrets in the backup script.
#   * No forbidden `docker compose down -v` (would destroy named volumes).
#   * Permissions hardening: umask 077, chmod 700/600, containers run as the
#     host user and with `--network none`.
#   * Path canonicalization/validation (realpath) and symlink rejection;
#     BACKUP_ROOT/LOCK_FILE under /home/hannibal; safe cleanup that never
#     removes arbitrary paths or follows symlinks.
#   * RETENTION_DAYS integer validation (1..365) and explicit `find` status
#     checking (no pipe-masked exit status).
#   * Atomic directory layout and directory-based retention.
#   * Tar entry validation (traversal/absolute/symlink/hardlink) always
#     mandatory before extraction; validation/extraction on a private copy.
#   * Restore-check restricts BACKUP_DIR to an allowed backup root (itself
#     canonicalized with symlinks resolved and validated under /home/hannibal
#     and non-symlink), rejects symlinks/non-canonical paths, rejects symlink/
#     hard-link backup set artifacts (requiring regular files with link count 1
#     both at the source before the `cp --reflink=never` private copy and on the
#     private copies inside the canonical set), and verifies the private copy
#     (source identity device/inode/size/mtime/birth before/after the copy and
#     after the checksum, plus a sha256sum comparison) without claiming
#     atomicity. It auto-selects the newest complete set by strict timestamp
#     (semantically validated via `date -d`, newest-first, ignoring foreign dirs
#     and impossible timestamps), streams the dump over stdin (never a bind
#     mount), uses a random container name with label ownership, and runs
#     ephemeral containers with `--network none` (restore via `docker exec`,
#     never `--network container:`); container removal is retried and confirmed
#     absent via `docker inspect` (distinguishing "no such object" from a
#     daemon/command error), never touches a pre-existing container, and a
#     leaked container yields a non-zero exit with its name/label preserved for
#     diagnosis.
#   * The keys volume is archived by a tar running as root whose stdout is
#     redirected by the host so the final file is created by the invoking user
#     (chmod 600), never root-owned.
#   * Monitoring validate forces dummy secrets, uses umask 077 and a private
#     temp dir with an exit trap (no credentials in shared /tmp).
#   * systemd OnFailure wiring to an identifiable failure unit.
#   * systemd unit invokes the backup via `/bin/sh` (no dependency on the
#     executable bit).
#   * Explicit preflight in both scripts: every required tool (docker,
#     realpath, tar, find, mktemp, id, stat, cp, sha256sum, awk, sed, grep,
#     sort, date, etc.) and every GNU-specific option (realpath -m/--, stat -c,
#     cp --reflink=never, date -d, find -maxdepth/-mindepth/-mtime) is checked
#     up front and fails early with a clear message.
set -eu

cd "$(dirname "$0")/.."

fail=0

require() {
    desc="$1"
    file="$2"
    pattern="$3"
    if grep -qF -- "$pattern" "$file"; then
        echo "ok: $desc"
    else
        echo "ERROR: $desc (missing from $file)" >&2
        fail=1
    fi
}

forbid() {
    desc="$1"
    file="$2"
    pattern="$3"
    if grep -qF -- "$pattern" "$file"; then
        echo "ERROR: $desc (found in $file)" >&2
        fail=1
    else
        echo "ok: $desc"
    fi
}

# --- Syntax -----------------------------------------------------------------
for script in ops/backup-ecosologic.sh ops/restore-check-ecosologic.sh ops/validate-backup.sh monitoring/validate.sh; do
    echo "sh -n $script"
    if ! sh -n "$script"; then
        echo "ERROR: syntax error in $script" >&2
        fail=1
    fi
done

# --- No hardcoded secrets ---------------------------------------------------
# The production backup must not contain hardcoded secrets. (The restore-check
# script intentionally uses a throwaway dummy POSTGRES_PASSWORD=restorecheck for
# its isolated container, and monitoring/validate.sh forces dummy values; those
# are not production secrets.)
for pattern in 'AUTH_JWT_KEY=' 'AUTH_ADMIN_PASSWORD=' 'GRAFANA_ADMIN_PASSWORD=' 'ALERT_SMTP_PASSWORD=' 'POSTGRES_PASSWORD='; do
    if grep -nF "$pattern" ops/backup-ecosologic.sh; then
        echo "ERROR: possible hardcoded secret ($pattern) in ops/backup-ecosologic.sh" >&2
        fail=1
    fi
done

# --- No destructive compose operations --------------------------------------
# Neither script may run `down -v` (would destroy named volumes).
for script in ops/backup-ecosologic.sh ops/restore-check-ecosologic.sh; do
    if grep -n 'down -v' "$script"; then
        echo "ERROR: forbidden 'down -v' in $script" >&2
        fail=1
    fi
done

# --- Permissions hardening (backup) -----------------------------------------
require "umask 077 set" ops/backup-ecosologic.sh 'umask 077'
require "backup root chmod 700" ops/backup-ecosologic.sh 'chmod 700'
require "artifact chmod 600" ops/backup-ecosologic.sh 'chmod 600'
require "containers run as host uid/gid" ops/backup-ecosologic.sh 'id -u'

# --- Volume archive written to host stdout (never root-owned) ---------------
require "volume archive streamed to host stdout" ops/backup-ecosologic.sh 'tar -czf - -C /source .'
require "keys volume tar runs as root" ops/backup-ecosologic.sh 'backup_volume "$KEYS_VOLUME" "keys" 1'
forbid "volume archive not written inside container" ops/backup-ecosologic.sh '-v "${STAGING}:/backup"'

# --- Path canonicalization and safe cleanup (backup) ------------------------
require "BACKUP_ROOT validation helper" ops/backup-ecosologic.sh 'validate_backup_root'
require "config path validation wired" ops/backup-ecosologic.sh 'validate_config_paths'
require "LOCK_FILE derived from BACKUP_ROOT" ops/backup-ecosologic.sh 'LOCK_FILE="${BACKUP_ROOT}/.backup.lock"'
require "path canonicalization via realpath" ops/backup-ecosologic.sh 'realpath -m'
require "rejects '..' path components" ops/backup-ecosologic.sh "must not contain '..' components"
require "safe cleanup helper present" ops/backup-ecosologic.sh 'safe_rm_rf'
require "cleanup refuses symlinks" ops/backup-ecosologic.sh 'refusing to remove symlink'
forbid "rm -rf on BACKUP_ROOT" ops/backup-ecosologic.sh 'rm -rf "$BACKUP_ROOT"'
forbid "rm -rf on STAGING" ops/backup-ecosologic.sh 'rm -rf "$STAGING"'
forbid "rm -rf on LOCK_FILE" ops/backup-ecosologic.sh 'rm -rf "$LOCK_FILE"'

# --- Retention validation and find status -----------------------------------
require "retention integer validation" ops/backup-ecosologic.sh 'validate_retention'
require "retention range 1..365" ops/backup-ecosologic.sh 'RETENTION_DAYS must be between 1 and 365'
require "find failure aborts retention" ops/backup-ecosologic.sh 'retention find failed'

# --- Atomic directory layout and retention ----------------------------------
require "atomic rename into timestamped set" ops/backup-ecosologic.sh 'mv "$STAGING" "$FINAL_DIR"'
require "directory-based retention (-type d)" ops/backup-ecosologic.sh '-type d'

# --- Temporary containers must not depend on the network --------------------
require "backup container uses --network none" ops/backup-ecosologic.sh '--network none'
require "restore-check container uses --network none" ops/restore-check-ecosologic.sh '--network none'
forbid "restore-check shares container network" ops/restore-check-ecosologic.sh '--network container:'
require "restore via docker exec (no network)" ops/restore-check-ecosologic.sh 'docker exec'

# --- Restore-check backup dir restriction and container ownership -----------
require "backup dir allowed root" ops/restore-check-ecosologic.sh 'BACKUP_ALLOWED_ROOT'
require "backup dir rejects symlinks" ops/restore-check-ecosologic.sh 'must not be a symlink'
require "backup dir canonicalized" ops/restore-check-ecosologic.sh 'realpath'
require "random container name" ops/restore-check-ecosologic.sh 'random_suffix'
require "container ownership label" ops/restore-check-ecosologic.sh 'ecosologic.restore-check='

# --- Allowed root canonicalization (symlinks resolved) ----------------------
require "allowed root must be absolute" ops/restore-check-ecosologic.sh 'BACKUP_ALLOWED_ROOT must be an absolute path'
require "allowed root rejects '..' components" ops/restore-check-ecosologic.sh "BACKUP_ALLOWED_ROOT must not contain '..' components"
require "allowed root rejects '.' components" ops/restore-check-ecosologic.sh "BACKUP_ALLOWED_ROOT must not contain '.' components"
require "allowed root must not be a symlink" ops/restore-check-ecosologic.sh 'BACKUP_ALLOWED_ROOT must not be a symlink'
require "allowed root resolves under /home/hannibal" ops/restore-check-ecosologic.sh 'BACKUP_ALLOWED_ROOT must resolve under /home/hannibal'
require "backup dir must be absolute" ops/restore-check-ecosologic.sh 'backup dir must be an absolute path'
require "backup dir rejects '..' components" ops/restore-check-ecosologic.sh "backup dir must not contain '..' components"
require "backup dir rejects '.' components" ops/restore-check-ecosologic.sh "backup dir must not contain '.' components"

# --- Backup set artifacts: no symlinks/hard links, regular files only --------
require "set directory rejects symlinks" ops/restore-check-ecosologic.sh 'backup set directory must not be a symlink'
require "artifact rejects symlinks" ops/restore-check-ecosologic.sh 'backup artifact must not be a symlink'
require "artifact must be a regular file" ops/restore-check-ecosologic.sh 'not a regular file'
require "artifact must stay inside canonical set" ops/restore-check-ecosologic.sh 'resolves outside the set'
require "artifact rejects hard links" ops/restore-check-ecosologic.sh 'backup artifact must not be a hard link'
require "source artifacts validated before copy" ops/restore-check-ecosologic.sh 'require_regular_source_artifact'
require "source artifact rejects hard links" ops/restore-check-ecosologic.sh 'source backup artifact must not be a hard link'
require "source link count via stat -c %h" ops/restore-check-ecosologic.sh 'stat -c %h'

# --- TOCTOU: private workdir copy (never mount/extract the source) ----------
require "private copy via non-reflink full copy" ops/restore-check-ecosologic.sh '--reflink=never'
require "private copy validated before use" ops/restore-check-ecosologic.sh 'PRIV_SET'
require "source identity checked before/after copy" ops/restore-check-ecosologic.sh 'torn snapshot'
require "identity uses nanosecond/birth-time stat format" ops/restore-check-ecosologic.sh '%d:%i:%s:%Y:%W'
require "private copy sha256sum-verified against source" ops/restore-check-ecosologic.sh 'sha256sum --'
require "sha256sum availability enforced" ops/restore-check-ecosologic.sh 'sha256sum is not available'
require "source identity re-checked after checksum" ops/restore-check-ecosologic.sh 'source changed during checksum'
forbid "source set never mounted directly" ops/restore-check-ecosologic.sh '-v "${SET_DIR}'
forbid "does not overclaim TOCTOU atomicity" ops/restore-check-ecosologic.sh 'no TOCTOU against the source'

# --- Restore dump via stdin (never bind-mounted into throwaway container) ----
require "db.dump streamed via stdin (no bind mount)" ops/restore-check-ecosologic.sh 'pg_restore --list <'
require "full restore via docker exec stdin" ops/restore-check-ecosologic.sh 'docker exec -i'
forbid "db.dump never bind-mounted" ops/restore-check-ecosologic.sh '/tmp/restore.dump'

# --- Full restore must not depend on the production role ---------------------
# The dump records the production owner role (e.g. ecosologic_app) and its
# GRANTs, which do not exist in the throwaway container (only the `postgres`
# superuser with a dummy password). The restore must skip owners/privileges.
require "full restore skips owners" ops/restore-check-ecosologic.sh '--no-owner'
require "full restore skips privileges" ops/restore-check-ecosologic.sh '--no-privileges'

# --- Auto-selection of the most recent complete set --------------------------
require "auto-select complete set helper" ops/restore-check-ecosologic.sh 'set_is_complete'
require "auto-select requires source link count 1" ops/restore-check-ecosologic.sh '[ "$links" = "1" ] || return 1'
require "auto-select fails without complete set" ops/restore-check-ecosologic.sh 'no complete backup sets found'
require "auto-select strict timestamp filter" ops/restore-check-ecosologic.sh '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]'
require "auto-select semantic timestamp validation" ops/restore-check-ecosologic.sh 'is_valid_stamp'
require "auto-select normalizes via date -d" ops/restore-check-ecosologic.sh 'date -d'
require "auto-select iterates newest-first" ops/restore-check-ecosologic.sh 'sort -r'

# --- Container removal: retry, confirm, never touch pre-existing -------------
require "cleanup retries container removal" ops/restore-check-ecosologic.sh 'could not remove throwaway container'
require "cleanup never removes non-owned container" ops/restore-check-ecosologic.sh 'refusing to remove container not owned'
require "cleanup classifies inspect state" ops/restore-check-ecosologic.sh 'inspect_state'
require "cleanup distinguishes absent from daemon error" ops/restore-check-ecosologic.sh 'no such (object|container)'
require "cleanup treats empty inspect output as absent" ops/restore-check-ecosologic.sh '[ -z "$out" ]'
require "cleanup treats daemon error as unknown (not absent)" ops/restore-check-ecosologic.sh 'state unknown'
require "container removal confirmed absent before clearing name" ops/restore-check-ecosologic.sh 'remove_check_container'
require "leaked container causes non-zero exit" ops/restore-check-ecosologic.sh 'CONTAINER_LEAKED'
require "leaked container name/label preserved" ops/restore-check-ecosologic.sh 'kept for diagnosis'

# --- Tar entry validation before extraction ---------------------------------
require "tar entry validation helper" ops/restore-check-ecosologic.sh 'validate_tar_entries'
require "symlink/hardlink detection (tar -tvzf)" ops/restore-check-ecosologic.sh 'tar -tvzf'
require "path traversal detection (grep)" ops/restore-check-ecosologic.sh 'grep -E'

# --- Monitoring validate hardening ------------------------------------------
require "monitoring validate umask 077" monitoring/validate.sh 'umask 077'
require "monitoring validate private temp dir" monitoring/validate.sh 'mktemp -d'
require "monitoring validate exit trap" monitoring/validate.sh "trap 'cleanup' 0 1 2 15"
require "monitoring validate forces dummy password" monitoring/validate.sh 'export ALERT_SMTP_PASSWORD="dummy"'

# --- Preflight (fail early on missing tools/options) ------------------------
require "backup preflight validates required tools" ops/backup-ecosologic.sh 'required tool '
require "backup preflight function wired" ops/backup-ecosologic.sh 'preflight'
require "backup preflight validates realpath -m" ops/backup-ecosologic.sh 'realpath does not support'
require "backup preflight validates Compose v2 plugin" ops/backup-ecosologic.sh 'docker compose version'
require "backup preflight validates GNU find" ops/backup-ecosologic.sh 'GNU find required'
require "restore-check preflight validates required tools" ops/restore-check-ecosologic.sh 'required tool '
require "restore-check preflight function wired" ops/restore-check-ecosologic.sh 'preflight'
require "restore-check preflight validates stat -c" ops/restore-check-ecosologic.sh 'GNU stat required'
require "restore-check preflight validates cp --reflink=never" ops/restore-check-ecosologic.sh 'GNU coreutils cp required'
require "restore-check preflight validates date -d" ops/restore-check-ecosologic.sh 'GNU date required'
require "restore-check preflight validates GNU find" ops/restore-check-ecosologic.sh 'GNU find required'

# --- Systemd units ----------------------------------------------------------
for unit in \
    ops/systemd/ecosologic-backup.service \
    ops/systemd/ecosologic-backup.timer \
    ops/systemd/ecosologic-backup-failure.service; do
    if [ ! -f "$unit" ]; then
        echo "ERROR: missing systemd unit $unit" >&2
        fail=1
    fi
done

require "backup service invokes via /bin/sh (no exec bit)" ops/systemd/ecosologic-backup.service 'ExecStart=/bin/sh '
require "backup service OnFailure wiring" ops/systemd/ecosologic-backup.service 'OnFailure=ecosologic-backup-failure.service'
require "failure unit records identifiable marker" ops/systemd/ecosologic-backup-failure.service 'ECOSOLOGIC_BACKUP_FAILED'
require "failure unit logs via logger" ops/systemd/ecosologic-backup-failure.service 'logger'
require "timer has OnCalendar schedule" ops/systemd/ecosologic-backup.timer 'OnCalendar='

# --- Docs wiring ------------------------------------------------------------
for doc in ops/production.md; do
    require "docs mention systemctl status" "$doc" 'systemctl status'
    require "docs mention journalctl" "$doc" 'journalctl'
done

if [ "$fail" -ne 0 ]; then
    echo "Backup script validation failed." >&2
    exit 1
fi
echo "Backup script validation passed."
