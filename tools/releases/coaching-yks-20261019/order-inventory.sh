set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -U "$db_user" -d coaching_db <<'SQL'
SELECT json_agg(x) FROM (SELECT u."Id",u."SourceId",u."Name",u."DisplayOrder",l."SourceId" lesson_source FROM coaching.study_catalog_units u JOIN coaching.study_catalog_lessons l ON l."Id"=u."LessonId" WHERE l."ExamCode" IN ('TYT','AYT','YDT') ORDER BY l."Name",u."DisplayOrder") x;
SELECT json_agg(x) FROM (SELECT t."Id",t."SourceId",t."Name",t."DisplayOrder",t."ParentId",l."SourceId" lesson_source FROM coaching.study_catalog_topics t JOIN coaching.study_catalog_lessons l ON l."Id"=t."LessonId" WHERE l."ExamCode"='TYT' AND l."Name" LIKE 'Matematik%' ORDER BY t."DisplayOrder",t."Id" LIMIT 8)x;
SQL
