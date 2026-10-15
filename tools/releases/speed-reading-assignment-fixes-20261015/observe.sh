set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/speed-reading-assignment-fixes-20261015
bash deploy.sh validate
printf 'observed_at='; date -u +%FT%TZ
for destination in https://masterhizliokuma.com/auth/login https://onuraltintas.net/staff/; do
  curl -fsS --max-time 15 -o /dev/null -w 'public_status=%{http_code} elapsed=%{time_total}\n' "$destination"
done
docker logs --since "$(cat deployed.txt)" eduivme-production-speed-reading-service-1 2>&1 | awk '/HTTP .* responded /{ n++; for(i=1;i<=NF;i++){if($i=="responded"){code=$(i+1); if(code>=500) errors++; if($(i+3) ~ /^[0-9.]+$/){sum+=$(i+3); if($(i+3)>max)max=$(i+3)}}}} END {printf "requests=%d server_5xx=%d mean_ms=%.2f max_ms=%.2f\n",n,errors,n?sum/n:0,max}'
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d speedreading_owned_db <<'SQL'
BEGIN READ ONLY;
SELECT "MigrationId" FROM speed_reading.__ef_migrations_history WHERE "MigrationId"='20261015163000_ClearUnmeasuredAssignmentMetrics';
SELECT a.id,a."Title",a."DueDate",a.institution_id,sa.id AS student_assignment_id,sa."IsCompleted",sa."CompletionDate",sa."Score",sa."KeyPerformanceMetric",sa."ResultId",r.time_spent_seconds,r.is_measured
FROM speed_reading.assignments a
JOIN speed_reading.student_assignments sa ON sa."AssignmentId"=a.id
LEFT JOIN speed_reading.exercise_session_results r ON r.id=sa."ResultId"
WHERE a."TeacherId"='b184504c-a3fa-4f7d-ad40-71fe418eaa10'
AND sa."StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed'
AND (a.id='ea11655e-9b4b-4bd3-be2f-1ee2c9b8c871' OR sa.id='ebd2f213-05ed-4a3d-b4b8-a3e88f369800');
SELECT count(*) AS unrepaired_unmeasured_assignments FROM speed_reading.student_assignments a
JOIN speed_reading.exercise_session_results r ON r.id=a."ResultId" AND r.student_id=a."StudentId"
WHERE a."IsCompleted"=TRUE AND r.is_measured=FALSE AND (a."Score" IS NOT NULL OR a."KeyPerformanceMetric" IS NOT NULL);
COMMIT;
SQL
