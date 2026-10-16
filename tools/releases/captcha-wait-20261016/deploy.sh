#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/captcha-wait-20261016
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
student=eduivme-production-speed-reading-frontend-1
image=eduivme/speed-reading-frontend:captcha-wait-20261016
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
ready() {
  local target=$1 ip html main
  for attempt in $(seq 1 30); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && html=$(curl -fsS --max-time 5 "http://$ip/"); then
      grep -qF '<base href="/">' <<< "$html" || return 1
      main=$(grep -oE 'src="main-[^"]+\.js"' <<< "$html" | cut -d'"' -f2)
      [[ "$main" =~ ^main-[A-Za-z0-9_-]+\.js$ ]] || return 1
      curl -fsS --max-time 5 "http://$ip/$main" -o /dev/null && return 0
    fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    [[ "$name" == "$student" ]] && continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || return 1
  done < previous-containers.txt
}
case "${1:-validate}" in
  deploy)
    test ! -e deployed.txt; sha256sum -c payload.sha256 > payload-verified.log
    revision=$(<source-revision.txt); [[ "$revision" =~ ^[a-f0-9]{40}$ ]]
    [[ $(docker inspect "$student" --format '{{.Image}}') == sha256:d369749656b6f4fc790054f4bb8200870bce6e6f271bdcac86fcfe5478cec564 ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    docker inspect "$student" --format '{{.Image}}' > previous-image.txt
    docker inspect "$student" --format '{{.Id}}' > previous-container.txt
    jq --arg image "$(<previous-image.txt)" '.services["speed-reading-frontend"].image=$image' /var/lib/eduivme/releases/reports-brand-editor-20261016/production.compose.json > rollback.compose.json
    jq --arg image "$image" '.services["speed-reading-frontend"].image=$image' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    base=eduivme/speed-reading-frontend:captcha-wait-20261016-base
    docker image tag "$(<previous-image.txt)" "$base"
    [[ $(docker image inspect "$base" --format '{{.Id}}') == "$(<previous-image.txt)" ]]
    docker build --network none --build-arg "BASE_IMAGE=$base" --build-arg "SOURCE_REVISION=$revision" -f frontend.Dockerfile -t "$image" . > build.log 2>&1
    docker image inspect "$image" --format '{{.Id}}' > built-image.txt
    [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
    preview=captcha-wait-20261016-preview
    ! docker inspect "$preview" >/dev/null 2>&1
    trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
    "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" speed-reading-frontend > preview.log
    ready "$preview"; docker rm -f "$preview" >/dev/null; trap - EXIT
    unchanged
    [[ $(docker inspect "$student" --format '{{.Id}}') == "$(<previous-container.txt)" ]]
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never speed-reading-frontend > rollback.log 2>&1 || exit 70
        ready "$student" || exit 70
        [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<previous-image.txt)" ]] || exit 70
      fi
      exit "$status"
    }; trap rollback EXIT
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never speed-reading-frontend > promote.log 2>&1
    ready "$student"; unchanged
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    curl -fsS --max-time 15 https://masterhizliokuma.com/auth/login -o /dev/null
    date -u +%FT%TZ > deployed.txt; trap - EXIT
    ;;
  validate)
    test -s deployed.txt; ready "$student"; unchanged
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-image.txt)" ]]
    curl -fsS --max-time 15 -o /dev/null -w 'public_status=%{http_code}\n' https://masterhizliokuma.com/auth/login
    date -u +%FT%TZ
    ;;
  *) exit 64 ;;
esac
