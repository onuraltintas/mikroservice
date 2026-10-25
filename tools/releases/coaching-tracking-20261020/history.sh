set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-tracking-20261020
db_user=$(docker exec postgres printenv POSTGRES_USER)
case "${1:-}" in
before|after)
 for database in coaching_db identity_db notification_db; do
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" <<'SQL' | LC_ALL=C sort > "$database.rows.$1.txt"
BEGIN READ ONLY;
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;', table_schema||'.'||table_name,table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','pg_catalog') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY table_name \gexec
COMMIT;
SQL
 done
 ;;
compare)
 for database in coaching_db identity_db notification_db; do
  LC_ALL=C comm -23 "$database.rows.before.txt" "$database.rows.after.txt" > "$database.missing-old-rows.txt"
  test ! -s "$database.missing-old-rows.txt"
 done
 echo 'Every pre-test Coaching, Identity and Notification row is preserved byte-for-byte; added test records are separate.'
 ;;
*) exit 2;;
esac
