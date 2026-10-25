set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
db_user=$(docker exec postgres printenv POSTGRES_USER)
capture(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort > "$2"
BEGIN READ ONLY;
SELECT format('SELECT %L,md5((to_jsonb(t) %s)::text) FROM %I.%I t;',table_schema||'.'||table_name,
 CASE WHEN table_schema='identity' AND table_name='users' THEN '-CASE WHEN "Id" IN (''473bd2d6-e3db-47c4-be56-eeedec0022ed''::uuid,''b184504c-a3fa-4f7d-ad40-71fe418eaa10''::uuid) THEN ARRAY[''LastLoginAt'',''UpdatedAt'',''Version''] ELSE ARRAY[]::text[] END'
 WHEN table_schema='coaching' AND table_name='study_plan_revisions' THEN '-CASE WHEN "Id"=''a4cea155-5485-4db6-9f52-1ec4df8080c3''::uuid THEN ARRAY[''Version'',''UpdatedAt''] ELSE ARRAY[]::text[] END'
 ELSE '' END,table_schema,table_name)
FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type='BASE TABLE'
AND table_name NOT IN ('InboxState','OutboxState','OutboxMessage','RefreshTokens') ORDER BY table_schema,table_name \gexec
COMMIT;
SQL
}
case "$1" in
reference)
 ref=identity_calendar_reference_20261021
 [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$ref'") == 0 ]]
 docker exec postgres createdb -U "$db_user" "$ref"
 cleanup(){ docker exec postgres dropdb -U "$db_user" "$ref"; }; trap cleanup EXIT
 docker exec -i postgres pg_restore -U "$db_user" -d "$ref" --exit-on-error --no-owner --no-privileges < identity_db.before.backup > identity-reference-restore.log 2>&1
 capture "$ref" identity_db.protected.before.txt
 echo 'Identity reference captured from the predeployment backup; only the two test accounts login timestamp/version fields normalized.';;
verify)
 for database in coaching_db notification_db; do capture "$database" "$database.protected.after.txt"; LC_ALL=C comm -23 "$database.rows.before.txt" "$database.protected.after.txt" > "$database.protected.missing.txt"; test ! -s "$database.protected.missing.txt"; done
 capture identity_db identity_db.protected.after.txt
 LC_ALL=C comm -23 identity_db.protected.before.txt identity_db.protected.after.txt > identity_db.protected.missing.txt
 test ! -s identity_db.protected.missing.txt
 echo 'All original business records preserved; only test-plan version/timestamp and two test-account login metadata excluded.';;
*)exit 2;;
esac
