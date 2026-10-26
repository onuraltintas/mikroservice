set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'preview task count',count(*) FROM coaching.study_plan_tasks;
SELECT 'preview plan version',"Version" FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3';
SELECT 'preview retains original completed link',"StudyTaskId"='294578cc-e8e0-431d-a25b-a0a0d1674ca5'::uuid FROM coaching.weekly_decisions WHERE "Title"='CANLI KARAR BAĞLANTI TESTİ · 21.10.2026';
COMMIT;
SQL
