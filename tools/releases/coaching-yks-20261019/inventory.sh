set -euo pipefail
for service in coaching-service admin-panel staff-portal;do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} {{.Image}} {{index .Config.Labels "org.opencontainers.image.release"}} {{index .Config.Labels "org.opencontainers.image.revision"}}'
done
docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.Domain.dll /app/Coaching.Application.dll /app/Coaching.Infrastructure.dll /app/Coaching.API.dll
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'ArabicYdtLessons',count(*) FROM coaching.study_catalog_lessons WHERE "ExamCode"='YDT' AND ("Name" ILIKE '%Arap%' OR "Name" ILIKE '%Arabic%');
SELECT 'ArabicYdtPrograms',count(*) FROM coaching.official_curriculum_editions WHERE "ExamCode"='YDT' AND ("SubjectName" ILIKE '%Arap%' OR "SubjectName" ILIKE '%Arabic%');
SELECT 'ArabicLearningContexts',count(*) FROM coaching.student_learning_contexts WHERE "ForeignLanguage"='Arabic';
SELECT 'StudyPlanCount',count(*) FROM coaching.study_plan_revisions;
SELECT 'StudyTaskCount',count(*),count(*) FILTER(WHERE "IsCompleted") FROM coaching.study_plan_tasks;
COMMIT;
SQL
