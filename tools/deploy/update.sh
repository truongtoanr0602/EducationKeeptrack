#!/usr/bin/env bash
# Updates the VPS to the latest master: backup first (migrations only go forward), then pull, rebuild and restart.
set -euo pipefail
cd "$(dirname "$0")/../.."
COMPOSE=(docker compose -f compose.yaml -f compose.https.yaml)
bash tools/deploy/backup.sh
git fetch --quiet origin
git pull --ff-only origin master
"${COMPOSE[@]}" up -d --build
sleep 5
"${COMPOSE[@]}" ps
url="$(grep -E "^PUBLIC_URL=" .env | cut -d= -f2-)"
code="$(curl -s -o /dev/null -w "%{http_code}" --max-time 15 "${url:-https://localhost}/api/auth/csrf" || true)"
echo "GET /api/auth/csrf -> ${code} (expect 200)"
test "$code" = "200"
