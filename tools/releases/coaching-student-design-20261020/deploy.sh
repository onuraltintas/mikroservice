set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-student-design-20261020
base=/var/lib/eduivme/releases/coaching-weekly-followup-20261020
tag=coaching-student-design-20261020
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
services=(admin-panel staff-portal)
baseline(){ for s in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$(<"previous-$s.txt")" ]]; done; }
stable(){ while read -r name id; do case "$name" in eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac; [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]; done < containers.before.txt; }
ready(){ local s=$1 port=4000 url=/ ip; [[ "$s" != staff-portal ]] || { port=80; url=/staff/; }; for i in $(seq 1 30); do ip=$(docker inspect "eduivme-production-$s-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1); if curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$url" >/dev/null; then return; fi; sleep 2; done; return 1; }
case "${1:-}" in
prepare)
 test ! -e prepared.txt; sha256sum -c package.sha256 > package-verified.log
 docker ps --format '{{.Names}} {{.ID}}' --no-trunc > containers.before.txt
 cp "$base/production.compose.json" rollback.compose.json
 for s in "${services[@]}"; do
  docker inspect "eduivme-production-$s-1" --format '{{.Image}}' > "previous-$s.txt"
  jq --arg s "$s" --arg i "$(<"previous-$s.txt")" '.services[$s].image=$i|.services[$s].pull_policy="never"' rollback.compose.json > next.json; mv next.json rollback.compose.json
 done
 jq --arg tag "$tag" '.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
 "${compose[@]}" -f production.compose.json config --quiet
 bash history.sh before
 mkdir source; cp -a "$base/source/admin-panel" source/admin-panel; cp -a "$base/source/shared" source/shared
 mkdir source/clients; ln -s ../admin-panel source/clients/admin-panel
 docker run --rm --memory=2g --network none -v "$release:/release" -w /release/source node:24-alpine node ../transform.cjs > transform.log 2>&1
 docker run --rm --memory=2g --network none -v "$release:/release" -w /release/source node:24-alpine node ../feedback.cjs >> transform.log 2>&1
 docker run --rm --memory=2g --network none -v "$release:/release" -w /release/source node:24-alpine node ../finalize-source.cjs >> transform.log 2>&1
 cp -a overlay/. source/admin-panel/; cp build-portals.cjs source/admin-panel/
 date -u +%FT%TZ > prepared.txt
 ;;
build)
 test -s prepared.txt; test ! -e built.txt; baseline
 docker run --rm --name coaching-student-design-builder-20261020 --memory=4g --cpus=2 --network none -v "$release/source/admin-panel:/work/admin-panel" -v "$release/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > build.log 2>&1
 mkdir -p payload/admin payload/staff; cp -a source/admin-panel/dist/admin-panel/. payload/admin/; cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
 for s in "${services[@]}"; do
  cp "/var/lib/eduivme/releases/coaching-practice-adaptation-20261020/$s.Dockerfile" "$s.Dockerfile"
  docker tag "$(<"previous-$s.txt")" "eduivme/$s:$tag-base"
  docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$s:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$s.Dockerfile" -t "eduivme/$s:$tag" . > "$s-build.log" 2>&1
  docker image inspect "eduivme/$s:$tag" --format '{{.Id}}' > "built-$s.txt"
 done
 stable; date -u +%FT%TZ > built.txt
 ;;
promote)
 test -s built.txt; test ! -e deployed.txt; baseline; stable
 recover(){ local code=$?; trap - EXIT; if ((code!=0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "${services[@]}" > rollback.log 2>&1 || true; fi; exit "$code"; }; trap recover EXIT
 date -u +%FT%TZ > promotion-start.txt
 for s in "${services[@]}"; do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$s" > "$s-promote.log" 2>&1; ready "$s"; done
 stable; bash history.sh after; bash history.sh compare > preservation.txt
 date -u +%FT%TZ > deployed.txt; trap - EXIT
 ;;
verify)
 test -s deployed.txt
 for s in "${services[@]}"; do ready "$s"; [[ $(docker inspect "eduivme-production-$s-1" --format '{{.Image}}') == "$(<"built-$s.txt")" ]]; docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'; done
 stable; bash history.sh after; bash history.sh compare
 ;;
*) exit 2;;
esac
