set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/target-catalog-audit-20261019
mkdir -p "$dir"
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -At -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db > "$dir/legacy-public-catalog.json" <<'SQL'
BEGIN READ ONLY;
SELECT json_build_object('schools',(SELECT json_agg(s) FROM coaching.target_schools s),'programs',(SELECT json_agg(p) FROM coaching.target_university_programs p));
COMMIT;
SQL
# Transaction markers are excluded from the public-data fixture.
sed -i '/^BEGIN$/d;/^COMMIT$/d' "$dir/legacy-public-catalog.json"
wc -c "$dir/legacy-public-catalog.json"
