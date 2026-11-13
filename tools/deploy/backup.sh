#!/usr/bin/env bash
# Dumps the PostgreSQL database to ./backups and removes dumps older than KEEP_DAYS (default 14).
# Run from the repository root on the VPS. Safe to run from cron. Copy the dumps offsite as well (see docs/deploy/VPS.md).
set -euo pipefail
cd "$(dirname "$0")/../.."
umask 077
mkdir -p backups
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
file="backups/ekt-${stamp}.dump"
COMPOSE=(docker compose -f compose.yaml -f compose.https.yaml)
# -Fc: compressed custom format, restorable with pg_restore, including a single schema or table if ever needed.
"${COMPOSE[@]}" exec -T db pg_dump -U education_keeptrack -d education_keeptrack -Fc > "${file}.partial"
# A truncated dump must never look valid: it only gets its final name after pg_restore can read its table of contents.
"${COMPOSE[@]}" exec -T db pg_restore --list < "${file}.partial" > /dev/null
mv "${file}.partial" "${file}"
find backups -name 'ekt-*.dump' -mtime +"${KEEP_DAYS:-14}" -delete
echo "Backup written: ${file} ($(du -h "${file}" | cut -f1))"
