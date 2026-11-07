#!/usr/bin/env bash
# Starts an ISOLATED Compose project (own containers, volumes and ports), creates an administrator and runs the Playwright
# suite against it. Used by CI (job "compose-e2e") and for local reliability runs, so both exercise the same procedure.
#
#   deploy/scripts/e2e-stack.sh [project-name]        default project: cicek-e2e (ports 1622 / 5180 / 3100)
#
# Environment (all optional):
#   E2E_ENV_FILE       path of the generated settings file. If it exists it is reused, so repeated runs against the same
#                      volumes keep matching database passwords. Created with mode 600; never print or commit it.
#   E2E_SKIP_UP=1      skip `compose up` (stack already running; used for restart/cold-start experiments)
#   E2E_PLAYWRIGHT_ARGS  extra arguments for `playwright test`
#   ORACLE_IMAGE / ORACLE_PDB   test another Oracle edition (see deploy/.env.example)
#
# It never runs `down -v` and never touches another Compose project. Remove an isolated project yourself when finished.
set -euo pipefail

PROJECT="${1:-cicek-e2e}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT/deploy/docker-compose.yml"
DB_PORT="${ORACLE_HOST_PORT:-1622}"
API_PORT="${API_HOST_PORT:-5180}"
WEB_PORT="${STOREFRONT_HOST_PORT:-3100}"
ENV_FILE="${E2E_ENV_FILE:-}"

if [[ -z "$ENV_FILE" ]]; then
  ENV_FILE="$(mktemp)"
  trap 'rm -f "$ENV_FILE"' EXIT
fi

rand() { openssl rand -hex "$(( ($1 + 1) / 2 ))" | cut -c1-"$1"; }  # hex: letters+digits only, no SIGPIPE under pipefail
mask() { if [[ -n "${GITHUB_ACTIONS:-}" ]]; then echo "::add-mask::$1"; fi; }

if [[ ! -s "$ENV_FILE" ]]; then
  umask 077
  admin_pw="Ca$(rand 22)_9"; owner_pw="Cb$(rand 22)_9"; app_pw="Cc$(rand 22)_9"
  jwt="$(rand 48)"; e2e_admin_pw="Adm-$(rand 20)"
  for v in "$admin_pw" "$owner_pw" "$app_pw" "$jwt" "$e2e_admin_pw"; do mask "$v"; done
  cat > "$ENV_FILE" <<EOF
ORACLE_ADMIN_PASSWORD=$admin_pw
CICEK_OWNER_PASSWORD=$owner_pw
CICEK_APP_PASSWORD=$app_pw
JWT_SIGNING_KEY=$jwt
E2E_ADMIN_PASSWORD=$e2e_admin_pw
AUTH_RATE_LIMIT=1000
ORACLE_HOST_PORT=$DB_PORT
API_HOST_PORT=$API_PORT
STOREFRONT_HOST_PORT=$WEB_PORT
APP_ORIGIN=http://localhost:$WEB_PORT
EOF
  [[ -n "${ORACLE_PDB:-}" ]] && echo "ORACLE_PDB=$ORACLE_PDB" >> "$ENV_FILE"
  [[ -n "${ORACLE_IMAGE:-}" ]] && echo "ORACLE_IMAGE=$ORACLE_IMAGE" >> "$ENV_FILE"
else
  while IFS='=' read -r key value; do
    [[ "$key" =~ PASSWORD|KEY ]] && mask "$value"
  done < "$ENV_FILE"
fi

compose() { docker compose -p "$PROJECT" -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"; }
setting() { grep -E "^$1=" "$ENV_FILE" | head -1 | cut -d= -f2-; }

dump_logs() {
  echo "--- compose status"; compose ps -a || true
  echo "--- last log lines per service (secrets are never logged by the services)"
  compose logs --no-color --tail 60 || true
}

if [[ -z "${E2E_SKIP_UP:-}" ]]; then
  echo "Starting isolated stack '$PROJECT' (ports: db $DB_PORT, api $API_PORT, web $WEB_PORT)"
  # --wait gates on the real health checks (database open + schema owner exists, API /health/ready, storefront HTTP).
  compose up -d --build --wait --wait-timeout 1200 || { dump_logs; exit 1; }
fi

# Create the administrator the end-to-end tests sign in with (idempotent).
compose run --rm -e CICEK_ADMIN_PASSWORD="$(setting E2E_ADMIN_PASSWORD)" migrator create-admin --email e2e-admin@example.com \
  > /dev/null || { dump_logs; exit 1; }

cd "$ROOT/frontend/apps/storefront"
export E2E_BASE_URL="http://localhost:$WEB_PORT"
export E2E_FULL_STACK=1
export E2E_ADMIN_EMAIL=e2e-admin@example.com
E2E_ADMIN_PASSWORD="$(setting E2E_ADMIN_PASSWORD)"
export E2E_ADMIN_PASSWORD

# shellcheck disable=SC2086
if ! npx playwright test ${E2E_PLAYWRIGHT_ARGS:-}; then
  dump_logs
  exit 1
fi
