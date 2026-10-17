set -euo pipefail
for service in coaching-service notification-service staff-portal admin-panel; do
 name="eduivme-production-$service-1"
 docker inspect "$name" --format '{{.Name}} image={{.Image}} revision={{index .Config.Labels "org.opencontainers.image.revision"}}'
 docker exec "$name" sh -c 'find /app -maxdepth 1 -name "*Contracts*" -o -name "*Notification*"' 2>/dev/null || true
done
printf 'compose_labels\n'
docker inspect eduivme-production-notification-service-1 --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}'
