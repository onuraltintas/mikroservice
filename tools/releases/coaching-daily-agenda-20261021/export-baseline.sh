set -euo pipefail
umask 077
preview=/var/lib/eduivme/release-previews/coaching-daily-agenda-20261021
test ! -e "$preview"
mkdir -p "$preview/baseline/Coaching"
for module in Coaching.API Coaching.Application Coaching.Infrastructure Coaching.Domain EduPlatform.Shared.Contracts; do
 for ext in dll pdb; do docker cp "eduivme-production-coaching-service-1:/app/$module.$ext" "$preview/baseline/Coaching/"; done
done
cd "$preview"
tar -czf baseline.tar.gz baseline
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.Config.Image}}'; done
sha256sum baseline.tar.gz
