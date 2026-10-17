set -euo pipefail
for service in coaching-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} image={{.Image}} revision={{index .Config.Labels "org.opencontainers.image.revision"}} compose={{index .Config.Labels "com.docker.compose.project.config_files"}} workdir={{.Config.WorkingDir}} cmd={{json .Config.Cmd}}'
done
docker exec eduivme-production-staff-portal-1 sh -c 'find /usr/share/nginx/html /app/dist -name index.html 2>/dev/null | head -5' || true
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SELECT table_name,column_name FROM information_schema.columns WHERE table_schema='coaching' AND (table_name ILIKE '%teacher%' OR table_name ILIKE '%student%') ORDER BY table_name,ordinal_position;
SQL
