set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-outcomes-20261017
printf 'samples '; grep -Ec '^20[0-9]{2}-' monitor.log
tail -6 monitor.log
if test -s monitor-verified.txt; then printf 'verified '; cat monitor-verified.txt; else echo 'observation in progress'; fi
