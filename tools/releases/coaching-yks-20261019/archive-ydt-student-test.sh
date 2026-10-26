set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN;
UPDATE coaching.study_plan_revisions SET "Status"=2,"IsActive"=false,"UpdatedAt"=now(),"Version"="Version"+1 WHERE "Id"='8ec1e3bb-8471-47e9-b074-560ae5022b87' AND "Title"='YKS canlı doğrulama · öğrenci YDT English · 19.10.2026' AND "Status"=0 AND NOT "IsActive";
COMMIT;
SELECT "Status"=2 AND NOT "IsActive" FROM coaching.study_plan_revisions WHERE "Id"='8ec1e3bb-8471-47e9-b074-560ae5022b87';
SQL
