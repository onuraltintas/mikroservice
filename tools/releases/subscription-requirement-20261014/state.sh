#!/usr/bin/env bash
set -euo pipefail
[[ "$PWD" == /var/lib/eduivme/releases/subscription-requirement-20261014 ]]
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d speedreading_owned_db -Atc 'SELECT coalesce((SELECT "RequireActiveSubscription" FROM speed_reading.bank_transfer_payment_settings WHERE "Id"='"'"'4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2'"'"'),true) AS speed_reading_requires_subscription'
docker exec postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db -Atc 'SELECT "RequireActiveSubscription" FROM coaching.subscription_settings'
docker exec postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d speedreading_owned_db -Atc 'SELECT "MigrationId" FROM speed_reading.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 1'
docker exec postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d postgres -Atc 'SELECT count(*) FROM pg_database WHERE datname='"'"'subscription_requirement_drill_20261014'"'"''
docker inspect eduivme-production-speed-reading-service-1 eduivme-production-admin-panel-1 --format '{{.Name}} {{.Image}} status={{.State.Status}} restarts={{.RestartCount}}'
tail -n 3 monitor-public-http.log
