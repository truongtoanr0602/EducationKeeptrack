#!/usr/bin/env bash
set +x
set -euo pipefail
umask 077

: "${R2CI_DIR:?Set a fresh private run directory}"
: "${POSTGRES_PASSWORD:?Jenkins PostgreSQL test credential required}"
: "${DEMO_PASSWORD:?Demo test credential required}"
root="$(pwd)"
run="$root/$R2CI_DIR"
tool="$root/tools/R2Ci/bin/Release/net10.0/R2Ci.dll"
host="$root/src/Host/bin/Release/net10.0/Host.dll"
mkdir -p "$run"
# CreateNew refuses reusing a state/report directory from an older build.
dotnet "$tool" init "$run/state.json"
export TEST_S3_RUN_PREFIX
TEST_S3_RUN_PREFIX="$(dotnet "$tool" prefix "$run/state.json")"
docker compose -f compose.ci.yaml up -d --wait db > "$run/database.log" 2>&1
port="$(docker compose -f compose.ci.yaml port db 5432)"
port="${port##*:}"
export TEST_DATABASE_CONNECTION="Host=127.0.0.1;Port=$port;Database=postgres;Username=ekt_ci;Password=$POSTGRES_PASSWORD"
if ! dotnet test tests/ApiTests -c Release --no-build --results-directory "$run" --logger 'trx;LogFileName=api.trx' > "$run/api.log" 2>&1; then
  echo 'API integration failed; raw diagnostics remain private on the agent.'
  exit 1
fi
export ConnectionStrings__Database="$TEST_DATABASE_CONNECTION"
export ASPNETCORE_ENVIRONMENT=Development
export Logging__LogLevel__Default=Error
export Reporting__Endpoint="$TEST_S3_ENDPOINT" Reporting__Bucket="$TEST_S3_BUCKET"
export Reporting__AccessKey="$TEST_S3_ACCESS_KEY" Reporting__SecretKey="$TEST_S3_SECRET_KEY"
export Reporting__Region=auto Reporting__KeyPrefix="${TEST_S3_RUN_PREFIX}browser/"
export DEMO_EMAIL=ci-teacher@example.test
export Provision__Email="$DEMO_EMAIL" Provision__Password="$DEMO_PASSWORD"
export Provision__Until
Provision__Until="$(date -u -d '+1 day' +%Y-%m-%dT%H:%M:%SZ)"
dotnet "$host" --migrate > "$run/migrate.log" 2>&1
dotnet "$host" --provision-teacher > "$run/provision.log" 2>&1
export REPORT_EMAIL=ci-report-teacher@example.test
export Provision__Email="$REPORT_EMAIL"
dotnet "$host" --provision-teacher > "$run/provision-report.log" 2>&1
dotnet "$host" --urls http://127.0.0.1:5080 > "$run/backend.log" 2>&1 &
backend_pid=$!
stop_backend() {
  kill "$backend_pid" 2>/dev/null || true
  wait "$backend_pid" 2>/dev/null || true
}
trap stop_backend EXIT
ready=false
for ((attempt=0; attempt<60; attempt++)); do
  kill -0 "$backend_pid" 2>/dev/null || break
  if curl --fail --silent --output /dev/null http://127.0.0.1:5080/api/auth/csrf; then ready=true; break; fi
  sleep 1
done
if [[ "$ready" != true ]]; then echo 'Browser backend did not become ready.'; exit 1; fi
export CI=true PLAYWRIGHT_JSON_OUTPUT_FILE="$run/browser.json"
unset E2E_BASE_URL
export API_PROXY_TARGET=http://127.0.0.1:5080
cd ui
if ! npx playwright test --workers=1 > "$run/browser.log" 2>&1; then
  echo 'Browser integration failed; raw diagnostics remain private on the agent.'
  exit 1
fi
echo 'API and browser commands completed; post-build cleanup and evidence gate must still pass.'
