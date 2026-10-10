#!/usr/bin/env bash
# Run inside the checked, unpacked release directory. No image/compose changes.
set -Eeuo pipefail
umask 077
release_dir="$(pwd -P)"
case "$release_dir" in /var/lib/eduivme/releases/adult-programs-v2-20261010-*) ;; *) echo "Unexpected release directory" >&2; exit 1;; esac
api=eduivme-production-speed-reading-service-1
stopped=0
committed=0
on_exit() {
  status=$?
  if [ "$stopped" = 1 ] && [ "$committed" = 0 ]; then docker start "$api" >/dev/null; fi
  if [ "$status" != 0 ] && [ "$committed" = 1 ]; then
    if [ "$stopped" = 0 ]; then docker stop --time 30 "$api" >/dev/null; fi
    echo "Post-commit verification failed; API remains stopped for data-preserving recovery" >&2
  fi
  exit "$status"
}
trap on_exit EXIT
psql_run() { docker exec -i postgres psql -U eduplatform -d speedreading_owned_db -v ON_ERROR_STOP=1 "$@"; }
test "$(docker inspect -f '{{.State.Running}}' "$api")" = true
docker ps -a --format '{{.Names}}|{{.ID}}|{{.Image}}' | sort > containers-before.txt
docker inspect -f '{{.Image}}' "$api" > api-image-before.txt
curl --fail --silent --show-error --max-time 15 http://172.31.0.7:8080/health/ready | grep -qx Healthy
docker stop --time 30 "$api" >/dev/null
stopped=1
psql_run < tools/releases/adult-programs-v2-20261010/preflight.sql
docker exec postgres pg_dump -U eduplatform -d speedreading_owned_db -Fc > speedreading-before.dump
test -s speedreading-before.dump
docker exec -i postgres pg_restore --list < speedreading-before.dump > backup-list.txt
sha256sum speedreading-before.dump > backup-sha256.txt
psql_run -qAt < tools/releases/adult-programs-v2-20261010/inventory.sql > inventory-before.txt
psql_run -v plan_json="$(cat infrastructure/data/adult-program-plan-v2.json)" < content-packs/adult-programs/v2/apply.sql
committed=1
psql_run < tools/releases/adult-programs-v2-20261010/verify.sql
psql_run -qAt < tools/releases/adult-programs-v2-20261010/inventory.sql > inventory-after.txt
cmp inventory-before.txt inventory-after.txt
docker start "$api" >/dev/null
stopped=0
for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error --max-time 5 http://172.31.0.7:8080/health/ready 2>/dev/null | grep -qx Healthy; then break; fi
  if [ "$attempt" = 30 ]; then echo "API readiness failed" >&2; exit 1; fi
  sleep 2
done
docker ps -a --format '{{.Names}}|{{.ID}}|{{.Image}}' | sort > containers-after.txt
cmp containers-before.txt containers-after.txt
test "$(cat api-image-before.txt)" = "$(docker inspect -f '{{.Image}}' "$api")"
test "$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 20 https://masterhizliokuma.com/)" = 200
test "$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 20 https://masterhizliokuma.com/api/speed-reading/student-program/my-programs)" = 401
date --iso-8601=seconds > published-at.txt
printf 'Published: '; cat published-at.txt
printf 'Preserved table fingerprints: '; wc -l < inventory-after.txt
cat backup-sha256.txt
stat -c 'Backup: %s bytes, mode %a' speedreading-before.dump
