set -euo pipefail
for service in admin-panel staff-portal coaching-service; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} {{.State.Status}} {{.Config.Image}} restarts={{.RestartCount}}'
done
docker inspect eduivme-production-staff-portal-1 --format '{{.Image}}' | xargs docker image inspect --format 'workdir={{.Config.WorkingDir}} user={{.Config.User}} command={{json .Config.Cmd}} entrypoint={{json .Config.Entrypoint}} ports={{json .Config.ExposedPorts}} volumes={{json .Config.Volumes}}'
cat /var/lib/eduivme/releases/coaching-practice-adaptation-20261020/staff-portal.Dockerfile
test -s /var/lib/eduivme/releases/coaching-student-design-20261020-r7/deployed.txt
