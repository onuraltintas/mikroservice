set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
test -s deployed.txt
echo '8bd5c7bfa90db1c5589e48a0d3e10c55058e7a8c2076b35792c6f9617dd4f7fc  http-validation-fix.tar.gz' | sha256sum -c -
[[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$(<built-coaching-service.txt)" ]]
if test ! -d http-validation-fix; then
 mkdir -m 700 http-validation-fix
 tar -xzf http-validation-fix.tar.gz -C http-validation-fix
fi
cd http-validation-fix
sha256sum -c payload.sha256 > payload-verified.log
cat > Dockerfile <<'DOCKER'
ARG BASE_IMAGE
FROM ${BASE_IMAGE}
COPY *.dll *.pdb /app/
LABEL org.opencontainers.image.revision="coaching-decision-work-20261020-r2"
DOCKER
docker tag "$(<../built-coaching-service.txt)" eduivme/coaching-service:coaching-decision-work-validation-base
docker build --network none --pull=false --build-arg BASE_IMAGE=eduivme/coaching-service:coaching-decision-work-validation-base -t eduivme/coaching-service:coaching-decision-work-20261020-r2 . > build.log 2>&1
cd ..
jq '.services["coaching-service"].image="eduivme/coaching-service:coaching-decision-work-20261020-r2"' production.compose.json > fixed.compose.json
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
recover(){ local code=$?; trap - EXIT; if ((code!=0)); then "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never coaching-service admin-panel staff-portal > fix-rollback.log 2>&1 || true; fi; exit "$code"; }; trap recover EXIT
"${compose[@]}" -f fixed.compose.json up -d --no-deps --pull never coaching-service > fixed-promote.log 2>&1
for attempt in $(seq 1 30); do
 ip=$(docker inspect eduivme-production-coaching-service-1 --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
 if curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null 2>&1; then break; fi
 sleep 2
done
curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null
docker image inspect eduivme/coaching-service:coaching-decision-work-20261020-r2 --format '{{.Id}}' > built-coaching-service-fixed.txt
[[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$(<built-coaching-service-fixed.txt)" ]]
bash history.sh after; bash history.sh compare
date -u +%FT%TZ > validation-fixed.txt
trap - EXIT
echo 'HTTP record validation fix deployed; original business rows preserved; portals and schema unchanged.'
