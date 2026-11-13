#!/usr/bin/env bash
# Restores a dump produced by backup.sh into a SCRATCH database (ekt_restore_test) to prove the backup works.
# It never touches the live database. To restore for real, follow "Khôi phục" in docs/deploy/VPS.md.
set -euo pipefail
cd "$(dirname "$0")/../.."
dump="${1:?Usage: tools/deploy/restore.sh backups/ekt-<time>.dump}"
test -s "$dump" || { echo "Dump not found or empty: $dump" >&2; exit 1; }
COMPOSE=(docker compose -f compose.yaml -f compose.https.yaml)
"${COMPOSE[@]}" exec -T db psql -U education_keeptrack -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS ekt_restore_test" -c "CREATE DATABASE ekt_restore_test"
"${COMPOSE[@]}" exec -T db pg_restore -U education_keeptrack -d ekt_restore_test --no-owner --exit-on-error < "$dump"
"${COMPOSE[@]}" exec -T db psql -U education_keeptrack -d ekt_restore_test -tAc \
  "select 'identity accounts: ' || count(*) from identity.\"AspNetUsers\"" || true
"${COMPOSE[@]}" exec -T db psql -U education_keeptrack -d postgres -c "DROP DATABASE ekt_restore_test"
echo "Restore rehearsal OK: $dump"
