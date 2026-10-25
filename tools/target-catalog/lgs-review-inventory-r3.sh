set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT jsonb_agg(jsonb_build_object('record',to_jsonb(r),'stage',b."Stage",'sourceUrl',b."SourceUrl",
 'candidates',(SELECT jsonb_agg(jsonb_build_object('id',s."Id",'name',s."Name",'city',s."City",'district',s."District",'source',s."Source",'sourceId',s."SourceId"))
 FROM coaching.target_schools s WHERE upper(s."Name")=upper(r."Institution") AND upper(s."City")=upper(r."City")
 AND (r."District" IS NULL OR replace(upper(s."District"),' (MERKEZ)','')=upper(r."District")))) ORDER BY r."ExternalCode")
FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId"
WHERE b."ExamCode"='LGS' AND b."Year"=2026 AND r."MatchStatus" IN ('Ambiguous','Pending','Conflict');
COMMIT;
SQL
