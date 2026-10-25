set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN TRANSACTION READ ONLY;
SELECT table_name,column_name,data_type FROM information_schema.columns WHERE table_schema='coaching' AND table_name IN ('target_schools','target_university_programs','academic_goals') ORDER BY table_name,ordinal_position;
SELECT json_build_object('schools',count(*),'active',count(*) FILTER(WHERE "IsActive"),'noScore',count(*) FILTER(WHERE "MinimumScore" IS NULL),'noYear',count(*) FILTER(WHERE "ScoreYear" IS NULL),'noVerifiedLocation',count(*) FILTER(WHERE "ProvinceId" IS NULL OR "DistrictId" IS NULL),'cities',count(DISTINCT "City")) FROM coaching.target_schools;
SELECT "Source","ScoreYear","IsActive",count(*) FROM coaching.target_schools GROUP BY 1,2,3 ORDER BY 1,2,3;
SELECT row_to_json(s) FROM coaching.target_schools s ORDER BY "Name" LIMIT 12;
SELECT json_build_object('programs',count(*),'active',count(*) FILTER(WHERE "IsActive"),'noCode',count(*) FILTER(WHERE nullif(btrim("ProgramCode"),'') IS NULL),'noScoreType',count(*) FILTER(WHERE nullif(btrim("ScoreType"),'') IS NULL),'noScore',count(*) FILTER(WHERE "MinimumScore" IS NULL),'noYear',count(*) FILTER(WHERE "ScoreYear" IS NULL),'universities',count(DISTINCT "UniversityName")) FROM coaching.target_university_programs;
SELECT "Source","ScoreYear","ScoreType","IsActive",count(*) FROM coaching.target_university_programs GROUP BY 1,2,3,4 ORDER BY 1,2,3,4;
SELECT row_to_json(p) FROM coaching.target_university_programs p ORDER BY "UniversityName","Name" LIMIT 12;
SELECT "ProgramCode",count(*) FROM coaching.target_university_programs WHERE "ProgramCode" IS NOT NULL GROUP BY 1 HAVING count(*)>1;
SELECT count(*) goals,count(*) FILTER(WHERE target_school_id IS NOT NULL) school_links,count(*) FILTER(WHERE target_university_program_id IS NOT NULL) program_links FROM coaching.academic_goals;
SELECT json_build_object('schoolDuplicateNameLocationGroups',(SELECT count(*) FROM (SELECT 1 FROM coaching.target_schools GROUP BY "Name","City","District" HAVING count(*)>1) d),'schoolZeroScores',(SELECT count(*) FROM coaching.target_schools WHERE "MinimumScore"=0),'programZeroScores',(SELECT count(*) FROM coaching.target_university_programs WHERE "MinimumScore"=0));
SELECT "ScoreType",min("MinimumScore"),max("MinimumScore"),count(*) FILTER(WHERE "MinimumScore"=0) zero_scores FROM coaching.target_university_programs GROUP BY 1 ORDER BY 1;
COMMIT;
SQL

