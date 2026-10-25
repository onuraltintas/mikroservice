set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-yks-20261019-r3
db_user=$(docker exec postgres printenv POSTGRES_USER)
test -s e2e-setup-before.json
test ! -e e2e-restored.txt
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-e2e-cleanup.backup
{
echo 'BEGIN; CREATE TEMP TABLE saved_setup(document jsonb);'
encoded=$(base64 -w0 e2e-setup-before.json)
printf "INSERT INTO saved_setup VALUES(convert_from(decode('%s','base64'),'UTF8')::jsonb);\n" "$encoded"
cat <<'SQL'
CREATE TEMP TABLE saved_context AS SELECT c.* FROM saved_setup s CROSS JOIN LATERAL jsonb_populate_recordset(NULL::coaching.student_learning_contexts,s.document->'context')c;
DO $$ BEGIN
 IF (SELECT count(*) FROM saved_context)<>1 THEN RAISE EXCEPTION 'Invalid saved context'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.student_learning_contexts c JOIN saved_context s ON c."Id"=s."Id" AND c."StudentUserId"=s."StudentUserId" WHERE c."Purpose"=2 AND c."PrimaryScoreType"='TYT') THEN RAISE EXCEPTION 'Unexpected test scope; do not overwrite'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3' AND "IsActive" AND "Status"=1) THEN RAISE EXCEPTION 'Original active plan changed'; END IF;
END $$;
LOCK TABLE coaching.student_learning_contexts,coaching.student_course_preferences,coaching.student_topic_states,coaching.study_weekly_drafts,coaching.study_availability IN SHARE ROW EXCLUSIVE MODE;
DELETE FROM coaching.student_course_preferences WHERE "ContextId" IN(SELECT "Id" FROM saved_context);
DELETE FROM coaching.student_topic_states WHERE "ContextId" IN(SELECT "Id" FROM saved_context);
DELETE FROM coaching.study_weekly_drafts WHERE "StudentId" IN(SELECT "StudentUserId" FROM saved_context);
DELETE FROM coaching.study_availability WHERE "StudentId" IN(SELECT "StudentUserId" FROM saved_context);
DELETE FROM coaching.student_learning_contexts WHERE "Id" IN(SELECT "Id" FROM saved_context);
INSERT INTO coaching.student_learning_contexts SELECT * FROM saved_context;
INSERT INTO coaching.student_course_preferences SELECT x.* FROM saved_setup s CROSS JOIN LATERAL jsonb_populate_recordset(NULL::coaching.student_course_preferences,coalesce(nullif(s.document->'courses','null'::jsonb),'[]'::jsonb))x;
INSERT INTO coaching.student_topic_states SELECT x.* FROM saved_setup s CROSS JOIN LATERAL jsonb_populate_recordset(NULL::coaching.student_topic_states,coalesce(nullif(s.document->'topics','null'::jsonb),'[]'::jsonb))x;
INSERT INTO coaching.study_weekly_drafts SELECT x.* FROM saved_setup s CROSS JOIN LATERAL jsonb_populate_recordset(NULL::coaching.study_weekly_drafts,coalesce(nullif(s.document->'weekly','null'::jsonb),'[]'::jsonb))x;
INSERT INTO coaching.study_availability SELECT x.* FROM saved_setup s CROSS JOIN LATERAL jsonb_populate_recordset(NULL::coaching.study_availability,coalesce(nullif(s.document->'availability','null'::jsonb),'[]'::jsonb))x;
UPDATE coaching.study_plan_revisions SET "Status"=2,"IsActive"=false,"UpdatedAt"=now(),"Version"="Version"+1 WHERE "Id"='828f80e4-a4ce-4f3e-8447-d5f61378d716' AND "Status"=0 AND NOT "IsActive";
UPDATE coaching.study_plan_revisions SET "Status"=2,"IsActive"=false,"UpdatedAt"=now(),"Version"="Version"+1 WHERE "Title"='YKS canlı doğrulama · öğretmen TYT · 19.10.2026' AND "StudentId" IN(SELECT "StudentUserId" FROM saved_context) AND "Status"=0 AND NOT "IsActive";
COMMIT;
SELECT 'Original LGS setup restored; YKS test drafts archived; active LGS plan retained.';
SQL
} | docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db
date -u +%FT%TZ > e2e-restored.txt
