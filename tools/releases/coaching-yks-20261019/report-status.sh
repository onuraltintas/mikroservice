set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT "Id","Title","Status",(SELECT count(*) FROM coaching.study_plan_tasks t WHERE t."RevisionId"=p."Id") tasks FROM coaching.study_plan_revisions p ORDER BY "CreatedAt";
SELECT "MigrationId" FROM coaching.__ef_migrations_history WHERE "MigrationId" LIKE '%Yks%';
SQL
