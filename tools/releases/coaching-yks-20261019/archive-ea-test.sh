set -euo pipefail
umask 077
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN;
UPDATE coaching.study_plan_revisions SET "Status"=2,"IsActive"=false,"UpdatedAt"=now(),"Version"="Version"+1 WHERE "Id"='1cd18468-07b8-41a4-835e-9a68666aa410' AND "Title"='YKS canlı doğrulama · öğrenci EA · 19.10.2026' AND "Status"=0 AND NOT "IsActive";
COMMIT;
SELECT "Status"=2 AND NOT "IsActive" FROM coaching.study_plan_revisions WHERE "Id"='1cd18468-07b8-41a4-835e-9a68666aa410';
SQL
