#!/bin/sh
# Validates the monitoring stack configuration without touching a running
# deployment. Run from the repository root (or anywhere; the script resolves
# its own location).
#
# Requires: docker (with the compose plugin). It reuses the same pinned images
# as docker-compose.monitoring.yml, so promtool/amtool do not need to be
# installed on the host.
#
# Security: dummy values are FORCED for every secret, overriding any real values
# in the caller's environment or the project `.env` (so `docker compose config`
# can never leak production secrets). A private temporary directory (mode 700,
# removed on exit) holds the env-expanded Alertmanager config; no
# credential-bearing file is ever written to a shared /tmp location.
set -eu

umask 077

cd "$(dirname "$0")/.."

# Force dummy values only — never real secrets, even if the caller's environment
# or the project `.env` happens to export production secrets.
export ALERT_SMTP_HOST="smtp.example.com"
export ALERT_SMTP_PORT="587"
export ALERT_SMTP_FROM="alerts@example.com"
export ALERT_SMTP_TO="ops@example.com"
export ALERT_SMTP_USERNAME="dummy"
export ALERT_SMTP_PASSWORD="dummy"
export GRAFANA_ADMIN_PASSWORD="dummy"
export POSTGRES_USER="postgres"
export POSTGRES_PASSWORD="dummy"
export POSTGRES_DB="ecosologic"
export AUTH_JWT_KEY="dummy"
export AUTH_ADMIN_EMAIL="admin@example.com"
export AUTH_ADMIN_PASSWORD="dummy"

# Private temporary directory for the env-expanded Alertmanager config. The trap
# below removes it on exit (including on error and on signals).
TMPDIR_PRIV=$(mktemp -d "${TMPDIR:-/tmp}/ecosologic-validate.XXXXXX") || {
    echo "ERROR: cannot create private temporary directory" >&2
    exit 1
}
chmod 700 "$TMPDIR_PRIV"
cleanup() {
    rm -rf -- "$TMPDIR_PRIV"
}
trap 'cleanup' 0 1 2 15

EXPANDED="$TMPDIR_PRIV/alertmanager.expanded.yml"

echo "[1/4] docker compose config"
docker compose -f monitoring/docker-compose.monitoring.yml config --quiet

echo "[2/4] promtool check config"
docker run --rm -v "$PWD/monitoring:/etc/prometheus:ro" prom/prometheus:v3.5.0 \
  promtool check config /etc/prometheus/prometheus.yml

echo "[3/4] promtool check rules"
docker run --rm -v "$PWD/monitoring:/etc/prometheus:ro" prom/prometheus:v3.5.0 \
  promtool check rules /etc/prometheus/alerts.yml

echo "[4/4] amtool check-config (env-expanded)"
awk '{
  line = $0
  while ((start = index(line, "${")) > 0) {
    end = index(substr(line, start), "}")
    if (end == 0) break
    var = substr(line, start + 2, end - 3)
    line = substr(line, 1, start - 1) ENVIRON[var] substr(line, start + end)
  }
  print line
}' monitoring/alertmanager.yml > "$EXPANDED"
docker run --rm -v "$EXPANDED:/etc/alertmanager/alertmanager.yml:ro" \
  prom/alertmanager:v0.34.0 amtool check-config /etc/alertmanager/alertmanager.yml

echo "All monitoring config validations passed."
