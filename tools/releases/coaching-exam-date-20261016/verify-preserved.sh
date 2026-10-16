set -euo pipefail
umask 077
source /var/lib/eduivme/releases/coaching-exam-date-20261016/deploy.sh validate
fingerprint coaching_db > coaching.browser.after.txt
accounts > accounts.browser.after.txt
cmp coaching.live.before.txt coaching.browser.after.txt
cmp accounts.live.before.txt accounts.browser.after.txt
verifyDb coaching_db > browser-checks.json
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
  IF (SELECT count(*) FROM coaching.study_catalog_exams WHERE "Code"='LIVE261016' AND NOT "IsActive" AND "ExamDate" IS NULL) <> 1 THEN RAISE EXCEPTION 'Test exam not restored'; END IF;
  IF (SELECT count(*) FROM coaching.study_plan_revisions) <> 4 OR (SELECT count(*) FROM coaching.study_plan_tasks) <> 5 THEN RAISE EXCEPTION 'Plan counts changed'; END IF;
  IF (SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted") <> 4 OR (SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted") <> 86 THEN RAISE EXCEPTION 'Work changed'; END IF;
  IF (SELECT count(*) FROM coaching.outcome_duration_estimates WHERE "IsPublished") <> 52 THEN RAISE EXCEPTION 'Published durations changed'; END IF;
END $$;
SELECT 'Exam dates cleared after browser round trip; every existing business row and core account fingerprint unchanged.';
SQL
date -u +%FT%TZ > browser-data-verified.txt
cat browser-checks.json
