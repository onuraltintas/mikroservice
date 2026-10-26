set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-yks-20261019-r3
drill=coaching_yks_drill_20261019_r3
tag=coaching-yks-20261019-r3
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock;flock -n 8
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
payload(){ sha256sum -c payload.sha256 > payload-verified.log; }
baseline_targets(){
 for service in "${services[@]}";do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]];done
}
unchanged(){
 while read -r name id;do
  case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1)continue;;esac
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
ready(){
 local service=$1 ip port=8080 path=/health/ready
 if [[ "$service" == admin-panel ]];then port=4000;path=/;fi
 if [[ "$service" == staff-portal ]];then port=80;path=/staff/;fi
 for attempt in $(seq 1 30);do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]]&&curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done;return 1
}
protected(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L||md5(to_jsonb(t)::text) FROM coaching.%I t;',table_name||'|',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT LIKE 'study_catalog_%' AND table_name NOT LIKE 'official_curriculum_%'
AND table_name NOT IN ('outcome_duration_estimates','AdminAuditRecords','InboxState','OutboxState','OutboxMessage','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name \gexec
SELECT 'lgs.lesson|'||md5(to_jsonb(l)::text) FROM coaching.study_catalog_lessons l WHERE "ExamCode"='LGS';
SELECT 'lgs.unit|'||md5(to_jsonb(u)::text) FROM coaching.study_catalog_units u JOIN coaching.study_catalog_lessons l ON l."Id"=u."LessonId" WHERE l."ExamCode"='LGS';
SELECT 'lgs.topic|'||md5(to_jsonb(t)::text) FROM coaching.study_catalog_topics t JOIN coaching.study_catalog_lessons l ON l."Id"=t."LessonId" WHERE l."ExamCode"='LGS';
SELECT 'lgs.edition|'||md5(to_jsonb(e)::text) FROM coaching.official_curriculum_editions e WHERE "ExamCode"='LGS';
SELECT 'lgs.official-topic|'||md5(to_jsonb(t)::text) FROM coaching.official_curriculum_topics t JOIN coaching.official_curriculum_editions e ON e."Id"=t."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.outcome|'||md5((to_jsonb(o)-'{SourceKind,SourceUrl}'::text[])::text) FROM coaching.study_catalog_outcomes o JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.estimate|'||md5(to_jsonb(d)::text) FROM coaching.outcome_duration_estimates d JOIN coaching.study_catalog_outcomes o ON o."Id"=d."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.mapping|'||md5(to_jsonb(m)::text) FROM coaching.study_catalog_topic_outcomes m JOIN coaching.study_catalog_outcomes o ON o."Id"=m."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.share|'||md5(to_jsonb(s)::text) FROM coaching.study_catalog_topic_outcome_shares s JOIN coaching.study_catalog_outcomes o ON o."Id"=s."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
COMMIT;
SQL
}
accounts(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name \gexec
SQL
}
notifications(){ docker exec postgres psql -X -qAt -U "$db_user" -d notification_db -c 'SELECT count(*),md5(coalesce(string_agg(md5(to_jsonb(n)::text),'"'"''"'"' ORDER BY "Id"),'"'"''"'"')) FROM public."Notifications" n'; }
verify_data(){
 export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID VERIFY_OPERATION
 VERIFY_SCOPE=$1;VERIFY_OPERATION=$2;VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json)
 if [[ "$1" == drill ]];then VERIFY_CONNECTION=$(jq -nr --arg c "$VERIFY_CONNECTION" --arg d "$drill" '$c|sub("Database=coaching_db;";"Database="+$d+";")');[[ "$VERIFY_CONNECTION" == *"Database=$drill;"* ]];fi
 VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
 [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
 docker run --rm --network "$(<network.txt)" --entrypoint sh -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -e VERIFY_OPERATION -v "$release/payload/verification:/verification:ro" "$(<built-coaching-service-image.txt)" -c 'mkdir /tmp/verify && cp /verification/Verification.* /tmp/verify/ && cp /verification/yks-2026-basis-v1.json /tmp/verify/ && cp /app/*.dll /tmp/verify/ && dotnet /tmp/verify/Verification.dll'
}
mkdir -p "$release/regression-20261019"
cd "$release/regression-20261019"
case "${1:-}" in
prepare)
 test ! -e prepared.txt
 for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup";done
 protected coaching_db > protected.before.txt
 accounts > accounts.before.txt
 notifications > notifications.before.txt
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db > original-plan.json <<'SQL'
SELECT to_jsonb(p) FROM coaching.study_plan_revisions p WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3' AND "IsActive" AND "Status"=1;
SQL
 jq -e '.Id=="a4cea155-5485-4db6-9f52-1ec4df8080c3"' original-plan.json >/dev/null
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db > setup-before.json <<'SQL'
WITH s AS (SELECT "StudentId" id FROM coaching.study_plan_revisions WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3'), c AS (SELECT x.* FROM coaching.student_learning_contexts x,s WHERE x."StudentUserId"=s.id)
SELECT json_build_object('context',(SELECT json_agg(c) FROM c),'courses',(SELECT json_agg(p) FROM coaching.student_course_preferences p,c WHERE p."ContextId"=c."Id"),'topics',(SELECT json_agg(t) FROM coaching.student_topic_states t,c WHERE t."ContextId"=c."Id"),'weekly',(SELECT json_agg(w) FROM coaching.study_weekly_drafts w,s WHERE w."StudentId"=s.id),'availability',(SELECT json_agg(a) FROM coaching.study_availability a,s WHERE a."StudentId"=s.id));
SQL
 jq -e '.context|length==1' setup-before.json >/dev/null
 date -u +%FT%TZ > prepared.txt
 echo 'Fresh backups and original plan/setup fingerprints prepared.'
 ;;
checkpoint)
 test -s prepared.txt
 encoded=$(base64 -w0 original-plan.json)
 {
 echo 'BEGIN; CREATE TEMP TABLE original_plan(document jsonb);'
 printf "INSERT INTO original_plan VALUES(convert_from(decode('%s','base64'),'UTF8')::jsonb);\n" "$encoded"
 cat <<'SQL'
DO $$ BEGIN
 IF (SELECT count(*) FROM coaching.study_plan_revisions WHERE "IsActive" AND "Title" LIKE 'RG26 %')<>1 THEN RAISE EXCEPTION 'Expected one published regression plan'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."IsActive" AND p."Title" LIKE 'RG26 %' AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id")) THEN RAISE EXCEPTION 'Published plan has no tasks'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."IsActive" AND p."Title" LIKE 'RG26 % automatic' AND NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks t JOIN coaching.study_plan_task_outcomes l ON l."TaskId"=t."Id" WHERE t."RevisionId"=p."Id")) THEN RAISE EXCEPTION 'Automatic plan has no outcome links'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."IsActive" AND p."Title" LIKE 'RG26 %-v2 automatic' AND (SELECT count(DISTINCT t."WeeklyLessonId") FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id")<>CASE split_part(p."Title",' ',2) WHEN 'LGS' THEN 6 WHEN 'TYT' THEN 9 WHEN 'SAY' THEN 13 WHEN 'EA' THEN 13 WHEN 'SOZ' THEN 16 WHEN 'DIL' THEN 10 END) THEN RAISE EXCEPTION 'Selected lesson missing from final automatic plan'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions p WHERE p."IsActive" AND p."Title" LIKE 'RG26 %-v2 automatic' AND (SELECT sum(t."PlannedMinutes") FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id")<>CASE split_part(p."Title",' ',2) WHEN 'LGS' THEN 11970 WHEN 'TYT' THEN 31048 WHEN 'SAY' THEN 51393 WHEN 'EA' THEN 46058 WHEN 'SOZ' THEN 42763 WHEN 'DIL' THEN 35998 END) THEN RAISE EXCEPTION 'Automatic scope minutes incomplete'; END IF;
END $$;
SELECT json_build_object('id',p."Id",'title',p."Title",'published',p."IsActive",'tasks',(SELECT count(*) FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id"),'links',(SELECT count(*) FROM coaching.study_plan_task_outcomes l JOIN coaching.study_plan_tasks t ON t."Id"=l."TaskId" WHERE t."RevisionId"=p."Id")) FROM coaching.study_plan_revisions p WHERE p."IsActive" AND p."Title" LIKE 'RG26 %';
UPDATE coaching.study_plan_revisions SET "Status"=2,"IsActive"=false,"UpdatedAt"=now(),"Version"="Version"+1 WHERE "IsActive" AND "Title" LIKE 'RG26 %';
DO $$ DECLARE columns_text text; BEGIN
 SELECT string_agg(quote_ident(column_name),',' ORDER BY ordinal_position) INTO columns_text FROM information_schema.columns WHERE table_schema='coaching' AND table_name='study_plan_revisions' AND column_name<>'Id';
 EXECUTE format('UPDATE coaching.study_plan_revisions SET (%1$s)=(SELECT %1$s FROM jsonb_populate_record(NULL::coaching.study_plan_revisions,(SELECT document FROM original_plan))) WHERE "Id"=''a4cea155-5485-4db6-9f52-1ec4df8080c3''',columns_text);
END $$;
COMMIT;
SQL
 } | docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db | tee -a published-results.jsonl
 ;;
verify)
 test -s prepared.txt
 protected coaching_db > protected.after.txt
 LC_ALL=C comm -23 protected.before.txt protected.after.txt > missing-business.txt
 test ! -s missing-business.txt
 accounts > accounts.after.txt;cmp accounts.before.txt accounts.after.txt
 test -s notification-rows.before.txt
 docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db -c 'SELECT "Id"::text||'"'"'|'"'"'||md5(to_jsonb(n)::text) FROM public."Notifications" n ORDER BY "Id"' | LC_ALL=C sort > notification-rows.after.txt
 LC_ALL=C comm -23 notification-rows.before.txt notification-rows.after.txt > missing-notifications.txt
 test ! -s missing-notifications.txt
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('regressionPlans',count(*),'archived',count(*) FILTER(WHERE "Status"=2),'distinctCases',count(DISTINCT "Title")) FROM coaching.study_plan_revisions WHERE "Title" LIKE 'RG26 %';
SELECT json_build_object('activeOriginalLgs',"IsActive",'originalTasks',(SELECT count(*) FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id"),'completed',(SELECT count(*) FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id" AND t."IsCompleted")) FROM coaching.study_plan_revisions p WHERE "Id"='a4cea155-5485-4db6-9f52-1ec4df8080c3';
SQL
 echo 'All pre-regression business rows, accounts and existing notifications preserved.'
 ;;
*)exit 2;;
esac
