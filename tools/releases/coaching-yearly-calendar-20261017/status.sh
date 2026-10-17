set -euo pipefail
for name in coaching-service admin-panel; do
  docker inspect "eduivme-production-$name-1" --format '{{.Name}} {{.Image}} running={{.State.Running}} restarts={{.RestartCount}}'
done
docker image inspect $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') --format '{{json .Config.Labels}}'
docker image inspect $(docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}') --format '{{json .Config.Labels}}'
