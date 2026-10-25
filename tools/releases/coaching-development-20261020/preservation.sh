set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-development-20261020
cd "$release"
db_user=$(docker exec postgres printenv POSTGRES_USER)
snapshot(){
  local database=$1 output=$2
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" <<'SQL' | LC_ALL=C sort > "$output"
BEGIN READ ONLY;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',
 table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY table_name \gexec
COMMIT;
SQL
  test -s "$output"
}
case "${1:-}" in
before|after)
  snapshot coaching_db "coaching.$1.txt"
  snapshot identity_db "identity.$1.txt"
  snapshot notification_db "notification.$1.txt"
  ;;
compare)
  for name in coaching identity notification; do cmp "$name.before.txt" "$name.after.txt"; done
  echo 'All Coaching, Identity account and notification rows are unchanged (refresh tokens excluded).'
  ;;
*) exit 2;;
esac
