set -euo pipefail
umask 077
directory=/var/lib/eduivme/releases/coaching-weekly-review-20261020-baseline
test ! -e "$directory"; mkdir -m 700 "$directory"
for module in Coaching.API Coaching.Application Coaching.Infrastructure Coaching.Domain; do
 docker cp "eduivme-production-coaching-service-1:/app/$module.pdb" "$directory/$module.pdb"
 docker exec eduivme-production-coaching-service-1 sha256sum "/app/$module.dll" "/app/$module.pdb"
done > "$directory/modules.sha256"
cat "$directory/modules.sha256"
