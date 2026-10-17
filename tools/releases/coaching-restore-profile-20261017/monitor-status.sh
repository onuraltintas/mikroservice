set -euo pipefail
cd /var/lib/eduivme/releases/coaching-restore-profile-20261017
printf 'health_samples='
grep -Ec '^20[0-9]{2}-' monitor.log
if test -s monitor-verified.txt; then cat monitor-verified.txt; else tail -9 monitor.log; fi
