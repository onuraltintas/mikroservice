set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-practice-yks-regression-20261020
db_user=$(docker exec postgres printenv POSTGRES_USER)
snapshot(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L||%L||md5((to_jsonb(t)-%L-%L)::text) FROM %I.%I t;',table_schema||'.'||table_name,'|',
 CASE WHEN table_name='study_plan_revisions' THEN 'Version' ELSE '' END,
 CASE WHEN table_name='study_plan_revisions' THEN 'UpdatedAt' ELSE '' END,table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY 1 \gexec
COMMIT;
SQL
}
case "${1:-}" in
prepare)
 test ! -e "$release"; mkdir -p "$release"; cd "$release"
 for database in coaching_db identity_db; do
   docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"
   snapshot "$database" > "$database.before.rows"
 done
 sha256sum *.backup > backups.sha256
 date -u +%FT%TZ > started.txt
 echo 'Current accounts, all prior exams, tasks, outcome links and plans backed up and fingerprinted.'
 ;;
verify)
 cd "$release"; test -s started.txt
 for database in coaching_db identity_db; do
   snapshot "$database" > "$database.after.rows"
   LC_ALL=C comm -23 "$database.before.rows" "$database.after.rows" > "$database.changed-original.rows"
   test ! -s "$database.changed-original.rows"
 done
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "Id" FROM coaching.study_plan_revisions WHERE "IsActive"') == 'a4cea155-5485-4db6-9f52-1ec4df8080c3' ]]
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | tee results.txt
BEGIN READ ONLY;
SELECT 'active',"Id","Version",jsonb_extract_path_text("WeeklySource",'EndDate') FROM coaching.study_plan_revisions WHERE "IsActive";
SELECT 'protected_tasks',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
SELECT 'links',count(*) FROM coaching.study_plan_task_outcomes;
SELECT coalesce(r.practice_exam->>'ExamCode',r.practice_exam->>'examCode'),CASE WHEN e.student_owner_id IS NOT NULL THEN 'student' ELSE 'teacher' END,count(*),count(*) FILTER(WHERE r.topic_analysis IS NOT NULL) FROM coaching.exam_results r JOIN coaching.exams e ON e.id=r.exam_id WHERE e.title LIKE 'YKS-REVIEW26 %' GROUP BY 1,2 ORDER BY 1,2;
SELECT 'test_plan',p."Status",p."Version",count(t."Id"),count(t."Id") FILTER(WHERE t."SourceName" LIKE 'DenemeTekrar:%'),sum(t."PlannedMinutes") FILTER(WHERE t."SourceName" LIKE 'DenemeTekrar:%'),count(DISTINCT t."SourceName") FILTER(WHERE t."SourceName" LIKE 'DenemeTekrar:%') FROM coaching.study_plan_revisions p JOIN coaching.study_plan_tasks t ON t."RevisionId"=p."Id" WHERE p."Id"='15aba7c7-9907-470b-9b5d-585e5c412659' GROUP BY p."Status",p."Version";
SELECT 'test_plan_overlaps',count(*) FROM coaching.study_plan_tasks a JOIN coaching.study_plan_tasks b ON a."RevisionId"=b."RevisionId" AND a."Id"<b."Id" AND a."PlannedDate"=b."PlannedDate" AND a."PlannedStartMinute"<b."PlannedStartMinute"+b."PlannedMinutes" AND b."PlannedStartMinute"<a."PlannedStartMinute"+a."PlannedMinutes" WHERE a."RevisionId"='15aba7c7-9907-470b-9b5d-585e5c412659';
COMMIT;
SQL
 docker logs --since "$(<started.txt)" eduivme-production-coaching-service-1 > api-live.log 2>&1
 printf 'API error-pattern-lines: '; (grep -Eic '(Unhandled|fail:|error:|Exception)' api-live.log || true)
 mkdir -p public-verification
 cp results.txt started.txt api-fixed.txt api-fixed-image.txt public-verification/
 for database in coaching_db identity_db; do
  printf '%s|original_rows=%s|missing_or_changed=%s\n' "$database" "$(wc -l < "$database.before.rows")" "$(wc -l < "$database.changed-original.rows")" >> public-verification/preservation.txt
 done
 tar -czf public-verification.tar.gz public-verification
 date -u +%FT%TZ > verified.txt
 echo 'Every pre-existing normalized row remains; original LGS plan is active again.'
 ;;
*) exit 2;;
esac
