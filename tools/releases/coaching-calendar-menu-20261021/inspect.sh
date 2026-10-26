set -euo pipefail
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.Config.Image}} {{.Image}} {{index .Config.Labels "com.docker.compose.project.config_files"}}'; done
ls -d /var/lib/eduivme/releases/coaching-decision-work-20261020/source/admin-panel
