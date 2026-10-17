#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-restore-profile-20261017
cd "$release"; [[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-restore-profile-20261017
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
drill=coaching_restore_profile_drill_20261017
trap 'printf "CURRICULUM_FAILED stage=%s line=%s\n" "${1:-validate}" "$LINENO" >&2' ERR
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
unchanged() {
  local name id image
  while read -r name id image; do
    case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated service changed: $name" >&2; return 1; }
  done < baseline/containers.txt
}
baselineTargets() {
  local service
  for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
}
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; }
  [[ "$service" != staff-portal ]] || { port=80; path=/; }
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
table_schema||'.'||table_name,
'{}',
'','{}',
'',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory')
ORDER BY table_name
\gexec
SQL
}
accounts() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
}
fingerprintRows() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,coalesce(to_jsonb(t)->>''Id'',md5(to_jsonb(t)::text)),md5((to_jsonb(t)-%L::text[])::text) FROM %I.%I t;',
table_schema||'.'||table_name,'{}',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_weekly_drafts') ORDER BY table_name
\gexec
SQL
}
verifyDb() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM coaching.__ef_migrations_history WHERE "MigrationId"='20261017140000_ManualTimedStudyTasks') THEN RAISE EXCEPTION 'Manual timed migration missing'; END IF;
  IF to_regclass('coaching.study_weekly_drafts') IS NULL THEN RAISE EXCEPTION 'Weekly draft table missing'; END IF;
  IF (SELECT count(*) FROM information_schema.columns WHERE table_schema='coaching' AND ((table_name='study_plan_revisions' AND column_name='WeeklySource') OR (table_name='study_plan_tasks' AND column_name IN ('PlannedStartMinute','WeeklyLessonId','LessonNameSnapshot'))))<>4 THEN RAISE EXCEPTION 'Expected four additive columns'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_weekly_drafts GROUP BY "StudentId" HAVING count(*)>1) THEN RAISE EXCEPTION 'Duplicate student draft'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes t LEFT JOIN coaching.study_plan_tasks p ON p."Id"=t."TaskId" WHERE p."Id" IS NULL) THEN RAISE EXCEPTION 'Orphaned plan outcome'; END IF;
END $$;
SELECT json_build_object('passed',true,'plans',(SELECT count(*) FROM coaching.study_plan_revisions),
'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),
'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),
'drafts',(SELECT count(*) FROM coaching.study_weekly_drafts),'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SQL
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload
    mkdir -p baseline
    docker inspect $(docker ps --format '{{.Names}}') --format '{{.Name}} {{.Id}} {{.Image}}' | sed 's#^/##' > baseline/containers.txt
    [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == sha256:b1dcddda49514ca37f232e9e9faa73d2338491493730067ddb709b02246d1b0d ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:be9aae3825f10de6de3dcb562fb8f9d2bda889baeca46219df1623e043a8c6c9 ]]
    [[ $(docker inspect eduivme-production-staff-portal-1 --format '{{.Image}}') == sha256:c53761cf825c90b388fdd9f02a4a111107d5aabf175a060c7ba449d47d495bc7 ]]
    "${compose[@]}" -f /var/lib/eduivme/releases/coaching-calendar-move-20261017-r2/production.compose.json config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 baseline/containers.txt) | jq '{services:(map(select(.Config.Labels["com.docker.compose.service"] != null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json
      mv next.json frozen.compose.json
    done
    jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    accounts > baseline/accounts.txt; fingerprint coaching_db > baseline/coaching.txt
    unchanged; date -u +%FT%TZ > prepared.txt; echo 'Prepared three services; existing business rows fingerprinted.'
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload; baselineTargets
    for service in "${services[@]}"; do
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$(<source-revision.txt)" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    unchanged; date -u +%FT%TZ > built.txt; echo 'Built three images on the exact live bases.'
    ;;
  drill)
    test -s built.txt; test ! -e drill-verified.txt; baselineTargets; verifyPayload
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching.before-drill.backup
    connection=$(jq -er '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; owner=${BASH_REMATCH[2]}
    docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
    docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching.before-drill.backup > drill-restore.log 2>&1
    fingerprint "$drill" > drill.before.txt
    jq --arg database "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection |= sub("Database=coaching_db;";"Database="+$database+";")' production.compose.json > drill.compose.json
    jq -e --arg database "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$database+";")' drill.compose.json >/dev/null
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migration.log 2>&1
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migration-replay.log 2>&1
    verifyDb "$drill" > drill-result.json
    fingerprint "$drill" > drill.after.txt; cmp drill.before.txt drill.after.txt
    docker exec postgres dropdb -U "$db_user" "$drill"
    sha256sum coaching.before-drill.backup > drill-backup.sha256
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat drill-result.json
    ;;
  promote)
    test -s drill-verified.txt; test ! -e deployed.txt; verifyPayload; baselineTargets; unchanged
    for service in "${services[@]}"; do [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]; done
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        for service in "${services[@]}"; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1; waitReady "eduivme-production-$service-1" "$service" || exit 70; done
        echo 'Previous application images restored; schema and backups retained.' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    docker stop --time 30 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 eduivme-production-coaching-service-1 > writer-stop.log
    for database in identity_db coaching_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup"; done
    sha256sum *.before-promotion.backup > live-backups.sha256
    accounts > accounts.live.before.txt; fingerprint coaching_db > coaching.live.before.txt; fingerprintRows > coaching.rows.before.txt
    "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migration.log 2>&1
    verifyDb coaching_db > live-result.json
    accounts > accounts.live.after-migration.txt; fingerprint coaching_db > coaching.live.after-migration.txt
    cmp accounts.live.before.txt accounts.live.after-migration.txt; cmp coaching.live.before.txt coaching.live.after-migration.txt
    for service in "${services[@]}"; do
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitReady "eduivme-production-$service-1" "$service"
    done
    accounts > accounts.live.after.txt; fingerprint coaching_db > coaching.live.after.txt
    cmp accounts.live.before.txt accounts.live.after.txt; cmp coaching.live.before.txt coaching.live.after.txt
    unchanged; date -u +%FT%TZ > deployed.txt; trap - EXIT; cat live-result.json
    ;;
  validate)
    test -s deployed.txt; verifyPayload
    for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat deployed.txt; cat live-result.json
    ;;
  *) exit 2;;
esac
