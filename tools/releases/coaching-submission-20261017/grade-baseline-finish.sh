set -euo pipefail
folder=/var/lib/eduivme/releases/coaching-submission-20261017/grade-verification
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db <<'SQL' > "$folder/notifications.before.txt"
SELECT "Id",md5(to_jsonb(n)::text) FROM public."Notifications" n ORDER BY "Id";
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND status='Submitted' AND score IS NULL) THEN RAISE EXCEPTION 'Test submission is not awaiting grading'; END IF; END $$;
SQL
date -u +%FT%TZ > "$folder/started.txt"
wc -l "$folder/business.before.txt" "$folder/notifications.before.txt"
