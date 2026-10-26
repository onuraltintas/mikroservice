set -euo pipefail
umask 077
folder=/var/lib/eduivme/releases/coaching-submission-20261017/grade-verification
db_user=$(docker exec postgres printenv POSTGRES_USER)
started=$(cat "$folder/started.txt")
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > "$folder/business.after.txt"
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name) FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE' AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','assignments','assignment_students') ORDER BY table_name
\gexec
SELECT 'assignments',id,md5((to_jsonb(t)-CASE WHEN id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' THEN '{Version,updated_at}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignments t;
SELECT 'assignment_students',id,md5((to_jsonb(t)-CASE WHEN assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed' THEN '{status,score,teacher_feedback,updated_at}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.assignment_students t;
SQL
cmp "$folder/business.before.txt" "$folder/business.after.txt"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > "$folder/graded.json"
SELECT json_build_object('status',s.status,'score',s.score,'feedback',s.teacher_feedback,'submittedAtPreserved',s.submitted_at IS NOT NULL,'studentNotePreserved',s.student_note='Canlı teslim akışı doğrulaması · 17.10.2026. Test ödevi tamamlandı.','maxScore',a.max_score) FROM coaching.assignment_students s JOIN coaching.assignments a ON a.id=s.assignment_id WHERE s.assignment_id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND s.student_id='473bd2d6-e3db-47c4-be56-eeedec0022ed';
SQL
jq -e '.status=="Graded" and .score==95 and .feedback=="Ödevini tamamlayıp teslim ettin. Çözümün düzenli ve anlaşılır; bu çalışma biçimini sürdür." and .submittedAtPreserved and .studentNotePreserved and .maxScore==100' "$folder/graded.json" >/dev/null
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -v "started=$started" -d notification_db <<'SQL' > "$folder/notification.json"
SELECT json_build_object('gradeNotifications',count(*),'correctStudent',bool_and("UserId"='473bd2d6-e3db-47c4-be56-eeedec0022ed'),'correctAssignment',bool_and("RelatedEntityId"='c260e5c4-facd-42cd-af3a-69aa0b9cc67f'),'scoreAndFeedback',bool_and("Message"='Ödev puanın: 95. Geri bildirim: Ödevini tamamlayıp teslim ettin. Çözümün düzenli ve anlaşılır; bu çalışma biçimini sürdür.')) FROM public."Notifications" WHERE "Type"='AssignmentGraded' AND "RelatedEntityId"='c260e5c4-facd-42cd-af3a-69aa0b9cc67f' AND "CreatedAt">=:'started'::timestamptz;
SQL
jq -e '.gradeNotifications==1 and .correctStudent and .correctAssignment and .scoreAndFeedback' "$folder/notification.json" >/dev/null
date -u +%FT%TZ > "$folder/verified.txt"
cat "$folder/graded.json";cat "$folder/notification.json";echo 'All other Coaching business rows match the pre-grade snapshot.'
