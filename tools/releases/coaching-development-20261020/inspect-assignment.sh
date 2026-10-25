set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT table_name,column_name FROM information_schema.columns WHERE table_schema='coaching' AND table_name IN ('assignments','assignment_students','student_assignments') ORDER BY 1,ordinal_position;
SELECT 'assignment',id,teacher_id,title,status FROM coaching.assignments WHERE id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
COMMIT;
SQL
docker logs --since 12m eduivme-production-coaching-service-1 2>&1 | tail -90
