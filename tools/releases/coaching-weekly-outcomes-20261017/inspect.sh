set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted"),sum("ActualMinutes") FROM coaching.study_plan_tasks;
SELECT 'plan-status',"Status",count(*) FROM coaching.study_plan_revisions GROUP BY "Status";
SELECT l."Name",l."ExamCode",count(DISTINCT o."Id"),sum(e."LearningMinutes"+e."PracticeMinutes"+e."CheckMinutes")
FROM coaching.study_catalog_lessons l
JOIN coaching.study_catalog_topics t ON t."LessonId"=l."Id"
JOIN coaching.study_catalog_topic_outcomes m ON m."TopicId"=t."Id" AND m."IsApproved"
JOIN coaching.study_catalog_outcomes o ON o."Id"=m."OutcomeId" AND NOT o."IsArchived"
JOIN coaching.outcome_duration_estimates e ON e."OutcomeId"=o."Id" AND e."IsPublished"
AND NOT EXISTS(SELECT 1 FROM coaching.outcome_duration_estimates n WHERE n."OutcomeId"=e."OutcomeId" AND n."IsPublished" AND n."Number">e."Number")
GROUP BY l."Name",l."ExamCode";
SELECT 'weekly',"Version","TimeZoneId","SlotsJson" FROM coaching.study_weekly_drafts;
SELECT 'context',"AcademicYearStart","Version" FROM coaching.student_learning_contexts;
SELECT 'latest-migration',max("MigrationId") FROM coaching.__ef_migrations_history;
SQL
