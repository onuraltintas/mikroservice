set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('plans', (SELECT count(*) FROM coaching.study_plan_revisions),
 'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'links',(SELECT count(*) FROM coaching.study_plan_task_outcomes),
 'preferences',(SELECT count(*) FROM coaching.student_course_preferences),
 'weeklySlots',(SELECT coalesce(sum(jsonb_array_length("SlotsJson")),0) FROM coaching.study_weekly_drafts),
 'outcomes',(SELECT count(*) FROM coaching.study_catalog_outcomes),'estimates',(SELECT count(*) FROM coaching.outcome_duration_estimates),
 'shares',(SELECT count(*) FROM coaching.study_catalog_topic_outcome_shares));
SELECT json_build_object('id',p."Id",'title',p."Title",'status',p."Status",'active',p."IsActive",'version',p."Version",'weeklySource',p."WeeklySource") FROM coaching.study_plan_revisions p;
SELECT json_build_object('lesson',t."LessonNameSnapshot",'minutes',sum(t."PlannedMinutes"),'tasks',count(*),'topics',count(DISTINCT t."TopicId"),'start',min(t."PlannedDate"),'finish',max(t."PlannedDate")) FROM coaching.study_plan_tasks t GROUP BY t."LessonNameSnapshot" ORDER BY t."LessonNameSnapshot";
WITH allocations AS(SELECT t."TopicId",l."OutcomeId",l."EstimateId",sum(l."AllocatedMinutes") minutes FROM coaching.study_plan_task_outcomes l JOIN coaching.study_plan_tasks t ON t."Id"=l."TaskId" GROUP BY t."TopicId",l."OutcomeId",l."EstimateId"),
differences AS(SELECT s."TopicId" FROM coaching.study_catalog_topic_outcome_shares s FULL JOIN allocations a ON a."TopicId"=s."TopicId" AND a."OutcomeId"=s."OutcomeId" AND a."EstimateId"=s."EstimateId" WHERE a.minutes IS DISTINCT FROM s."LearningMinutes"+s."PracticeMinutes"+s."CheckMinutes")
SELECT json_build_object('scopeDifferences',(SELECT count(*) FROM differences),
 'uniqueOutcomes',(SELECT count(DISTINCT "OutcomeId") FROM allocations),
 'outcomeMinutes',(SELECT sum(minutes) FROM allocations),
 'overlaps',(SELECT count(*) FROM coaching.study_plan_tasks a JOIN coaching.study_plan_tasks b ON a."Id"<b."Id" AND a."StudentId"=b."StudentId" AND a."PlannedDate"=b."PlannedDate" AND a."PlannedStartMinute"<b."PlannedStartMinute"+b."PlannedMinutes" AND b."PlannedStartMinute"<a."PlannedStartMinute"+a."PlannedMinutes"),
 'outsideDates',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "PlannedDate"<'2026-10-19' OR "PlannedDate">'2027-06-01'),
 'paragrafTopics',(SELECT count(DISTINCT t."Id") FROM coaching.study_catalog_topics t JOIN coaching.study_plan_tasks w ON w."TopicId"=t."Id" WHERE t."Name" LIKE 'Paragrafta Anlam%'),
 'wrongEstimateVersions',(SELECT count(*) FROM allocations a WHERE EXISTS(SELECT 1 FROM coaching.outcome_duration_estimates e JOIN coaching.outcome_duration_estimates old ON old."Id"=a."EstimateId" WHERE e."OutcomeId"=old."OutcomeId" AND e."IsPublished" AND e."Number">old."Number")));
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'ACCOUNTS',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SQL
