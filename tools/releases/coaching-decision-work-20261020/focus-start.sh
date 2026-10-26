set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
bash -n focus-build.sh
nohup bash focus-build.sh > focus-driver.log 2>&1 </dev/null &
echo 'Linked-task focus portal build started.'
