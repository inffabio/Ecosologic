#!/bin/sh
# Shell tests for the restore-check cleanup classification.
#
# Exercises the exact `inspect_state` and `remove_check_container` functions from
# ops/restore-check-ecosologic.sh against a controlled `docker` stub (no real
# Docker daemon is required). In particular it guards the regression where a
# successful `docker rm` was followed by `docker inspect` exiting 1 with NO
# output, which used to be misclassified as an unknown state and logged a
# spurious "docker inspect failed after removal" error even though the container
# was gone.
#
# Run from anywhere:
#   sh ops/test-restore-check.sh

set -u

REPO="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
SCRIPT="$REPO/ops/restore-check-ecosologic.sh"

[ -f "$SCRIPT" ] || { echo "FAIL: missing $SCRIPT" >&2; exit 1; }

pass=0
fail=0

ok() {
    pass=$((pass + 1))
    echo "ok: $1"
}

bad() {
    fail=$((fail + 1))
    echo "FAIL: $1" >&2
}

# Extract a single function definition (its `}` is the only unindented brace).
extract_fn() {
    sed -n "/^$1() {/,/^}/p" "$SCRIPT"
}

# --- Build the controlled docker stub ---------------------------------------
TMP="$(mktemp -d "${TMPDIR:-/tmp}/restore-check-test.XXXXXX")" \
    || { echo "FAIL: cannot create temp dir" >&2; exit 1; }
trap 'rm -rf "$TMP"' 0 1 2 15

BIN="$TMP/bin"
mkdir -p "$BIN"
STATE="$TMP/state"
mkdir -p "$STATE"

cat > "$BIN/docker" <<'EOF'
#!/bin/sh
# Controlled docker stub. Behavior is keyed on the object/container name, with a
# per-run state directory supplied via $STATE (for the remove-then-vanish case).
cmd="$1"
if [ "$cmd" = "inspect" ]; then
    shift
    template=""
    if [ "$1" = "-f" ]; then
        template="$2"
        shift 2
    fi
    name="$1"
    case "$name" in
        exists)
            echo '[{"Id":"exists","Config":{"Labels":{"ecosologic.restore-check":"ecosologic-restore-check-TEST"}}}]'
            exit 0
            ;;
        exists-then-vanish)
            if [ -e "$STATE/removed" ]; then
                exit 1
            fi
            if [ -n "$template" ]; then
                echo 'ecosologic-restore-check-TEST'
            else
                echo '[{"Id":"exists-then-vanish","Config":{"Labels":{"ecosologic.restore-check":"ecosologic-restore-check-TEST"}}}]'
            fi
            exit 0
            ;;
        absent-msg)
            echo 'Error: No such object: absent-msg' >&2
            exit 1
            ;;
        absent-container-msg)
            echo 'Error response from daemon: No such container: absent-container-msg' >&2
            exit 1
            ;;
        absent-lowercase)
            echo 'error: no such object: absent-lowercase' >&2
            exit 1
            ;;
        absent-empty)
            exit 1
            ;;
        daemon-down)
            echo 'Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?' >&2
            exit 1
            ;;
        command-error)
            echo 'docker: unknown command' >&2
            exit 125
            ;;
        *)
            echo "stub: no such scenario: $name" >&2
            exit 2
            ;;
    esac
elif [ "$cmd" = "rm" ]; then
    shift 2  # rm -f
    name="$1"
    case "$name" in
        exists-then-vanish)
            : > "$STATE/removed"
            exit 0
            ;;
        *)
            echo "Error: No such object: $name" >&2
            exit 1
            ;;
    esac
else
    echo "stub: unexpected command: $cmd" >&2
    exit 2
fi
EOF
chmod 700 "$BIN/docker"

export PATH="$BIN:$PATH"
export STATE

# --- Load the functions under test (isolated; the main body is never run) ---
for fn in inspect_state remove_check_container; do
    def="$(extract_fn "$fn")"
    if [ -z "$def" ]; then
        echo "FAIL: could not extract $fn from $SCRIPT" >&2
        exit 1
    fi
    eval "$def"
