set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-submission-20261017
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock;flock -n 8
test ! -e deployed.txt
[[ $(sha256sum release.tar.gz|cut -d' ' -f1) == 63602338133abe5583b2418b885d2a40a48e922f3fda91ae905ce5aa03380299 ]]
tar -xzf release.tar.gz;sha256sum -c payload.sha256 > payload-verified.txt
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(coaching-service staff-portal)
[[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == sha256:7a061bb3fe773d192231bcc60a316aa51ce840251c095bb04f2d19f8867573e5 ]]
[[ $(docker inspect eduivme-production-staff-portal-1 --format '{{.Image}}') == sha256:f851d2f6d5a491b56bdffc57c8caf598fa42227ff969fcac743d592423dc85d7 ]]
"${compose[@]}" -f /var/lib/eduivme/releases/coaching-followup-20261017/production.compose.json config --format json > frozen.compose.json
jq '.services["coaching-service"].image="sha256:7a061bb3fe773d192231bcc60a316aa51ce840251c095bb04f2d19f8867573e5"|.services["staff-portal"].image="sha256:f851d2f6d5a491b56bdffc57c8caf598fa42227ff969fcac743d592423dc85d7"' frozen.compose.json > rollback.compose.json
jq '.services["coaching-service"].image="eduivme/coaching-service:coaching-submission-20261017"|.services["staff-portal"].image="eduivme/staff-portal:coaching-submission-20261017"' frozen.compose.json > production.compose.json
docker ps --format '{{.Names}} {{.ID}}' > containers.before.txt
for service in "${services[@]}"; do
 base=$(docker inspect "eduivme-production-$service-1" --format '{{.Image}}')
 docker tag "$base" "eduivme/$service:coaching-submission-base-20261017"
 docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:coaching-submission-base-20261017" --build-arg SOURCE_REVISION=2bc1cf94 -f "$service.Dockerfile" -t "eduivme/$service:coaching-submission-20261017" . > "$service-build.log" 2>&1
done
db_user=$(docker exec postgres printenv POSTGRES_USER)
fingerprint(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name) FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE' AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name
\gexec
SQL
}
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching.before-promotion.backup
fingerprint > rows.before.txt
ready(){
 local service=$1 port=8080 path=/health/ready ip
 [[ "$service" != staff-portal ]] || { port=80;path=/; }
 for attempt in $(seq 1 40); do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if curl -fsS --max-time 3 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done
 return 1
}
rollback(){
 status=$?;trap - EXIT
 if ((status!=0));then for service in "${services[@]}";do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1;done;fi
 exit "$status"
}
trap rollback EXIT
for service in "${services[@]}";do
 "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
 ready "$service"
 [[ $(docker inspect "eduivme-production-$service-1" --format '{{.State.Running}} {{.RestartCount}}') == 'true 0' ]]
done
fingerprint > rows.after.txt;cmp rows.before.txt rows.after.txt
while read -r name id;do case "$name" in eduivme-production-coaching-service-1|eduivme-production-staff-portal-1)continue;;esac;[[ $(docker inspect "$name" --format '{{.Id}}') == "$id"* ]];done < containers.before.txt
date -u +%FT%TZ > deployed.txt;trap - EXIT
cat deployed.txt
for service in "${services[@]}";do docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}';done
