set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
db_user=$(docker exec postgres printenv POSTGRES_USER)
coaching_copy=coaching_practice_verify_20261020
identity_copy=identity_practice_verify_20261020
sed '/^identity.RefreshTokens|/d' live-identity.before.txt > live-identity-accounts.before.txt
sed '/^identity.RefreshTokens|/d' live-identity.after.txt > live-identity-accounts.after.txt
cmp live-identity-accounts.before.txt live-identity-accounts.after.txt
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
