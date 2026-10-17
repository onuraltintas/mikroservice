set -euo pipefail
cd /var/lib/eduivme/releases/coaching-followup-20261017
printf 'samples='
grep -Ec '^20[0-9]{2}-' monitor.log
tail -n 9 monitor.log
if test -s monitor-verified.txt; then cat monitor-verified.txt; else echo 'monitor pending'; fi
