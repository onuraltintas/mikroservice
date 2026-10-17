set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
teacher_id=$(docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db -c 'SELECT a."TeacherUserId" FROM coaching.teacher_student_assignments a JOIN coaching.study_plan_revisions p ON p."StudentId"=a."StudentUserId" WHERE p."Id"='"'2f7e2d07-e044-471d-aedc-dfe004df7e2e'"' AND a."IsActive" LIMIT 1')
[[ "$teacher_id" =~ ^[0-9a-f-]{36}$ ]]
docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db -c "SELECT \"Email\" FROM identity.users WHERE \"Id\"='$teacher_id'"
