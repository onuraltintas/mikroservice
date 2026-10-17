set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-followup-20261017
db_user=$(docker exec postgres printenv POSTGRES_USER)
started=$(cat live-verification/baseline-verified.txt)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db -v "started=$started" <<'SQL' > notification-live-result.json
SELECT json_build_object('studentUpdates',count(*),'correctRecipient',bool_and("UserId"='473bd2d6-e3db-47c4-be56-eeedec0022ed'),'correctPlan',bool_and("RelatedEntityId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e'),'uniqueMessages',count(DISTINCT "Id")) FROM public."Notifications" WHERE "Type"='StudyPlanUpdated' AND "CreatedAt">=:'started'::timestamptz;
SQL
jq -e '.studentUpdates==2 and .correctRecipient and .correctPlan and .uniqueMessages==2' notification-live-result.json >/dev/null
for service in notification-service coaching-service staff-portal; do docker inspect "eduivme-production-$service-1" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}}'; done > service-status.txt
count=$(docker logs --since "$(cat deployed.txt)" eduivme-production-notification-service-1 2>&1 | grep -Eic 'Unhandled exception|fail:|\[ERR\]|FATAL' || true)
[[ "$count" == 0 ]]
printf '{"notificationErrors":%s}\n' "$count" > notification-errors.json
date -u +%FT%TZ > notification-live-verified.txt
cat notification-live-result.json;cat notification-errors.json;cat service-status.txt
