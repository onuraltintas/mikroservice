\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='10s';
SELECT pg_advisory_xact_lock(4188,100);
LOCK TABLE coaching.study_plan_revisions,coaching.study_plan_tasks,coaching.study_plan_task_outcomes,
 coaching.study_weekly_drafts,coaching.study_availability,coaching.reminder_receipts,
 coaching.study_catalog_topics,coaching.study_catalog_topic_outcomes,coaching.outcome_duration_estimates,
 coaching.study_catalog_topic_outcome_shares,coaching.student_topic_states,coaching.study_catalog_lessons,
 coaching.study_catalog_units,coaching.student_course_preferences,coaching.study_catalog_exams
 IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE target_plans AS SELECT unnest(string_to_array(:'plan_ids',',')::uuid[]) AS id;
CREATE TEMP TABLE target_students AS SELECT unnest(string_to_array(:'student_ids',',')::uuid[]) AS id;
DELETE FROM coaching.reminder_receipts WHERE "Kind"='StudyDay' AND "SourceId" IN(SELECT id FROM target_plans);
DELETE FROM coaching.study_plan_task_outcomes WHERE "TaskId" IN(SELECT "Id" FROM coaching.study_plan_tasks WHERE "RevisionId" IN(SELECT id FROM target_plans));
DELETE FROM coaching.study_plan_tasks WHERE "RevisionId" IN(SELECT id FROM target_plans);
DELETE FROM coaching.study_plan_revisions WHERE "Id" IN(SELECT id FROM target_plans);
DELETE FROM coaching.study_weekly_drafts WHERE "StudentId" IN(SELECT id FROM target_students)
 AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."StudentId"=coaching.study_weekly_drafts."StudentId");
DELETE FROM coaching.study_availability WHERE "StudentId" IN(SELECT id FROM target_students)
 AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."StudentId"=coaching.study_availability."StudentId");
CREATE TEMP TABLE latest_lgs AS
 SELECT DISTINCT ON(e."OutcomeId") e."Id",e."OutcomeId",e."Number"
 FROM coaching.outcome_duration_estimates e JOIN coaching.study_catalog_outcomes o ON o."Id"=e."OutcomeId"
 JOIN coaching.official_curriculum_editions p ON p."Id"=o."EditionId"
 WHERE p."ExamCode"='LGS' AND e."IsPublished" ORDER BY e."OutcomeId",e."Number" DESC;
DO $$ BEGIN
 IF (SELECT count(*) FROM latest_lgs)<>269 OR (SELECT count(*) FROM coaching.study_catalog_topic_outcome_shares s JOIN latest_lgs l ON l."Id"=s."EstimateId")<>319 THEN
  RAISE EXCEPTION 'Current reviewed LGS scope must be complete before cleanup';
 END IF;
END $$;
DELETE FROM coaching.outcome_duration_estimates e USING latest_lgs l WHERE e."OutcomeId"=l."OutcomeId" AND e."Number"<l."Number"
 AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes w WHERE w."EstimateId"=e."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id");
DELETE FROM coaching.study_catalog_topic_outcomes m USING coaching.study_catalog_outcomes o,coaching.official_curriculum_editions p
 WHERE m."OutcomeId"=o."Id" AND o."EditionId"=p."Id" AND p."ExamCode"='LGS' AND NOT m."IsApproved"
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topic_outcome_shares s WHERE s."TopicId"=m."TopicId" AND s."OutcomeId"=m."OutcomeId");
DELETE FROM coaching.study_catalog_topics t WHERE t."SourceId"='lgs-2026:din:ornek-davranislar' AND NOT t."IsActive"
 AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks w WHERE w."TopicId"=t."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.student_topic_states s WHERE s."TopicId"=t."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topic_outcomes m WHERE m."TopicId"=t."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId"=t."Id");
DELETE FROM coaching.study_catalog_lessons l WHERE l."ExamCode"='LIVE261016' AND NOT l."IsActive"
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_units u WHERE u."LessonId"=l."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topics t WHERE t."LessonId"=l."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.student_course_preferences p WHERE p."LessonId"=l."Id")
 AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks w WHERE w."WeeklyLessonId"=l."Id");
DELETE FROM coaching.study_catalog_exams e WHERE e."Code"='LIVE261016' AND NOT e."IsActive"
 AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_lessons l WHERE l."ExamCode"=e."Code")
 AND NOT EXISTS(SELECT 1 FROM coaching.official_curriculum_editions p WHERE p."ExamCode"=e."Code");
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM coaching.study_plan_tasks) OR EXISTS(SELECT 1 FROM coaching.study_plan_revisions)
  OR EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes) THEN
  RAISE EXCEPTION 'Unexpected plan arrived after approved cleanup snapshot; retain it and review';
 END IF;
 IF (SELECT count(*) FROM coaching.outcome_duration_estimates)<>269
  OR (SELECT count(*) FROM coaching.study_catalog_topic_outcome_shares)<>319
  OR (SELECT count(*) FROM coaching.study_catalog_topic_outcomes WHERE "IsApproved")<>319 THEN
  RAISE EXCEPTION 'Current LGS data changed unexpectedly';
 END IF;
END $$;
COMMIT;
