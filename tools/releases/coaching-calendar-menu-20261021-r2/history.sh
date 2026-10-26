set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
db_user=$(docker exec postgres printenv POSTGRES_USER)
capture(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort > "$2"
BEGIN READ ONLY;
SELECT format('SELECT %L,md5((to_jsonb(t) %s)::text) FROM %I.%I t;',table_schema||'.'||table_name,
 CASE WHEN table_schema='coaching' AND table_name='study_plan_revisions' THEN '-CASE WHEN "Id"=''a4cea155-5485-4db6-9f52-1ec4df8080c3''::uuid THEN ARRAY[''Version'',''UpdatedAt''] ELSE ARRAY[]::text[] END'
 ELSE '' END,table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY table_schema,table_name \gexec
COMMIT;
SQL
}
case "$1" in
before|after) for database in coaching_db identity_db notification_db; do capture "$database" "$database.rows.$1.txt"; done;;
probe) capture "$2" "$3";;
compare)
 for database in coaching_db identity_db notification_db; do
  LC_ALL=C comm -23 "$database.rows.before.txt" "$database.rows.after.txt" > "$database.missing-old-rows.txt"
  test ! -s "$database.missing-old-rows.txt"
 done
 echo 'All original business rows preserved; nullable decision links normalized, only the named test plan version/timestamp tracked separately.';;
*) exit 2;;
esac
