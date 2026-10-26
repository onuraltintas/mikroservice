set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT l."ExamCode",l."Name",l."IsActive",count(t."Id"),count(t."Id") FILTER(WHERE t."IsActive") FROM coaching.study_catalog_lessons l LEFT JOIN coaching.study_catalog_topics t ON t."LessonId"=l."Id" WHERE l."ExamCode" IN ('TYT','AYT','YDT') GROUP BY 1,2,3 ORDER BY 1,2;
SELECT "Code","IsActive" FROM coaching.study_catalog_exams ORDER BY 1;
COMMIT;
SQL
