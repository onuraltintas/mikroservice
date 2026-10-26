base_release=$release
release=/var/lib/eduivme/releases/coaching-wizard-coverage-20261019
tag=coaching-wizard-coverage-20261019
cd "$release"
portals=(admin-panel staff-portal)
stable_others(){
 while read -r name id;do
  case "$name" in eduivme-production-admin-panel-1|eduivme-production-staff-portal-1)continue;;esac
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
case "${1:-}" in
prepare)
 test ! -e prepared.txt
 sha256sum -c payload.sha256 > payload-verified.log
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 for service in "${portals[@]}";do
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"$base_release/built-$service-image.txt")" ]]
  docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
 done
 cp "$base_release/production.compose.json" rollback.compose.json
 jq --arg tag "$tag" '.services["admin-panel"].image="eduivme/admin-panel:"+$tag|.services["staff-portal"].image="eduivme/staff-portal:"+$tag' rollback.compose.json > production.compose.json
 for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup";done
 protected coaching_db > protected.before.txt
 accounts > accounts.before.txt
 notifications > notifications.before.txt
 date -u +%FT%TZ > prepared.txt
 echo 'Exact portal bases, backups and fingerprints prepared; backend remains unchanged.'
 ;;
build)
 test -s prepared.txt;test ! -e built.txt
 sha256sum -c payload.sha256 > payload-verified.log
 for service in "${portals[@]}";do
  docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
  docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
 done
 stable_others;date -u +%FT%TZ > built.txt
 echo 'Both portal images built from exact live bases.'
 ;;
promote)
 test -s built.txt;test ! -e deployed.txt
 for service in "${portals[@]}";do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"previous-$service-image.txt")" ]];done
 stable_others
 recover(){ local status=$?;trap - EXIT;if ((status!=0));then for service in "${portals[@]}";do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" >> recovery.log 2>&1;done;fi;exit "$status";};trap recover EXIT
 for service in "${portals[@]}";do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1;ready "$service";done
 protected coaching_db > protected.after.txt;cmp protected.before.txt protected.after.txt
 accounts > accounts.after.txt;cmp accounts.before.txt accounts.after.txt
 notifications > notifications.after.txt;cmp notifications.before.txt notifications.after.txt
 stable_others;date -u +%FT%TZ > deployed.txt;trap - EXIT
 echo 'Both portals deployed; backend containers and protected records unchanged.'
 ;;
verify)
 test -s deployed.txt
 for service in "${portals[@]}";do ready "$service";[[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]];done
 stable_others;echo 'Portal readiness and exact image checks passed.'
 ;;
*)exit 2;;
esac
