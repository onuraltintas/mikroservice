set -euo pipefail
umask 077
folder=/var/lib/eduivme/releases/coaching-submission-20261017/grade-verification
mkdir -p "$folder"
test ! -s "$folder/business.before.txt"
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > "$folder/coaching.before.backup"
docker exec postgres pg_dump -U "$db_user" -d notification_db -Fc > "$folder/notification.before.backup"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > "$folder/business.before.txt"
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name) FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE' AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','assignments','assignment_students') ORDER BY table_name
\gexec
SELECT 'assignments',id,md5((to_jsonb(t)-CASE WHEN id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' THEN '{Version,updated_at}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignments t;
SELECT 'assignment_students',id,md5((to_jsonb(t)-CASE WHEN assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' THEN '{status,score,teacher_feedback,updated_at}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignment_students t;
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db <<'SQL' > "$folder/notifications.before.txt"
SELECT "Id",md5(to_jsonb(n)::text) FROM public."Notifications" n ORDER BY "Id";
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM coaching.assignment_students WHERE assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND status='Submitted' AND score IS NULL) THEN RAISE EXCEPTION 'Test submission is not awaiting grading'; END IF; END $$;
SQL
date -u +%FT%TZ > "$folder/started.txt"
