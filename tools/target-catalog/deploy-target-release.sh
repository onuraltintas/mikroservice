set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-targets-20261019
tag=coaching-targets-20261019
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
protected(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM coaching.%I t;',table_name,'','',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','AdminAuditRecords') ORDER BY table_name \gexec
COMMIT;
SQL
}
ready(){
 local service=$1 ip port=8080 path=/health/live
 if [[ "$service" == admin-panel ]];then port=4000;path=/;fi
 if [[ "$service" == staff-portal ]];then port=80;path=/staff/;fi
 for attempt in $(seq 1 30);do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]]&&curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done;return 1
}
stable_others(){
 while read -r name id;do
  case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1)continue;;esac
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
case "${1:-}" in
prepare)
 test ! -e prepared.txt
 sha256sum -c payload.sha256 > payload-verified.log
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 for service in "${services[@]}";do
  image=$(docker inspect "eduivme-production-$service-1" --format '{{.Config.Image}}')
  expected="eduivme/$service:coaching-wizard-coverage-20261019"
  if [[ "$service" == coaching-service ]];then expected='eduivme/coaching-service:coaching-yks-20261019-r3';fi
  [[ "$image" == "$expected" ]]
  docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
 done
 cp /var/lib/eduivme/releases/coaching-wizard-coverage-20261019/production.compose.json rollback.compose.json
 jq --arg tag "$tag" '.services["coaching-service"].image="eduivme/coaching-service:"+$tag|.services["admin-panel"].image="eduivme/admin-panel:"+$tag|.services["staff-portal"].image="eduivme/staff-portal:"+$tag' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup";done
 sha256sum *.before.backup > backups.sha256
 protected > protected.before.txt
 date -u +%FT%TZ > prepared.txt
 echo 'Exact image baselines, server-local backups and all coaching data fingerprints prepared.'
 ;;
build)
 test -s prepared.txt;test ! -e built.txt
 sha256sum -c payload.sha256 > payload-verified.log
 for service in "${services[@]}";do
  docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
 done
 stable_others;date -u +%FT%TZ > built.txt
 echo 'API and both portal images built from exact live bases.'
 ;;
promote)
 test -s built.txt;test ! -e deployed.txt
 for service in "${services[@]}";do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]];done
 stable_others
 recover(){ local status=$?;trap - EXIT;if ((status!=0));then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" >> recovery.log 2>&1;fi;exit "$status";};trap recover EXIT
 for service in "${services[@]}";do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1;ready "$service";done
 protected > protected.after.txt;cmp protected.before.txt protected.after.txt
 stable_others;date -u +%FT%TZ > deployed.txt;trap - EXIT
 echo 'API and both portals deployed. Every protected coaching table is unchanged; no migrations ran.'
 ;;
verify)
 test -s deployed.txt
 for service in "${services[@]}";do ready "$service";[[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]];done
 stable_others;echo 'All three exact images are running and healthy.'
 ;;
*)exit 2;;
esac
