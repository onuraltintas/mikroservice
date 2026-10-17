set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-followup-20261017
test -s built.txt
test ! -e notification-drill-verified.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
drill=coaching_followup_notification_drill_20261017
[[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
docker exec postgres pg_dump -U "$db_user" -d notification_db -Fc > notification.before-drill.backup
connection=$(jq -er '.services["notification-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; owner=${BASH_REMATCH[2]}
docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < notification.before-drill.backup > notification-drill-restore.log 2>&1
fingerprint() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$drill" <<'SQL'
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_type='BASE TABLE' AND table_schema NOT IN ('information_schema','pg_catalog') ORDER BY table_schema,table_name
\gexec
SQL
}
fingerprint > notification-drill.before.txt
jq --arg database "$drill" '.services["notification-service"].environment.ConnectionStrings__DefaultConnection |= sub("Database=notification_db;";"Database="+$database+";")' production.compose.json > notification-drill.compose.json
jq -e --arg database "$drill" '.services["notification-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$database+";")' notification-drill.compose.json >/dev/null
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
"${compose[@]}" -f notification-drill.compose.json run --rm --no-deps --pull never notification-service --migrate-only > notification-drill.log 2>&1
"${compose[@]}" -f notification-drill.compose.json run --rm --no-deps --pull never notification-service --migrate-only > notification-drill-replay.log 2>&1
fingerprint > notification-drill.after.txt
cmp notification-drill.before.txt notification-drill.after.txt
docker exec postgres dropdb -U "$db_user" "$drill"
sha256sum notification.before-drill.backup > notification-drill-backup.sha256
date -u +%FT%TZ > notification-drill-verified.txt
echo 'Notification backup restore and repeated migration/seeding passed with every existing row unchanged; broker was not connected by the drill.'
