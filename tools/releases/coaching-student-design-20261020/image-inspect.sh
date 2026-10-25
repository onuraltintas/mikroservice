set -euo pipefail
image=$(cat /var/lib/eduivme/releases/coaching-student-design-20261020-r2/built-admin-panel.txt)
docker image inspect "$image" --format 'workdir={{.Config.WorkingDir}} user={{.Config.User}} command={{json .Config.Cmd}} entrypoint={{json .Config.Entrypoint}} ports={{json .Config.ExposedPorts}}'
docker image inspect node:24-alpine --format '{{.Id}}'
cat /var/lib/eduivme/releases/coaching-practice-adaptation-20261020/admin-panel.Dockerfile
df -h /var/lib/docker
