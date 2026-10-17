set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-calendar-move-20261017
cd "$release"
db_user=$(docker exec postgres printenv POSTGRES_USER)
baseline_db=coaching_calendar_verify_20261017
[[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$baseline_db'") == 0 ]]
connection=$(jq -er '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; owner=${BASH_REMATCH[2]}
docker exec postgres createdb -U "$db_user" -O "$owner" "$baseline_db"
docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$baseline_db" --exit-on-error --no-owner --no-privileges < coaching_db.before-promotion.backup > original-restore.log 2>&1
fingerprint() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,coalesce(to_jsonb(t)->>''Id'',md5(to_jsonb(t)::text)),md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_plan_revisions','study_plan_tasks') ORDER BY table_name
\gexec
SELECT 'study_plan_revisions',"Id",md5((to_jsonb(t)-CASE WHEN "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' THEN '{UpdatedAt,UpdatedBy,Version}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_revisions t WHERE "Id"<>'b5536157-dd00-4f63-b522-9f250c77b33b';
SELECT 'study_plan_tasks',"Id",md5((to_jsonb(t)-CASE WHEN "Id" IN ('84d75f90-06e8-424b-a772-3b519b2c3aac','8ce4d3c9-cde0-40b7-82e0-9e98612741a9','cf55f3ef-167a-432c-8d1b-54f4a5fc4425') THEN '{UpdatedAt,UpdatedBy,Version}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_tasks t WHERE "Id"<>'afbc9fea-ae32-4bb5-bc65-00395d3755f7';
SQL
}
fingerprint "$baseline_db" > original-protected.before.txt
fingerprint coaching_db > original-protected.after.txt
cmp original-protected.before.txt original-protected.after.txt
docker exec postgres dropdb -U "$db_user" "$baseline_db"
date -u +%FT%TZ > original-rows-verified.txt
printf 'All original business rows, outcome links, accounts relationships and completed work retained. Only the four known move audit/version fields differ.\n'
