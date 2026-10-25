set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
test -s deployed.txt; test -s hotfix-deployed.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
coaching_copy=coaching_practice_verify_20261020
identity_copy=identity_practice_verify_20261020
for database in "$coaching_copy" "$identity_copy"; do
  [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]
  docker exec postgres createdb -U "$db_user" "$database"
done
docker exec -i postgres pg_restore -U "$db_user" -d "$coaching_copy" --exit-on-error --no-owner --no-privileges < coaching_db.before-promotion.backup > verify-coaching-restore.log 2>&1
docker exec -i postgres pg_restore -U "$db_user" -d "$identity_copy" --exit-on-error --no-owner --no-privileges < identity_db.before.backup > verify-identity-restore.log 2>&1
coaching_hash(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5((to_jsonb(t)-%L-%L-%L-%L-%L-%L-%L)::text),%L ORDER BY md5((to_jsonb(t)-%L-%L-%L-%L-%L-%L-%L)::text)),%L)) FROM coaching.%I t %s;',
 table_name,'CreatedByTeacherId','topic_analysis','practice_exam','target_net','target_practice_exam_code',CASE WHEN table_name='study_plan_revisions' THEN 'Version' ELSE '' END,CASE WHEN table_name='study_plan_revisions' THEN 'UpdatedAt' ELSE '' END,'',
 'CreatedByTeacherId','topic_analysis','practice_exam','target_net','target_practice_exam_code',CASE WHEN table_name='study_plan_revisions' THEN 'Version' ELSE '' END,CASE WHEN table_name='study_plan_revisions' THEN 'UpdatedAt' ELSE '' END,'',table_name,
 CASE WHEN table_name='study_plan_tasks' THEN 'WHERE coalesce(t."SourceName",'''') NOT LIKE ''DenemeTekrar:%''' ELSE '' END)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','__ef_migrations_history','__EFMigrationsHistory','exams','exam_results','study_availability')
AND table_name NOT ILIKE '%idempotency%' ORDER BY table_name \gexec
COMMIT;
SQL
}
identity_hash(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY 1 \gexec
COMMIT;
SQL
}
coaching_hash "$coaching_copy" > live-original-coaching.before.txt
coaching_hash coaching_db > live-original-coaching.after.txt
cmp live-original-coaching.before.txt live-original-coaching.after.txt
identity_hash "$identity_copy" > live-identity.before.txt
identity_hash identity_db > live-identity.after.txt
cmp live-identity.before.txt live-identity.after.txt
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee live-e2e-counts.txt
BEGIN READ ONLY;
SELECT 'active_plan',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'active_tasks',count(*),count(*) FILTER(WHERE t."IsCompleted") FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions p ON p."Id"=t."RevisionId" WHERE p."IsActive";
SELECT 'original_tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks WHERE coalesce("SourceName",'') NOT LIKE 'DenemeTekrar:%';
SELECT 'outcome_links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT 'added_review',"PlannedDate","PlannedStartMinute","PlannedMinutes","LessonNameSnapshot","Title" FROM coaching.study_plan_tasks WHERE "SourceName" LIKE 'DenemeTekrar:%' ORDER BY 2,3;
SELECT 'analyzed_exams',count(*),count(*) FILTER(WHERE r.topic_analysis IS NOT NULL) FROM coaching.exam_results r JOIN coaching.exams e ON e.id=r.exam_id WHERE e.title LIKE 'Canlı doğrulama · % LGS % · 20.10.2026';
COMMIT;
SQL
for database in "$coaching_copy" "$identity_copy"; do docker exec postgres dropdb -U "$db_user" "$database"; done
for service in coaching-service admin-panel staff-portal; do
  docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}} {{.Image}}'
  docker logs --since 2026-10-20T10:35:00Z "eduivme-production-$service-1" > "live-$service.log" 2>&1
done
echo 'All original accounts and protected Coaching rows match the restored backups. Only the approved plan version/timestamp, test exams, availability and four new reviews differ.'
date -u +%FT%TZ > live-preservation-verified.txt
