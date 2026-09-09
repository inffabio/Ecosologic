#!/bin/sh
# Alertmanager entrypoint: expands ${VAR} placeholders in the config file from
# the container environment before starting alertmanager.
#
# Alertmanager has no native `--config.expand-env` flag (that is a Prometheus
# flag), so this wrapper reproduces the same behaviour for Alertmanager. All
# SMTP credentials stay in the `.env` on the server and are never stored in the
# repository.
set -eu

TEMPLATE=/etc/alertmanager/alertmanager.yml
GENERATED=/tmp/alertmanager.generated.yml

umask 077

: "${ALERT_SMTP_HOST:?ALERT_SMTP_HOST is required}"
: "${ALERT_SMTP_PORT:?ALERT_SMTP_PORT is required}"
: "${ALERT_SMTP_FROM:?ALERT_SMTP_FROM is required}"
: "${ALERT_SMTP_TO:?ALERT_SMTP_TO is required}"
: "${ALERT_SMTP_USERNAME:?ALERT_SMTP_USERNAME is required}"
: "${ALERT_SMTP_PASSWORD:?ALERT_SMTP_PASSWORD is required}"

awk '{
  line = $0
  while ((start = index(line, "${")) > 0) {
    end = index(substr(line, start), "}")
    if (end == 0) break
    var = substr(line, start + 2, end - 3)
    line = substr(line, 1, start - 1) ENVIRON[var] substr(line, start + end)
  }
  print line
}' "$TEMPLATE" > "$GENERATED"

exec /bin/alertmanager "$@"
