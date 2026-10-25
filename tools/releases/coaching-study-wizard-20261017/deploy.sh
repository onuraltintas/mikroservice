#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-study-wizard-20261017
cd "$release"; [[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-study-wizard-20261017
services=(coaching-service admin-panel)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
baseCompose=/var/lib/eduivme/releases/coaching-restore-profile-20261017/production.compose.json
expectedCoaching=sha256:961f4a68be3e0bc2c9d4689fdc0786aecd171a9367232e22032c076da1249022
expectedAdmin=sha256:a3e2e800f56eeb29c5aabf1f6031a23bdc83efaaefa4f8c25b53e841edc55ea6
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
baselineTargets() {
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}') == "$(<previous-coaching-service-container.txt)" ]]
  [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Id}}') == "$(<previous-admin-panel-container.txt)" ]]
}
unchanged() {
  local name id image
  while read -r name id image; do
    case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated service changed: $name" >&2; return 1; }
  done < baseline/containers.txt
}
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  docker inspect "$name" --format '{{.Name}} running={{.State.Running}} exit={{.State.ExitCode}} restarts={{.RestartCount}}' >&2; return 1
}
fingerprint() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL'
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5((to_jsonb(t)-%L::text[])::text),%L ORDER BY md5((to_jsonb(t)-%L::text[])::text)),%L)) FROM %I.%I t;',
table_schema||'.'||table_name,'{}','','{}','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name
\gexec
SQL
}
identityFingerprint() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
}
case "${1:-}" in
  prepare)
    test ! -e prepared.txt; verifyPayload; mkdir -p baseline; chmod 700 "$release"
    [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$expectedCoaching" ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == "$expectedAdmin" ]]
    docker inspect $(docker ps -q) --format '{{.Name}} {{.Id}} {{.Image}}' | sed 's#^/##' > baseline/containers.txt
    "${compose[@]}" -f "$baseCompose" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 baseline/containers.txt) | jq '{services:(map(select(.Config.Labels["com.docker.compose.project"] == "eduivme-production" and .Config.Labels["com.docker.compose.service"] != null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    for service in "${services[@]}"; do
      name="eduivme-production-$service-1"
      docker inspect "$name" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "$name" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "$name" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json
      mv next.json frozen.compose.json
    done
    jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f rollback.compose.json config --quiet
    "${compose[@]}" -f production.compose.json config --quiet
    baselineTargets; unchanged; date -u +%FT%TZ > prepared.txt
    echo 'Prepared exact running-image baselines and immutable rollback configuration.'
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload; baselineTargets; unchanged
    for service in "${services[@]}"; do
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=10f70067+study-plan-wizard-20261017" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    baselineTargets; unchanged; date -u +%FT%TZ > built.txt; echo 'Built only the wizard UI and completed-topic filtering on the current live images.'
    ;;
  promote)
    test -s built.txt; test ! -e deployed.txt; verifyPayload; baselineTargets; unchanged
    db_user=$(docker exec postgres printenv POSTGRES_USER)
    docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-promotion.backup
    docker exec postgres pg_dump -U "$db_user" -d identity_db -Fc > identity_db.before-promotion.backup
    sha256sum *.before-promotion.backup > live-backups.sha256
    fingerprint coaching_db > coaching.before.txt; identityFingerprint > identity.before.txt
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        for service in "${services[@]}"; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1; waitReady "eduivme-production-$service-1" "$service" || exit 70; done
        echo 'Previous application images restored; database schema and backups retained.' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    for service in "${services[@]}"; do
      [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitReady "eduivme-production-$service-1" "$service"
    done
    fingerprint coaching_db > coaching.after.txt; identityFingerprint > identity.after.txt
    cmp coaching.before.txt coaching.after.txt; cmp identity.before.txt identity.after.txt
    unchanged; date -u +%FT%TZ > deployed.txt; trap - EXIT
    cat deployed.txt; cat live-backups.sha256
    ;;
  validate)
    test -s deployed.txt; verifyPayload; db_user=$(docker exec postgres printenv POSTGRES_USER)
    for service in "${services[@]}"; do
      [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
      waitReady "eduivme-production-$service-1" "$service"
      docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}'
    done
    unchanged; curl -kfsS https://eduivme.com/auth/login >/dev/null; curl -kfsS https://eduivme.com/coaching-portal/study-plan-assistant >/dev/null
    cat coaching.after.txt; cat identity.after.txt; cat deployed.txt
    ;;
  *) echo 'Usage: deploy.sh prepare|build|promote|validate' >&2; exit 2;;
esac
