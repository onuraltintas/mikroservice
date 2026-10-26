set -euo pipefail
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
diff -u live-identity.before.txt live-identity.after.txt || true
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -U "$db_user" -d identity_db <<'SQL'
BEGIN READ ONLY;
SELECT table_schema,table_name,column_name FROM information_schema.columns WHERE table_name ILIKE '%user%' ORDER BY 1,2,ordinal_position;
COMMIT;
SQL
ls -lh coaching_db.before-promotion.backup identity_db.before.backup
