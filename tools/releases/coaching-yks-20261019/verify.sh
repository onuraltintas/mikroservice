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
test -s deployed.txt
for service in "${services[@]}";do
 ready "$service"
 [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
done
unchanged
verify_data live verify