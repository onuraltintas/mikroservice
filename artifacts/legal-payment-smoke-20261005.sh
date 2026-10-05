#!/usr/bin/env bash
set -euo pipefail
cd /var/lib/eduivme/releases/legal-payment-20261005
for service in coaching-service speed-reading-service admin-panel staff-portal speed-reading-frontend; do
  container="eduivme-production-${service}-1"
  test "$(docker inspect "$container" --format '{{.Config.Image}}')" = "eduivme/$service:legal-payment-20261005"
  test "$(docker inspect "$container" --format '{{.State.Running}}')" = true
  test "$(docker inspect "$container" --format '{{.RestartCount}}')" = 0
  printf '%s image/running/restarts OK\n' "$service"
done
for service in coaching-service speed-reading-service; do
  ip=$(docker inspect "eduivme-production-${service}-1" | jq -er '.[0].NetworkSettings.Networks["eduplatform-production"].IPAddress')
  curl --fail --silent --show-error --retry 5 --retry-delay 3 --max-time 10 "http://$ip:8080/health/ready"
  printf '\n%s readiness OK\n' "$service"
done
for url in \
  https://eduivme.com/ \
  https://eduivme.com/staff/ \
  https://onuraltintas.net/ \
  https://masterhizliokuma.com/ \
  https://onuraltintas.net/api/platform/legal-pages/coaching-newsletter-consent \
  https://masterhizliokuma.com/api/platform/legal-pages/speed-reading-newsletter-consent; do
  test "$(curl --silent --show-error --location --max-time 20 -o /dev/null -w '%{http_code}' "$url")" = 200
  printf '%s HTTP200\n' "$url"
done
for url in \
  https://onuraltintas.net/api/coaching/subscriptions/my-access \
  https://masterhizliokuma.com/api/speed-reading/bank-transfer/requests; do
  test "$(curl --silent --show-error --max-time 20 -o /dev/null -w '%{http_code}' "$url")" = 401
  printf '%s anonymous HTTP401\n' "$url"
done
docker exec postgres psql -U eduplatform -d coaching_db -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId"=$$20261005090416_RecordAdultPayerDeclaration$$' | grep -qx 1
docker exec postgres psql -U eduplatform -d speedreading_owned_db -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM speed_reading.__ef_migrations_history WHERE "MigrationId"=$$20261005091502_RecordAdultPayerDeclaration$$' | grep -qx 1
test -s coaching_db.production-before-migration.backup
test -s speedreading_owned_db.production-before-migration.backup
printf 'Both migrations and protected backups OK\n'
