set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
teacher=$(docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT "Id" FROM identity.users WHERE "Email"='testogretmen@eduivme.com';
SQL
)
[[ "$teacher" =~ ^[0-9a-f-]{36}$ ]]
printf 'teacher: %s\n' "$teacher"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db -v teacher="$teacher" <<'SQL'
BEGIN READ ONLY;
SELECT "UserId","Type",count(*) FROM public."Notifications"
WHERE "UserId" IN ('473bd2d6-e3db-47c4-be56-eeedec0022ed',:'teacher') GROUP BY 1,2 ORDER BY 1,2;
SELECT "Id","UserId","Type","RelatedEntityId","Title" FROM public."Notifications"
WHERE "UserId" IN ('473bd2d6-e3db-47c4-be56-eeedec0022ed',:'teacher') AND "Type" IN ('AssignmentSubmitted','AssignmentGraded','StudyPlanUpdated','StudyPlanPublished')
ORDER BY "CreatedAt" DESC LIMIT 18;
COMMIT;
SQL
