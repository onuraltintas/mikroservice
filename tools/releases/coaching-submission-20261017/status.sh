set -euo pipefail
cd /var/lib/eduivme/releases/coaching-submission-20261017
for file in coaching-service-build.log staff-portal-build.log coaching-service-promotion.log staff-portal-promotion.log deployed.txt;do if test -e "$file";then printf '%s\n' "$file";tail -n 5 "$file";fi;done
for service in coaching-service staff-portal;do docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}} image={{.Image}}';done
