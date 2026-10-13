#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-completion-20261013-ac014834
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock
flock -n 9
test -s deployed.txt
edge=eduivme-production-speed-reading-edge-1
config=/opt/eduivme/infrastructure/caddy/Caddyfile.speed-reading.litespeed
if test -e speed-edge.before.caddy; then cmp -s "$config" speed-edge.before.caddy; else cp "$config" speed-edge.before.caddy; fi
python3 - <<'PY'
from pathlib import Path
source = Path('speed-edge.before.caddy').read_text()
anchor = '    @legacyTeacher path /teacher /teacher/*\n'
assert source.count(anchor) == 1
assert '@legacyCoachingStaff' not in source
routes = '''    @legacyCoachingStudent path /student/coaching /student/coaching/
    redir @legacyCoachingStudent https://eduivme.com/coaching-portal 302

    @legacyCoachingStaff path /coaching /coaching/*
    redir @legacyCoachingStaff https://onuraltintas.net/staff/?product=coaching 302

'''
Path('speed-edge.after.caddy').write_text(source.replace(anchor, routes + anchor))
PY
docker cp speed-edge.after.caddy "$edge:/tmp/coaching-caddy-candidate"
docker exec "$edge" caddy validate --config /tmp/coaching-caddy-candidate --adapter caddyfile > speed-edge-validation.log 2>&1
cmp -s "$config" speed-edge.before.caddy
rollback() {
  status=$?; trap - EXIT
  if [[ "$status" != 0 ]]; then
    cat speed-edge.before.caddy > "$config"
    docker cp speed-edge.before.caddy "$edge:/tmp/coaching-caddy-rollback"
    if ! docker exec "$edge" caddy reload --config /tmp/coaching-caddy-rollback --adapter caddyfile > speed-edge-rollback.log 2>&1; then
      echo 'CRITICAL: edge configuration rollback failed.' >&2; exit 70
    fi
    cmp -s "$config" speed-edge.before.caddy || exit 70
  fi
  exit "$status"
}
trap rollback EXIT
# Preserve the bind-mounted inode so reload reads the updated configuration.
cat speed-edge.after.caddy > "$config"
docker exec "$edge" caddy reload --config /tmp/coaching-caddy-candidate --adapter caddyfile > speed-edge-reload.log 2>&1
curl -sS --max-time 10 -D speed-edge-direct.headers -o /dev/null http://127.0.0.1:5201/student/coaching
grep -Eq '^HTTP/[0-9.]+ 302' speed-edge-direct.headers
checkRedirect() {
  local path=$1 destination=$2
  for attempt in $(seq 1 12); do
    curl -sS --max-time 20 -H 'Cache-Control: no-cache' -D redirect.headers -o /dev/null "https://masterhizliokuma.com$path"
    tr -d '\r' < redirect.headers > redirect.normalized
    if grep -Eq '^HTTP/[0-9.]+ 302' redirect.normalized && grep -Fxi "location: $destination" redirect.normalized; then return 0; fi
    sleep 2
  done
  return 1
}
for path in /student/coaching /student/coaching/; do checkRedirect "$path" https://eduivme.com/coaching-portal; done
for path in /coaching /coaching/dashboard /coaching/students; do checkRedirect "$path" 'https://onuraltintas.net/staff/?product=coaching'; done
[[ $(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' https://masterhizliokuma.com/auth/login) == 200 ]]
sha256sum speed-edge.before.caddy speed-edge.after.caddy > speed-edge-config.sha256
date -u +%FT%TZ > legacy-routes-deployed.txt
trap - EXIT
