set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
for file in *.protected.missing.txt; do printf '%s ' "$file"; wc -l < "$file"; cut -d'|' -f1 "$file" | sort | uniq -c; done
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres psql -X -qAt -U "$db_user" -d identity_db -c "SELECT column_name FROM information_schema.columns WHERE table_schema='identity' AND table_name='users' ORDER BY ordinal_position"