done

# --- inspect_state unit tests -----------------------------------------------
check_classify() {
    name="$1"
    expected="$2"
    inspect_state "$name"
    got=$?
    if [ "$got" = "$expected" ]; then
        ok "inspect_state '$name' -> $got"
    else
        bad "inspect_state '$name' -> $got (expected $expected)"
    fi
}

check_classify exists 0
check_classify absent-msg 1
check_classify absent-container-msg 1
check_classify absent-lowercase 1
check_classify absent-empty 1
check_classify daemon-down 2
check_classify command-error 2

# --- remove_check_container regression tests --------------------------------
RESTORE_CHECK_TAG="ecosologic-restore-check-TEST"

# exists-then-vanish: present at first inspect (label matches), `docker rm -f`
# succeeds, then the follow-up inspect exits 1 with EMPTY output. This must be
# classified absent, never as "docker inspect failed after removal".
rm -f "$STATE/removed"
CNAME="exists-then-vanish"
CONTAINER_LEAKED=0
remove_check_container > "$TMP/out" 2>&1
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$CNAME" ] && [ "$CONTAINER_LEAKED" -eq 0 ]; then
    ok "remove_check_container: empty-output-after-rm -> absent (no leak)"
else
    bad "remove_check_container: exists-then-vanish rc=$rc CNAME=$CNAME leaked=$CONTAINER_LEAKED out=$(cat "$TMP/out")"
fi

# absent-msg: already gone before removal; must clear the name and succeed.
CNAME="absent-msg"
CONTAINER_LEAKED=0
remove_check_container > "$TMP/out" 2>&1
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$CNAME" ] && [ "$CONTAINER_LEAKED" -eq 0 ]; then
    ok "remove_check_container: 'No such object' before removal -> absent"
else
    bad "remove_check_container: absent-msg rc=$rc CNAME=$CNAME leaked=$CONTAINER_LEAKED out=$(cat "$TMP/out")"
fi

# absent-empty: already gone, exit 1 with empty output; must clear and succeed.
CNAME="absent-empty"
CONTAINER_LEAKED=0
remove_check_container > "$TMP/out" 2>&1
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$CNAME" ] && [ "$CONTAINER_LEAKED" -eq 0 ]; then
    ok "remove_check_container: empty-output before removal -> absent"
else
    bad "remove_check_container: absent-empty rc=$rc CNAME=$CNAME leaked=$CONTAINER_LEAKED out=$(cat "$TMP/out")"
fi

# absent-lowercase: already gone before removal, lowercase docker message
# "error: no such object"; must clear the name and succeed (case-insensitive
# match, never misclassified as an unknown daemon/command state).
CNAME="absent-lowercase"
CONTAINER_LEAKED=0
remove_check_container > "$TMP/out" 2>&1
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$CNAME" ] && [ "$CONTAINER_LEAKED" -eq 0 ]; then
    ok "remove_check_container: lowercase 'no such object' before removal -> absent"
else
    bad "remove_check_container: absent-lowercase rc=$rc CNAME=$CNAME leaked=$CONTAINER_LEAKED out=$(cat "$TMP/out")"
fi

# daemon-down: unknown state must leak and fail (never silent success).
CNAME="daemon-down"
CONTAINER_LEAKED=0
remove_check_container > "$TMP/out" 2>&1
rc=$?
if [ "$rc" -ne 0 ] && [ "$CONTAINER_LEAKED" -eq 1 ] && [ "$CNAME" = "daemon-down" ]; then
    ok "remove_check_container: daemon error -> unknown (leaked, non-zero)"
else
    bad "remove_check_container: daemon-down rc=$rc CNAME=$CNAME leaked=$CONTAINER_LEAKED out=$(cat "$TMP/out")"
fi

echo ""
echo "restore-check tests: $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
