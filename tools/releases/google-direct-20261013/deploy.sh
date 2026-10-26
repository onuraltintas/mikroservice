#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/google-direct-20261013
[[ "$PWD" == "$release" ]] || exit 2
exec 9>/var/lock/eduivme-speed-reading-deploy.lock
flock -n 9 || exit 2
frontend=eduivme-production-speed-reading-frontend-1
identity=eduivme-production-identity-service-1
marker=google-direct-20261013
export TEEN_API_IMAGE
TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
base=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
declare -A seen=()
IFS=',' read -r -a files <<< "$(docker inspect "$frontend" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')"
for file in "${files[@]}"; do
  test -f "$file"
  if [[ -z "${seen[$file]:-}" ]]; then base+=(-f "$file"); seen[$file]=1; fi
done
newCompose=("${base[@]}" -f "$release/production.override.yml")

waitHealthy() {
  local attempt ip
  for attempt in $(seq 1 30); do
    ip=$(docker inspect "$identity" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ $(docker inspect "$frontend" --format '{{.State.Health.Status}}' 2>/dev/null || true) == healthy ]] && \
      [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 5 "http://$ip:8080/health/live" >/dev/null; then
      return 0
    fi
    sleep 2
  done
  return 1
}

checkPublic() {
  local attempt code
  for attempt in $(seq 1 12); do
    code=$(curl -sS --max-time 15 -o /dev/null -w '%{http_code}' "https://masterhizliokuma.com/auth/login?release=$marker" || true)
    if [[ "$code" == 200 ]] && curl -fsS --max-time 15 \
      "https://masterhizliokuma.com/ngsw-worker.js?release=$marker" | grep -F "$marker" >/dev/null && \
      curl -fsS --max-time 15 https://masterhizliokuma.com/api/auth/captcha-config >/dev/null; then
      return 0
    fi
    sleep 5
  done
  return 1
}

case "${1:-validate}" in
  validate)
    sha256sum -c payload.sha256 >/dev/null
    "${newCompose[@]}" config --quiet
    echo 'Package and Compose validation passed.'
    ;;
  build)
    sha256sum -c payload.sha256 >/dev/null
    [[ $(docker inspect "$identity" --format '{{.Config.Image}}') == eduivme/identity-service:google-inline-20261001 ]]
    [[ $(docker inspect "$frontend" --format '{{.Config.Image}}') == eduivme/speed-reading-frontend:auth-timeout-20261013-319b250c ]]
    docker inspect "$identity" --format '{{.Image}}' > previous-identity-id.txt
    docker inspect "$frontend" --format '{{.Image}}' > previous-frontend-id.txt
    printf 'services:\n  identity-service:\n    image: eduivme/identity-service:google-inline-20261001\n    pull_policy: never\n  speed-reading-frontend:\n    image: eduivme/speed-reading-frontend:auth-timeout-20261013-319b250c\n    pull_policy: never\n' > previous.override.yml
    docker build --network=none -f Dockerfile.identity -t eduivme/identity-service:google-direct-20261013 .
    docker build --network=none -f Dockerfile.frontend -t eduivme/speed-reading-frontend:google-direct-20261013 .
    docker run --rm --network none -v "$release/probe-runtime:/probe:ro" --entrypoint dotnet \
      eduivme/identity-service:google-direct-20261013 /probe/Probe.dll /app
    date -u +%FT%TZ > built-at.txt
    ;;
  promote)
    test -s built-at.txt
    [[ $(docker inspect "$identity" --format '{{.Image}}') == "$(<previous-identity-id.txt)" ]]
    [[ $(docker inspect "$frontend" --format '{{.Image}}') == "$(<previous-frontend-id.txt)" ]]
    "${newCompose[@]}" config --quiet
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-before.txt
    rollbackCompose=("${base[@]}" -f "$release/previous.override.yml")
    rollbackEnabled=1
    onExit() {
      status=$?
      trap - EXIT
      if [[ "$status" != 0 && "$rollbackEnabled" == 1 ]]; then
        echo 'Release check failed; restoring previous Identity and frontend images.' >&2
        "${rollbackCompose[@]}" up -d --no-deps --pull never identity-service speed-reading-frontend || true
      fi
      exit "$status"
    }
    trap onExit EXIT
    "${newCompose[@]}" up -d --no-deps --pull never identity-service speed-reading-frontend
    waitHealthy
    checkPublic
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-after.txt
    diff -u <(grep -Ev "^($identity|$frontend) " containers-before.txt) <(grep -Ev "^($identity|$frontend) " containers-after.txt)
    rollbackEnabled=0
    date -u +%FT%TZ > deployed-at.txt
    trap - EXIT
    echo 'Google direct registration release passed live checks.'
    ;;
  rollback)
    rollbackCompose=("${base[@]}" -f "$release/previous.override.yml")
    "${rollbackCompose[@]}" up -d --no-deps --pull never identity-service speed-reading-frontend
    waitHealthy
    ;;
  *) exit 2 ;;
esac
