-- KM2 read-only check; run before and after each step and paste the output into the release notes.
-- It fails (non-zero exit) when an invariant the pack relies on is broken.
\set ON_ERROR_STOP on
BEGIN READ ONLY;
SET LOCAL search_path = coaching;

-- 1. AYT Tarih: links, KM2 review state and YKS scopes.
SELECT format('ayt tarih: leaves %s | linked %s | km2 pending %s | km2 approved %s | with yks scope %s',
  count(*),
  count(*) FILTER (WHERE EXISTS (SELECT 1 FROM study_catalog_topic_outcomes m WHERE m."TopicId"=t."Id")),
  (SELECT count(*) FROM study_catalog_topic_outcomes m JOIN (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) x ON x."Id"=m."TopicId"
    WHERE x.lesson_source_id='c4ee03b3-8384-4963-b599-08dc055fab0e' AND m."Evidence" LIKE 'KM2 önerisi%' AND m."ReviewedBy" IS NULL),
  (SELECT count(*) FROM study_catalog_topic_outcomes m JOIN (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) x ON x."Id"=m."TopicId"
    WHERE x.lesson_source_id='c4ee03b3-8384-4963-b599-08dc055fab0e' AND m."Evidence" LIKE 'KM2 önerisi%' AND m."IsApproved"),
  count(*) FILTER (WHERE EXISTS (SELECT 1 FROM study_catalog_yks_scopes s WHERE s."TopicId"=t."Id")))
FROM (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) t WHERE t.lesson_source_id='c4ee03b3-8384-4963-b599-08dc055fab0e';

-- 2. TDP Matematik (Destek): estimates and yield weights.
SELECT format('tdp matematik: leaves %s | without minutes %s | yield weight other than 3: %s',
  count(*), count(*) FILTER (WHERE "EstimatedMinutes" IS NULL), count(*) FILTER (WHERE "YieldWeight"<>3))
FROM (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) WHERE lesson_source_id='6283030c-281d-4ff3-1536-08dce12e5803';

-- 3. LGS Matematik: the 8th-grade MEB 2018 program edition is complete (12 official topics, 52 outcomes, 180 lesson periods).
SELECT format('lgs matematik: official topics %s | outcomes %s | lesson periods %s | outcomes linked %s | leaves %s',
  (SELECT count(*) FROM official_curriculum_topics ot WHERE ot."EditionId"=e."Id"),
  (SELECT count(*) FROM study_catalog_outcomes o WHERE o."EditionId"=e."Id" AND NOT o."IsArchived"),
  (SELECT sum(ot."LessonPeriods") FROM official_curriculum_topics ot WHERE ot."EditionId"=e."Id"),
  (SELECT count(DISTINCT m."OutcomeId") FROM study_catalog_topic_outcomes m JOIN study_catalog_outcomes o ON o."Id"=m."OutcomeId"
    JOIN (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) x ON x."Id"=m."TopicId" WHERE o."EditionId"=e."Id" AND m."IsApproved" AND x.lesson_source_id='00c641eb-c82d-4712-3b41-08dd3944e858'),
  (SELECT count(*) FROM (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) x WHERE x.lesson_source_id='00c641eb-c82d-4712-3b41-08dd3944e858'))
FROM official_curriculum_editions e WHERE e."Code"='meb-2018-lgs-matematik-2026-pilot-v1';

-- 4. LGS schools: the year is still unknown (see lgs-school-year-check.sql).
SELECT format('lgs schools: rows %s | catalog year %s | placement year %s', count(*), count("CatalogYear"), count("PlacementYear"))
FROM target_catalog WHERE "CatalogType"='School';

DO $$
DECLARE problem text;
BEGIN
  -- A YKS scope makes an AYT Tarih topic plannable for SÖZ/EA students, so it must never precede an approved link.
  SELECT string_agg(t."Name", '; ') INTO problem FROM (SELECT t.*, l."SourceId" lesson_source_id FROM study_catalog_topics t JOIN study_catalog_lessons l ON l."Id"=t."LessonId"
  WHERE l."Source"='approved-catalog' AND NOT EXISTS (SELECT 1 FROM study_catalog_topics c WHERE c."ParentId"=t."Id")) t
  WHERE t.lesson_source_id='c4ee03b3-8384-4963-b599-08dc055fab0e'
    AND EXISTS (SELECT 1 FROM study_catalog_yks_scopes s WHERE s."TopicId"=t."Id")
    AND NOT EXISTS (SELECT 1 FROM study_catalog_topic_outcomes m WHERE m."TopicId"=t."Id" AND m."IsApproved");
  IF problem IS NOT NULL THEN RAISE EXCEPTION 'km2 verify: YKS scope without an approved outcome link: %', problem; END IF;
  -- Every KM2 duration version adds up exactly (OutcomeScopeBudget).
  SELECT string_agg(e."Id"::text, ', ') INTO problem FROM outcome_duration_estimates e
  WHERE e."Evidence" LIKE 'KM2%' AND EXISTS (SELECT 1 FROM (SELECT sum(s."LearningMinutes") l, sum(s."PracticeMinutes") p,
      sum(s."CheckMinutes") c FROM study_catalog_topic_outcome_shares s WHERE s."EstimateId"=e."Id") x
    WHERE x.l IS DISTINCT FROM e."LearningMinutes" OR x.p IS DISTINCT FROM e."PracticeMinutes" OR x.c IS DISTINCT FROM e."CheckMinutes");
  IF problem IS NOT NULL THEN RAISE EXCEPTION 'km2 verify: duration shares do not add up: %', problem; END IF;
END $$;
ROLLBACK;
