set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('editions',count(DISTINCT e."Id"),'outcomes',count(DISTINCT o."Id"),
 'published',count(DISTINCT d."Id") FILTER(WHERE d."IsPublished"),'drafts',count(DISTINCT d."Id") FILTER(WHERE NOT d."IsPublished"))
FROM coaching.official_curriculum_editions e JOIN coaching.study_catalog_outcomes o ON o."EditionId"=e."Id"
JOIN coaching.outcome_duration_estimates d ON d."OutcomeId"=o."Id" WHERE e."ExamCode"='LGS';
SELECT json_build_object('approvedMappings',count(*) FILTER(WHERE "IsApproved"),'unapprovedMappings',count(*) FILTER(WHERE NOT "IsApproved")) FROM coaching.study_catalog_topic_outcomes;
SELECT json_build_object('plans',count(*),'activePlans',count(*) FILTER(WHERE "Status"=1)) FROM coaching.study_plan_revisions;
SELECT json_build_object('tasks',count(*),'completed',count(*) FILTER(WHERE "IsCompleted"),'actualMinutes',sum(coalesce("ActualMinutes",0))) FROM coaching.study_plan_tasks;
SELECT json_build_object('outcomeLinks',count(*)) FROM coaching.study_plan_task_outcomes;
SELECT json_build_object('unmappedLgsLeafTopics',count(*)) FROM coaching.study_catalog_topics t
JOIN coaching.study_catalog_lessons l ON l."Id"=t."LessonId"
WHERE l."ExamCode"='LGS' AND l."IsActive" AND t."IsActive"
AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId"=t."Id")
AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topic_outcomes m JOIN coaching.outcome_duration_estimates d ON d."OutcomeId"=m."OutcomeId" WHERE m."TopicId"=t."Id" AND m."IsApproved" AND d."IsPublished");
SQL
