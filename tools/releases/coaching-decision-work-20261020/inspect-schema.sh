set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
diff -u expected-coaching.schema.after.txt coaching_db.schema.after.txt | head -n 90 || true
for s in coaching-service admin-panel staff-portal; do docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.Image}} {{.State.Status}}'; done
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 1'
