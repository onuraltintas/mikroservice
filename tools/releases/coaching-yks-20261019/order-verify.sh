set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT l."Name",q."Name" FROM coaching.study_catalog_lessons l CROSS JOIN LATERAL (SELECT t."Name" FROM coaching.study_catalog_topics t JOIN coaching.study_catalog_units u ON u."Id"=t."UnitId" WHERE t."LessonId"=l."Id" AND t."IsActive" AND NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId"=t."Id") AND EXISTS(SELECT 1 FROM coaching.study_catalog_yks_scopes s WHERE s."TopicId"=t."Id") ORDER BY u."DisplayOrder",CASE WHEN t."ParentId" IS NULL THEN t."DisplayOrder" ELSE (SELECT p."DisplayOrder" FROM coaching.study_catalog_topics p WHERE p."Id"=t."ParentId") END,t."DisplayOrder",t."Id" LIMIT 1)q WHERE l."ExamCode" IN ('TYT','AYT','YDT') ORDER BY l."ExamCode",l."Name";
SQL
