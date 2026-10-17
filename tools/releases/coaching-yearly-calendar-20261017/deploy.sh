#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-yearly-calendar-20261017
cd "$release"; [[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-yearly-calendar-20261017
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
trap 'printf "CALENDAR_FAILED stage=%s line=%s\n" "${1:-validate}" "$LINENO" >&2' ERR
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
unchanged() {
  local name id image
  while read -r name id image; do
    [[ "$name" != eduivme-production-admin-panel-1 ]] || continue
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated service changed: $name" >&2; return 1; }
  done < baseline/containers.txt
}
baselineTarget() { [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Id}}') == "$(<previous-admin-panel-container.txt)" ]]; }
waitReady() {
  local ip
  for attempt in $(seq 1 40); do
    ip=$(docker inspect eduivme-production-admin-panel-1 --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:4000/auth/login" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  return 1
}
fingerprint() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name
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
# Only the two test plan state transitions and one explicit synthetic completion may change during browser validation.
protectedRows() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5((%s)::text),%L ORDER BY md5((%s)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,expression,'',expression,'',table_schema,table_name)
FROM (SELECT table_schema,table_name,CASE table_name
WHEN 'study_plan_revisions' THEN 'CASE WHEN t."Id" IN (''17bf39c1-a949-424a-9f80-d9d8aa61fa1d'',''2f7e2d07-e044-471d-aedc-dfe004df7e2e'') THEN to_jsonb(t)-''{Status,IsActive,Version,UpdatedAt,UpdatedBy}''::text[] ELSE to_jsonb(t) END'
WHEN 'study_plan_tasks' THEN 'CASE WHEN t."Id"=''51031e12-b6ad-4a0c-96e9-7d6add866469'' THEN to_jsonb(t)-''{IsCompleted,ActualMinutes,ActualQuestions,CompletedAt,Version,UpdatedAt,UpdatedBy}''::text[] ELSE to_jsonb(t) END'
ELSE 'to_jsonb(t)' END expression FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory')) s ORDER BY table_name
\gexec
SELECT 'previous-completions',count(*),sum("ActualMinutes"),md5(string_agg(to_jsonb(t)::text,'' ORDER BY "Id")) FROM coaching.study_plan_tasks t WHERE "IsCompleted" AND "Id"<>'51031e12-b6ad-4a0c-96e9-7d6add866469';
SQL
}
verifyDb() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM coaching.__ef_migrations_history WHERE "MigrationId"='20261017061920_WeeklyOutcomePlans') THEN RAISE EXCEPTION 'Existing migration missing'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes t LEFT JOIN coaching.study_plan_tasks p ON p."Id"=t."TaskId" WHERE p."Id" IS NULL) THEN RAISE EXCEPTION 'Orphaned plan outcome'; END IF;
END $$;
SELECT json_build_object('passed',true,'plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'weeklyDrafts',(SELECT count(*) FROM coaching.study_weekly_drafts),'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SQL
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload; mkdir -p baseline
    docker inspect $(docker ps --format '{{.Names}}') --format '{{.Name}} {{.Id}} {{.Image}}' | sed 's#^/##' > baseline/containers.txt
    [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == sha256:4fac5d3cadbd2d45fbdfefe61fb3699001fbb70d1a1d916903ad100ffef434f2 ]]
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == sha256:38f0da06e429a4e4fa9db8064cab80afc1a807ef8f807a9a921b19e51e81d246 ]]
    "${compose[@]}" -f /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2/production.compose.json config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 baseline/containers.txt) | jq '{services:(map(select(.Config.Labels["com.docker.compose.service"] != null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}' > previous-admin-panel-image.txt
    docker inspect eduivme-production-admin-panel-1 --format '{{.Id}}' > previous-admin-panel-container.txt
    docker inspect eduivme-production-admin-panel-1 | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > admin-panel.environment.json
    jq --slurpfile env admin-panel.environment.json '.services["admin-panel"].environment=$env[0]' frozen.compose.json > next.json; mv next.json frozen.compose.json
    jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    accounts > baseline/accounts.txt; fingerprint > baseline/coaching.txt
    verifyDb > baseline/result.json; unchanged
    date -u +%FT%TZ > prepared.txt; echo 'Prepared admin-panel only; no database migration or API restart.'
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload; baselineTarget
    docker image tag "$(<previous-admin-panel-image.txt)" "eduivme/admin-panel:$tag-base"
    docker build --network none --build-arg "BASE_IMAGE=eduivme/admin-panel:$tag-base" --build-arg "SOURCE_REVISION=$(<source-revision.txt)" -f admin-panel.Dockerfile -t "eduivme/admin-panel:$tag" . > admin-panel-build.log 2>&1
    docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}' > built-admin-panel-image.txt
    unchanged; date -u +%FT%TZ > built.txt; echo 'Built admin-panel on its exact live base.'
    ;;
  promote)
    test -s built.txt; test ! -e deployed.txt; verifyPayload; baselineTarget; unchanged
    [[ $(docker image inspect "eduivme/admin-panel:$tag" --format '{{.Id}}') == "$(<built-admin-panel-image.txt)" ]]
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never admin-panel > admin-panel-rollback.log 2>&1
        waitReady || exit 70
        echo 'Previous admin-panel image restored; all databases retained.' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    for database in identity_db coaching_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup"; done
    sha256sum *.before-promotion.backup > live-backups.sha256
    accounts > accounts.live.before.txt; fingerprint > coaching.live.before.txt; protectedRows > protected.before.txt
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never admin-panel > admin-panel-promotion.log 2>&1
    waitReady
    accounts > accounts.live.after.txt; fingerprint > coaching.live.after.txt
    cmp accounts.live.before.txt accounts.live.after.txt; cmp coaching.live.before.txt coaching.live.after.txt
    verifyDb > live-result.json; unchanged
    date -u +%FT%TZ > deployed.txt; trap - EXIT; cat live-result.json
    ;;
  validate)
    test -s deployed.txt; verifyPayload; waitReady
    [[ $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') == "$(<built-admin-panel-image.txt)" ]]
    unchanged; cat deployed.txt; cat live-result.json
    docker inspect eduivme-production-admin-panel-1 eduivme-production-coaching-service-1 --format '{{.Name}} image={{.Image}} status={{.State.Status}} restarts={{.RestartCount}}'
    ;;
  verify-browser)
    test -s deployed.txt
    accounts > accounts.after-browser.txt; protectedRows > protected.after-browser.txt
    cmp accounts.live.before.txt accounts.after-browser.txt; cmp protected.before.txt protected.after-browser.txt
    docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='17bf39c1-a949-424a-9f80-d9d8aa61fa1d' AND "Status"=2 AND NOT "IsActive") THEN RAISE EXCEPTION 'Old active plan not preserved in archive'; END IF;
  IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "Status"=1 AND "IsActive") THEN RAISE EXCEPTION 'Yearly test plan not active'; END IF;
  IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='51031e12-b6ad-4a0c-96e9-7d6add866469' AND "IsCompleted" AND "ActualMinutes"=17 AND "ActualQuestions"=0 AND "CompletedAt" IS NOT NULL) THEN RAISE EXCEPTION 'Synthetic completion does not match expected values'; END IF;
  IF (SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted")<>5 OR (SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted")<>103 THEN RAISE EXCEPTION 'Unexpected completion totals'; END IF;
END $$;
SELECT "Id","IsCompleted","ActualMinutes","ActualQuestions","CompletedAt","Version" FROM coaching.study_plan_tasks WHERE "Id"='51031e12-b6ad-4a0c-96e9-7d6add866469';
SQL
    verifyDb > browser-result.json; unchanged; cat browser-result.json
    date -u +%FT%TZ > browser-db-verified.txt
    ;;
  *) exit 2;;
esac
