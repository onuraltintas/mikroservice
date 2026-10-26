set -euo pipefail
cd /var/lib/eduivme/releases/coaching-tracking-20261020
if test -f fixture-seed.log; then head -18 fixture-seed.log; fi
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "TeacherUserId","StudentUserId","InstitutionId","IsActive" FROM coaching.teacher_student_assignments WHERE "StudentUserId"='"'473bd2d6-e3db-47c4-be56-eeedec0022ed'"';'
