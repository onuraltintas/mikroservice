#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/report-localization-20261016-r2
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
base=/var/lib/eduivme/releases/report-localization-20261016
tag=report-localization-20261016-r2
staff=eduivme-production-staff-portal-1
student=eduivme-production-speed-reading-frontend-1
api=eduivme-production-speed-reading-service-1
revision=$(<source-revision.txt); [[ "$revision" =~ ^[a-f0-9]{40}$ ]]
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
waitStatic() {
  local target=$1 baseHref=$2 ip html script
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$target" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && html=$(curl -fsS --max-time 5 "http://$ip/"); then
      grep -qF "<base href=\"$baseHref\">" <<< "$html" || return 1
      script=$(grep -oE 'src="main-[^"]+\.js"' <<< "$html" | cut -d'"' -f2)
      [[ "$script" =~ ^main-[A-Za-z0-9_-]+\.js$ ]] || return 1
      if [[ $(curl -fsS --max-time 5 -o /dev/null -w '%{http_code}' "http://$ip/$script") == 200 ]]; then
        printf 'static_ready target=%s base=%s html=passed main_js=200\n' "$target" "$baseHref"
        return 0
      fi
    fi
    sleep 2
  done
  return 1
}
unchanged() {
  local name id
  while read -r name id; do
    [[ "$name" == "$staff" || "$name" == "$student" ]] && continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < previous-containers.txt
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload
    [[ $(docker inspect "$api" --format '{{.Image}}') == "$(</var/lib/eduivme/releases/no-measurement-reports-20261016/built-image.txt)" ]]
    [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<"$base/built-staff-image.txt")" ]]
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<"$base/built-student-image.txt")" ]]
    docker ps --filter label=com.docker.compose.project=eduivme-production --format '{{.Names}}' | while read -r name; do docker inspect "$name" --format '{{.Name}} {{.Id}}' | sed 's|^/||'; done > previous-containers.txt
    for target in staff student; do
      name=${!target}
      docker inspect "$name" --format '{{.Image}}' > "previous-$target-image.txt"
      docker inspect "$name" --format '{{.Id}}' > "previous-$target-container.txt"
      docker inspect "$name" | jq '.[0].Config.Env | map(capture("^(?<key>[^=]+)=(?<value>.*)$")) | from_entries' > "$target.environment.json"
    done
    jq --arg staff "$(<previous-staff-image.txt)" --arg student "$(<previous-student-image.txt)" '.services["staff-portal"].image=$staff | .services["speed-reading-frontend"].image=$student' "$base/production.compose.json" > rollback.compose.json
    "${compose[@]}" -f rollback.compose.json config --format json > rollback.resolved.json
    for target in staff student; do
      service=staff-portal; [[ "$target" != student ]] || service=speed-reading-frontend
      jq -S --arg service "$service" '.services[$service].environment' rollback.resolved.json > expected.environment.json
      jq -S . "$target.environment.json" > current.environment.json
      cmp expected.environment.json current.environment.json
    done
    jq --arg tag "$tag" '.services["staff-portal"].image=("eduivme/staff-portal:"+$tag) | .services["speed-reading-frontend"].image=("eduivme/speed-reading-frontend:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload
    for target in staff student; do
      name=${!target}
      [[ $(docker inspect "$name" --format '{{.Id}}') == "$(<"previous-$target-container.txt")" ]]
      image=staff-portal; dockerfile=staff.Dockerfile
      if [[ "$target" == student ]]; then image=speed-reading-frontend; dockerfile=frontend.Dockerfile; fi
      docker image tag "$(<"previous-$target-image.txt")" "eduivme/$image:$tag-base"
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$image:$tag-base" --build-arg "SOURCE_REVISION=$revision" -f "$dockerfile" -t "eduivme/$image:$tag" . > "$target-build.log" 2>&1
      [[ $(docker image inspect "eduivme/$image:$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$revision" ]]
      docker image inspect "eduivme/$image:$tag" --format '{{.Id}}' > "built-$target-image.txt"
    done
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  preview)
    test -s built.txt; test ! -e deployed.txt; verifyPayload
    for target in staff student; do
      preview="$tag-$target-preview"
      service=staff-portal; baseHref=/staff/
      if [[ "$target" == student ]]; then service=speed-reading-frontend; baseHref=/; fi
      ! docker inspect "$preview" >/dev/null 2>&1
      trap 'docker rm -f "$preview" >/dev/null 2>&1 || true' EXIT
      "${compose[@]}" -f production.compose.json run -d --no-deps --pull never --name "$preview" "$service" > "$target-preview.log"
      waitStatic "$preview" "$baseHref"
      docker rm -f "$preview" >/dev/null; trap - EXIT
    done
    unchanged; date -u +%FT%TZ > preview-verified.txt
    ;;
  promote)
    test -s built.txt; test -s preview-verified.txt; test ! -e deployed.txt; verifyPayload
    [[ $(docker inspect "$staff" --format '{{.Id}}') == "$(<previous-staff-container.txt)" ]]
    [[ $(docker inspect "$student" --format '{{.Id}}') == "$(<previous-student-container.txt)" ]]
    [[ $(docker image inspect "eduivme/staff-portal:$tag" --format '{{.Id}}') == "$(<built-staff-image.txt)" ]]
    [[ $(docker image inspect "eduivme/speed-reading-frontend:$tag" --format '{{.Id}}') == "$(<built-student-image.txt)" ]]
    unchanged
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never staff-portal speed-reading-frontend > rollback.log 2>&1 || exit 70
        [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<previous-staff-image.txt)" ]] || exit 70
        [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<previous-student-image.txt)" ]] || exit 70
        waitStatic "$staff" /staff/ || exit 70
        waitStatic "$student" / || exit 70
        echo 'Previous staff and student images restored' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never staff-portal > promotion.log 2>&1
    [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<built-staff-image.txt)" ]]
    waitStatic "$staff" /staff/
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never speed-reading-frontend >> promotion.log 2>&1
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-student-image.txt)" ]]
    waitStatic "$student" /
    unchanged; date -u +%FT%TZ > deployed.txt
    ;;
  validate)
    test -s deployed.txt
    [[ $(docker inspect "$staff" --format '{{.Image}}') == "$(<built-staff-image.txt)" ]]
    [[ $(docker inspect "$student" --format '{{.Image}}') == "$(<built-student-image.txt)" ]]
    waitStatic "$staff" /staff/; waitStatic "$student" /; unchanged
    docker inspect "$api" "$staff" "$student" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'
    ;;
  *) exit 2 ;;
esac
