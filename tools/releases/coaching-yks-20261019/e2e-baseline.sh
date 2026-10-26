set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-yks-20261019-r3
test ! -e e2e-setup-before.json
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db > e2e-setup-before.json <<'SQL'
WITH s AS (SELECT "StudentId" id FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3'), c AS (SELECT x.* FROM coaching.student_learning_contexts x,s WHERE x."StudentUserId"=s.id)
SELECT json_build_object('context',(SELECT json_agg(c) FROM c),'courses',(SELECT json_agg(p) FROM coaching.student_course_preferences p,c WHERE p."ContextId"=c."Id"),'topics',(SELECT json_agg(t) FROM coaching.student_topic_states t,c WHERE t."ContextId"=c."Id"),'weekly',(SELECT json_agg(w) FROM coaching.study_weekly_drafts w,s WHERE w."StudentId"=s.id),'availability',(SELECT json_agg(a) FROM coaching.study_availability a,s WHERE a."StudentId"=s.id));
SQL
jq -e '.context|length==1' e2e-setup-before.json >/dev/null
echo 'Original test-student setup captured privately.'
