#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/multi-institution-staff-origin-20261014
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
parent=/var/lib/eduivme/releases/multi-institution-enrollment-20261014
container=eduivme-production-staff-portal-1
tag=multi-institution-enrollment-20261014-r2
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
revision=$(<source-revision.txt)
[[ "$revision" =~ ^[a-f0-9]{40}$ ]]
health() {
 local ip html script
 for attempt in $(seq 1 30); do
  ip=$(docker inspect "$1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]] && html=$(curl -fsS --max-time 5 "http://$ip:80/"); then
   grep -qF '<base href="/staff/">' <<< "$html" || return 1
   script=$(grep -oE 'src="main-[^"]+\.js"' <<< "$html" | cut -d'"' -f2)
   [[ "$script" =~ ^main-[A-Za-z0-9_-]+\.js$ ]] || return 1
   curl -fsS --max-time 5 "http://$ip:80/$script" >/dev/null && return 0
  fi
  sleep 2
 done
 return 1
}
unchanged() {
 while read -r name id; do
  [[ "$name" == "$container" ]] && continue
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || return 1
 done < previous-containers.txt
}
case "${1:-validate}" in
 prepare)
  test ! -e prepared.txt; test -s "$parent/staff-rollback-verified.txt"
  sha256sum -c payload.sha256 > payload-verified.log
  [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<"$parent/previous-staff-portal-image.txt")" ]]
  docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' |
   while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
  docker inspect "$container" --format '{{.Image}}' > previous-staff-image.txt
  docker inspect "$container" --format '{{.Id}}' > previous-staff-container.txt
  # The other three services stay at the already verified multi-institution release.
  jq --arg image "$(<previous-staff-image.txt)" '.services["staff-portal"].image=$image' "$parent/production.compose.json" > rollback.compose.json
  jq --arg tag "$tag" '.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  docker image tag "$(<previous-staff-image.txt)" "eduivme/staff-portal:$tag-base"
  docker build --network none --build-arg "BASE_IMAGE=eduivme/staff-portal:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f staff.Dockerfile -t "eduivme/staff-portal:$tag" . > build.log 2>&1
  preview=multi-institution-staff-origin-preview-20261014
  docker inspect "$preview" >/dev/null 2>&1 && exit 2
  trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
  "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" staff-portal > preview.log
  health "$preview"; unchanged
  date -u +%FT%TZ > prepared.txt
  ;;
 promote)
  test -s prepared.txt; test ! -e deployed.txt
  [[ $(docker inspect "$container" --format '{{.Id}}') == "$(<previous-staff-container.txt)" ]]
  unchanged
  rollback() {
   local status=$?; trap - EXIT
   if ((status != 0)); then
    "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never staff-portal > rollback.log 2>&1 || exit 70
    health "$container" || exit 70
    [[ $(docker inspect "$container" --format '{{.Image}}') == "$(<previous-staff-image.txt)" ]] || exit 70
   fi
   exit "$status"
  }
  trap rollback EXIT
  "${compose[@]}" -f production.compose.json up -d --no-deps --pull never staff-portal > promotion.log 2>&1
  health "$container"; unchanged
  curl -fsS --max-time 15 https://onuraltintas.net/staff/ | grep -qF '<base href="/staff/">'
  date -u +%FT%TZ > deployed.txt
  ;;
 validate)
  test -s deployed.txt; health "$container"; unchanged
  [[ $(docker inspect "$container" --format '{{.Image}}') == $(docker image inspect "eduivme/staff-portal:$tag" --format '{{.Id}}') ]]
  docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
  docker inspect "$container" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
  cat deployed.txt
  ;;
 *) exit 2 ;;
esac
