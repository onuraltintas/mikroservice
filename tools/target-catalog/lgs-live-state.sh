set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker ps --format '{{.Names}} {{.Image}}' | grep -E 'coaching|postgres'
docker exec eduivme-production-coaching-service-1 sh -c 'if [ -n "${POSTGRES_PASSWORD:-}" ] && [ -n "${POSTGRES_USER:-}" ]; then echo "Operator connection fields available"; else echo "Operator requires connection setup"; fi'
docker exec -i postgres psql -X -At -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 4;
SELECT to_regclass('coaching.target_admission_batches'),to_regclass('coaching.target_admission_records');
SELECT pg_database_size('coaching_db');
COMMIT;
SQL
