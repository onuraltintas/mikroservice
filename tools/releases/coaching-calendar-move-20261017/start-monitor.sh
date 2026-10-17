set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017
test -s deployed.txt
test ! -e monitor-started.txt
nohup bash monitor.sh > monitor-command.log 2>&1 </dev/null &
date -u +%FT%TZ > monitor-started.txt
echo 'Fifteen-minute production health observation started.'
