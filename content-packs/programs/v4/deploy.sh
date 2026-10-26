#!/bin/sh
set -eu
umask 077
release=/var/lib/eduivme/releases/program-order-v4-20261013
service=eduivme-production-speed-reading-service-1
database=speedreading_owned_db
test "$(docker inspect --format '{{.State.Running}}' "$service")" = true
test -s "$release/backup-contents.txt"
cmp "$release/rehearsal-before.txt" "$release/rehearsal-after-rollback.txt"
cmp "$release/rehearsal-after-first.txt" "$release/rehearsal-after-second.txt"
grep -q '"Programs":20,"Days":560,"Slots":3640' "$release/rehearsal-audit.log"
docker ps -a --format '{{.Names}}|{{.ID}}|{{.Image}}' | sort > "$release/containers-before.txt"
applied=0
success=0
query() { docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$database" < "$release/$1"; }
finish() {
 status=$?
 trap - EXIT
 restart=1
 if [ "$success" -ne 1 ] && [ "$applied" -eq 1 ]; then
  if docker stop -t 30 "$service" > "$release/rollback-stop.log"; then
   if query rollback.sql > "$release/live-rollback.log"; then
    printf '%s\n' 'Verification failed; program/schedule patch rolled back'
   else
    restart=0
    status=1
    printf '%s\n' 'Guarded rollback refused; API remains stopped. Inspect live activity; never overwrite the whole database' >&2
   fi
  else
   restart=0
   status=1
   printf '%s\n' 'Could not stop API for rollback; no rollback attempted' >&2
  fi
 fi
 if [ "$restart" -eq 1 ] && [ "$(docker inspect --format '{{.State.Running}}' "$service")" != true ]; then
  docker start "$service" > "$release/start.log" || status=1
 fi
 rm -f "$release/audit.env"
 exit "$status"
}
trap finish EXIT
docker stop -t 30 "$service" > "$release/stop.log"
docker exec postgres pg_dump -U eduplatform -d "$database" -Fc > "$release/live-before.dump"
docker exec -i postgres pg_restore --list < "$release/live-before.dump" > "$release/live-backup-contents.txt"
sha256sum "$release/live-before.dump" > "$release/live-backup.sha256"
query inventory-protected.sql > "$release/live-protected-before.txt"
query apply.sql > "$release/live-apply.log"
applied=1
query verify.sql
query inventory-protected.sql > "$release/live-protected-after.txt"
cmp "$release/live-protected-before.txt" "$release/live-protected-after.txt"
query snapshot.sql > "$release/live-after.json"
node "$release/verify-release.mjs" "$release/snapshot.json" "$release/live-after.json"
docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$service" > "$release/audit.env"
image=$(docker inspect --format '{{.Config.Image}}' "$service")
docker run --rm --network eduplatform-production --env-file "$release/audit.env" "$image" --audit-training-programs > "$release/live-audit.log" 2>&1
grep -q '"Programs":20,"Days":560,"Slots":3640' "$release/live-audit.log"
docker ps -a --format '{{.Names}}|{{.ID}}|{{.Image}}' | sort > "$release/containers-after.txt"
cmp "$release/containers-before.txt" "$release/containers-after.txt"
docker start "$service" > "$release/start.log"
ready=0
attempt=0
while [ "$attempt" -lt 20 ]; do
 attempt=$((attempt + 1))
 address=$(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$service")
 if [ "$(docker inspect --format '{{.State.Running}}' "$service")" = true ] \
  && curl -fsS --max-time 3 "http://$address:8080/health/live" > "$release/health-live.txt" \
  && curl -fsS --max-time 3 "http://$address:8080/health/ready" > "$release/health-ready.txt"; then
  ready=1
  break
 fi
 sleep 2
done
test "$ready" -eq 1
success=1
printf '%s\n' 'PASS: live program patch, expected frozen schedules, protected data, 20/560/3640 runtime audit and live/ready health'
