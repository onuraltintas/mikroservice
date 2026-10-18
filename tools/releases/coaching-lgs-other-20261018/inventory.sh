set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_agg(x) FROM (SELECT l."Id" AS lesson_id,l."Name" AS lesson,l."CurriculumVersionId" AS curriculum,l."GradeNumber" AS grade,t."Id" AS topic_id,t."SourceId" AS source_id,t."Name" AS topic,t."ParentId" AS parent_id,t."UnitId" AS unit_id,t."DisplayOrder" AS sort_order,NOT EXISTS(SELECT 1 FROM coaching.study_catalog_topics c WHERE c."ParentId"=t."Id") AS leaf FROM coaching.study_catalog_lessons l JOIN coaching.study_catalog_topics t ON t."LessonId"=l."Id" WHERE l."ExamCode"='LGS' AND l."IsActive" AND t."IsActive" ORDER BY l."Name",t."DisplayOrder",t."Name")x;
SQL
