set -euo pipefail
umask 077
folder=/var/lib/eduivme/releases/coaching-followup-20261017/submission-verification
db_user=$(docker exec postgres printenv POSTGRES_USER)
fingerprint() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name) FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE' AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','assignments','assignment_students') ORDER BY table_name
\gexec
SELECT 'assignments',id,md5((to_jsonb(t)-CASE WHEN id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' THEN '{Version,updated_at}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignments t;
SELECT 'assignment_students',id,md5((to_jsonb(t)-CASE WHEN assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' THEN '{status,submitted_at,student_note}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignment_students t;
SQL
}
case "${1:-}" in
 baseline)
 test ! -e "$folder"; mkdir -p "$folder";cd "$folder"
 for database in coaching_db notification_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.backup";done
 fingerprint > protected.before.txt
 date -u +%FT%TZ > started.txt
 ;;
 verify)
 cd "$folder";fingerprint > protected.after.txt;cmp protected.before.txt protected.after.txt
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('status',s.status,'submitted',s.submitted_at IS NOT NULL,'note',s.student_note,'teacher',a.teacher_id,'pending',count(*) FILTER(WHERE s.status IN ('Assigned','InProgress') AND s.submitted_at IS NULL)) FROM coaching.assignment_students s JOIN coaching.assignments a ON a.id=s.assignment_id WHERE s.assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND s.student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' GROUP BY s.status,s.submitted_at,s.student_note,a.teacher_id;
SQL
 date -u +%FT%TZ > verified.txt
 ;;
 *) exit 2;;
esac
