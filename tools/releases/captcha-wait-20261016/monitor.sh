#!/usr/bin/env bash
set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/captcha-wait-20261016
deadline=$(( $(date -d "$(cat deployed.txt)" +%s) + 900 ))
while (( $(date +%s) < deadline )); do
  bash deploy.sh validate >> monitoring.log
  sleep 30
done
bash deploy.sh validate >> monitoring.log
date -u +%FT%TZ > monitoring-complete.txt
