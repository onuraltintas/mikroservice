\set ON_ERROR_STOP on
BEGIN;
\ir incoming.sql
DO $$ BEGIN
 IF (SELECT count(*) FROM coaching.target_catalog WHERE "CatalogType"='School')<>3150
 OR (SELECT count(*) FROM coaching.target_catalog WHERE "CatalogType"='UniversityProgram')<>21493
 OR EXISTS(SELECT 1 FROM incoming i LEFT JOIN coaching.target_catalog c ON c."Source"='mebi-lgs' AND c."SourceId"=i.id::text
  WHERE c."Id" IS NULL OR c."Name"<>i."schoolName" OR c."City"<>i.city OR c."District"<>i.town
    OR c."InstitutionType" IS DISTINCT FROM i."schoolType" OR c."Language" IS DISTINCT FROM i.language
    OR c."MinimumScore" IS DISTINCT FROM i."minScore" OR c."Field" IS DISTINCT FROM i.branch
    OR c."PlacementYear" IS NOT NULL OR c."PlacementStage" IS NOT NULL OR c."Quota" IS NOT NULL OR NOT c."IsActive")
 THEN RAISE EXCEPTION 'Catalog count or source field mismatch'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema='coaching' AND table_name IN ('target_schools','target_university_programs','target_admission_batches','target_admission_records'))
 THEN RAISE EXCEPTION 'Retired catalog tables remain'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.academic_goals g LEFT JOIN coaching.target_catalog s ON s."Id"=g.target_school_id
  LEFT JOIN coaching.target_catalog p ON p."Id"=g.target_university_program_id
  WHERE (g.target_school_id IS NOT NULL AND (s."Id" IS NULL OR s."CatalogType"<>'School'))
     OR (g.target_university_program_id IS NOT NULL AND (p."Id" IS NULL OR p."CatalogType"<>'UniversityProgram')))
 THEN RAISE EXCEPTION 'A goal relationship is missing or changed kind'; END IF;
END $$;
SELECT json_build_object('catalog_total',count(*),'lgs',count(*) FILTER(WHERE "CatalogType"='School'),
 'yks',count(*) FILTER(WHERE "CatalogType"='UniversityProgram'),'ydt',count(*) FILTER(WHERE "ScoreType"='YDT')) FROM coaching.target_catalog;
ROLLBACK;
