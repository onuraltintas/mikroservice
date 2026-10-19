set -euo pipefail
dir=/var/lib/eduivme/releases/lgs-target-import-20261019
test -f "$dir/apply-passed"
db_user=$(docker exec postgres printenv POSTGRES_USER)
app=eduivme-production-coaching-service-1
docker inspect --format '{{.State.Running}}' "$app"
ip=$(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{println}}{{end}}' "$app" | head -n 1)
curl --fail --silent --show-error --max-time 20 "http://$ip:8080/health/live"
echo
docker exec -i postgres psql -X -At -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT json_build_object('batches',count(*)) FROM coaching.target_admission_batches;
SELECT json_build_object('records',count(*),'verified',count(*) FILTER (WHERE "MatchStatus"='Verified'),'ambiguous',count(*) FILTER (WHERE "MatchStatus"='Ambiguous'),'pending',count(*) FILTER (WHERE "MatchStatus"='Pending'),'conflict',count(*) FILTER (WHERE "MatchStatus"='Conflict')) FROM coaching.target_admission_records;
SELECT json_build_object('oldSchools',count(*) FILTER (WHERE "Source"<>'meb-2026'),'newSchools',count(*) FILTER (WHERE "Source"='meb-2026')) FROM coaching.target_schools;
SELECT 'alias',"MatchStatus",count(*) FROM coaching.target_admission_records WHERE split_part("ExternalCode",':',2) IN ('235280','966950','965232','972018','763476','162379','966707','970805') GROUP BY 2 ORDER BY 2;
SELECT 'attachment_migration_still_pending',count(*)=0 FROM coaching.__ef_migrations_history WHERE "MigrationId"='20261017215200_AddAssignmentInstructionAttachments';
COMMIT;
SQL
echo 'Preservation comparisons:'
cmp "$dir/apply-before.json" "$dir/apply-after.json"
cmp "$dir/apply-schools-before.json" "$dir/apply-schools-after.json"
echo 'PASS'
