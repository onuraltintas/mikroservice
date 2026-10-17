set -euo pipefail
umask 077
source /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017-r2/deploy.sh validate
fingerprintRows > coaching.rows.browser.after.txt
LC_ALL=C comm -23 coaching.rows.before.txt coaching.rows.browser.after.txt > changed-old-rows.txt
test ! -s changed-old-rows.txt
accounts > accounts.browser.after.txt
cmp accounts.live.before.txt accounts.browser.after.txt
verifyDb coaching_db > browser-checks.json
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "WeeklySource" IS NOT NULL AND "Title" LIKE 'Canlı doğrulama · Yıllık kazanım%') THEN RAISE EXCEPTION 'Verification draft not found'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "WeeklySource" IS NOT NULL AND "IsActive") THEN RAISE EXCEPTION 'Verification must not activate a new plan'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes l JOIN coaching.study_plan_tasks t ON t."Id"=l."TaskId" JOIN coaching.study_plan_revisions r ON r."Id"=t."RevisionId" WHERE r."WeeklySource" IS NOT NULL AND l."AllocatedMinutes"<>t."PlannedMinutes") THEN RAISE EXCEPTION 'Allocation mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_tasks a JOIN coaching.study_plan_tasks b ON a."RevisionId"=b."RevisionId" AND a."PlannedDate"=b."PlannedDate" AND a."Id"<b."Id" AND a."PlannedStartMinute"<b."PlannedStartMinute"+b."PlannedMinutes" AND b."PlannedStartMinute"<a."PlannedStartMinute"+a."PlannedMinutes" JOIN coaching.study_plan_revisions r ON r."Id"=a."RevisionId" WHERE r."WeeklySource" IS NOT NULL) THEN RAISE EXCEPTION 'Generated tasks overlap'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_tasks t JOIN coaching.study_plan_revisions r ON r."Id"=t."RevisionId" WHERE r."WeeklySource" IS NOT NULL AND (t."IsCompleted" OR t."ActualMinutes" IS NOT NULL OR t."PlannedDate"<(r."WeeklySource"->>'StartDate')::date OR t."PlannedDate">(r."WeeklySource"->>'EndDate')::date)) THEN RAISE EXCEPTION 'Invalid generated task range/actual work'; END IF;
  IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes l JOIN coaching.study_plan_tasks t ON t."Id"=l."TaskId" JOIN coaching.study_plan_revisions r ON r."Id"=t."RevisionId" JOIN coaching.outcome_duration_estimates e ON e."Id"=l."EstimateId" WHERE r."WeeklySource" IS NOT NULL AND (NOT e."IsPublished" OR EXISTS(SELECT 1 FROM coaching.outcome_duration_estimates n WHERE n."OutcomeId"=e."OutcomeId" AND n."IsPublished" AND n."Number">e."Number"))) THEN RAISE EXCEPTION 'Unexpected estimate version'; END IF;
END $$;
SELECT json_build_object('revision',r."RevisionNumber",'status',r."Status",'source',r."WeeklySource",'tasks',count(*),'minutes',sum(t."PlannedMinutes"),'firstDate',min(t."PlannedDate"),'lastDate',max(t."PlannedDate")) FROM coaching.study_plan_revisions r JOIN coaching.study_plan_tasks t ON t."RevisionId"=r."Id" WHERE r."WeeklySource" IS NOT NULL GROUP BY r."Id";
SELECT 'outcomeLinks',count(*),'distinctOutcomes',count(DISTINCT l."OutcomeId"),'minutes',sum(l."AllocatedMinutes"),'estimateVersions',array_agg(DISTINCT e."Number") FROM coaching.study_plan_task_outcomes l JOIN coaching.study_plan_tasks t ON t."Id"=l."TaskId" JOIN coaching.study_plan_revisions r ON r."Id"=t."RevisionId" JOIN coaching.outcome_duration_estimates e ON e."Id"=l."EstimateId" WHERE r."WeeklySource" IS NOT NULL;
SELECT 'all-existing-business-rows-preserved',true;
SQL
date -u +%FT%TZ > browser-data-verified.txt
cat browser-checks.json
