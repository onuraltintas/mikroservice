#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-completion-20261013-ac014834
[[ "$PWD" == "$release" ]]
test -s deployed.txt; test -s legacy-routes-deployed.txt
started=$(<deployed.txt)
date -u +%FT%TZ
for service in identity-service notification-service coaching-service; do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
  [[ "$ip" =~ ^[0-9.]+$ ]]
  printf '%s readiness=' "$service"
  curl -fsS --max-time 10 "http://$ip:8080/health/ready"
  printf '\n'
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.RestartCount}}') == 0 ]]
  docker logs --since "$started" "eduivme-production-$service-1" > "$service-monitor.log" 2>&1
  errors=$(grep -Ec '\[(ERR|FTL)\]|fail:|Unhandled exception|Coaching reminders could not' "$service-monitor.log" || true)
  printf '%s log_errors=%s\n' "$service" "$errors"
  [[ "$errors" == 0 ]]
done
identity_ip=$(docker inspect eduivme-production-identity-service-1 --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
[[ "$identity_ip" =~ ^[0-9.]+$ ]]
jq -er '"header = " + ("X-Internal-Service-Key: " + (.INTERNAL_SERVICE_API_KEY // .Internal__ServiceApiKey) | @json)' coaching-service.environment.json |
  curl -fsS --max-time 10 --config - -H 'Content-Type: application/json' \
    --data '{"institutionId":"00000000-0000-0000-0000-000000000001","studentId":"00000000-0000-0000-0000-000000000002"}' \
    "http://$identity_ip:8080/api/internal/coaching/institution-student" > identity-membership-probe.json
jq -e '.isActive == false' identity-membership-probe.json >/dev/null
echo 'Identity institution-student contract=200/inactive for absent fixture IDs'
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY 1' > coaching-smoke-history.txt
diff -u expected-production-coaching-history.txt coaching-smoke-history.txt
triggers=$(docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ($$lock_session_scheduling$$,$$guard_session_overlap$$,$$guard_attendance_overlap$$)')
[[ "$triggers" == 4 ]]
tables=$(docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT count(*) FROM information_schema.tables WHERE table_schema=$$coaching$$ AND table_name IN ($$fee_agreements$$,$$fee_installments$$,$$fee_payments$$,$$reminder_receipts$$)')
[[ "$tables" == 4 ]]
echo 'Coaching migration history exact; overlap triggers=4 and finance/reminder tables=4'
for service in admin-panel staff-portal; do
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.State.Health.Status}}') == healthy ]]
  [[ $(docker inspect "eduivme-production-$service-1" --format '{{.RestartCount}}') == 0 ]]
  printf '%s healthy/restarts=0\n' "$service"
done
for path in /api/coaching/study-planning/plans /api/coaching/finance/me /api/coaching/finance/receivables /api/coaching-admin/finance/receivables /api/coaching/staff/students/00000000-0000-0000-0000-000000000001/study/plans /api/coaching-admin/institutions; do
  code=$(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' "https://eduivme.com$path")
  printf '%s anonymous=%s\n' "$path" "$code"
  [[ "$code" == 401 ]]
done
for url in 'https://eduivme.com/coaching-portal/finance' 'https://eduivme.com/dashboard/coaching/finance' 'https://onuraltintas.net/staff/?product=coaching' 'https://masterhizliokuma.com/auth/login'; do
  [[ $(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' "$url") == 200 ]]
  printf '%s public=200\n' "$url"
done
for path in /student/coaching /coaching /coaching/dashboard; do
  [[ $(curl -sS --max-time 20 -o /dev/null -w '%{http_code}' "https://masterhizliokuma.com$path") == 302 ]]
  printf '%s legacy=302\n' "$path"
done
docker ps --format '{{.Names}} {{.ID}} {{.Image}}' | sort > containers-smoke.txt
pattern='^eduivme-production-(identity-service|notification-service|coaching-service|admin-panel|staff-portal)-1 '
diff -u <(grep -Ev "$pattern" containers-before.txt) <(grep -Ev "$pattern" containers-smoke.txt)
echo 'Live infrastructure, public routes and anonymous access checks passed; authenticated role workflows require separate evidence.'
