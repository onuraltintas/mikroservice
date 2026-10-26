set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-yks-20261019-r3/regression-20261019
db_user=$(docker exec postgres printenv POSTGRES_USER)
baseline=coaching_final_notification_baseline_20261019
if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$baseline'") == 0 ]];then
 docker exec postgres createdb -U "$db_user" "$baseline"
 docker exec -i postgres pg_restore -U "$db_user" -d "$baseline" --exit-on-error --no-owner --no-privileges < notification_db.before.backup > notification-baseline-restore.log 2>&1
fi
docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$baseline" -c 'SELECT "Id"::text||'"'"'|'"'"'||md5(to_jsonb(n)::text) FROM public."Notifications" n ORDER BY "Id"' | LC_ALL=C sort > notification-rows.before.txt
echo 'Original notification rows fingerprinted from the private pre-test backup; live database untouched.'
