set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-teacher-wizard-20261018
db_user=$(docker exec postgres printenv POSTGRES_USER)
mkdir -p verification
rows() {
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'task|'||"Id"||'|'||md5(to_jsonb(t)::text) FROM coaching.study_plan_tasks t ORDER BY "Id";
SELECT 'link|'||"TaskId"||'|'||md5(to_jsonb(t)::text) FROM coaching.study_plan_task_outcomes t ORDER BY "TaskId";
SELECT 'profile|'||"UserId"||'|'||md5(to_jsonb(t)::text) FROM coaching.student_profiles t ORDER BY "UserId";
SELECT 'plan|'||"Id"||'|'||md5((to_jsonb(t)-ARRAY['Status','IsActive','Version','UpdatedAt','PublishedAt','ArchivedAt'])::text) FROM coaching.study_plan_revisions t ORDER BY "Id";
SQL
}
if [[ "$1" == baseline ]]; then
  rows | LC_ALL=C sort > verification/rows.before.txt
  docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > verification/coaching.before-workflow.backup
  echo 'Workflow baseline saved.'
else
  rows | LC_ALL=C sort > verification/rows.after.txt
  LC_ALL=C comm -23 verification/rows.before.txt verification/rows.after.txt > verification/missing-or-modified.txt
  test ! -s verification/missing-or-modified.txt
  echo 'Every original task, outcome link, student profile and plan content preserved.'
fi
