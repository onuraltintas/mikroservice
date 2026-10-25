set -euo pipefail
cd /var/lib/eduivme/releases/unified-target-catalog-20261020
source <(sed '/^case /,$d' deploy.sh)
protected coaching_db > ui-protected.after.txt
cmp promotion-protected.before.txt ui-protected.after.txt
echo 'All protected coaching tables, including goal content and versions, match the pre-migration baseline after UI saves.'
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT "CatalogType", count(*) FROM coaching.target_catalog GROUP BY 1 ORDER BY 1;
SELECT "ScoreType",count(*) FROM coaching.target_catalog GROUP BY 1 ORDER BY 1;
SELECT 'remaining_old_tables',count(*) FROM information_schema.tables WHERE table_schema='coaching'
 AND table_name IN ('target_schools','target_university_programs','target_admission_batches','target_admission_records');
SELECT 'goals_preserved',count(*) FROM coaching.academic_goals;
SELECT 'preserved_lgs_goal_identities',count(*) FROM coaching.academic_goals WHERE target_school_id IN
 ('ff9bc028-04f4-4e7f-b5ac-3af5a26f7bff','e739171d-ad2b-438b-9c30-9401660f9a3e');
COMMIT;
SQL
exec 9>&-
bash deploy.sh verify
