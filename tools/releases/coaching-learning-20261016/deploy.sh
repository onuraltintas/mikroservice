#!/usr/bin/env bash
set -euo pipefail
trap 'printf "DEPLOY_FAILED stage=%s line=%s\n" "${1:-validate}" "$LINENO" >&2' ERR
umask 077
release=/var/lib/eduivme/releases/coaching-learning-20261016
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-learning-20261016
services=(identity-service coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
identity_drill=coaching_learning_drill_identity_20261016
coaching_drill=coaching_learning_drill_coaching_20261016
identity_name=coaching-learning-drill-identity-20261016
coaching_name=coaching-learning-drill-coaching-20261016
rabbit=coaching-learning-drill-rabbit-20261016
redis=coaching-learning-drill-redis-20261016
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
unchanged() {
  local name id image
  while read -r name id image; do
    case "$name" in eduivme-production-identity-service-1|eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated container changed: $name" >&2; return 1; }
  done < baseline/containers.txt
}
baselineTargets() {
  local service
  for service in "${services[@]}"; do
    [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]
  done
}
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; }
  [[ "$service" != staff-portal ]] || { port=80; path=/; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  docker inspect "$name" --format '{{.Name}} running={{.State.Running}} exit={{.State.ExitCode}} restarts={{.RestartCount}}' >&2; return 1
}
fingerprint() {
  local database=$1 schema=$2
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" -v schema="$schema" <<'SQL'
SELECT format('SELECT %L, count(*), md5(coalesce(string_agg(md5((to_jsonb(t) - %L::text[])::text), %L ORDER BY md5((to_jsonb(t) - %L::text[])::text)), %L)) FROM %I.%I t;',table_schema||'.'||table_name,CASE WHEN table_schema='identity' AND table_name='Configurations' THEN '{CurriculumVersionId,UpdatedAt}' ELSE '{CurriculumVersionId}' END,'',CASE WHEN table_schema='identity' AND table_name='Configurations' THEN '{CurriculumVersionId,UpdatedAt}' ELSE '{CurriculumVersionId}' END,'',table_schema,table_name)
FROM information_schema.tables
WHERE table_schema=:'schema' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','curriculum_versions','student_learning_contexts','student_course_preferences','student_topic_states')
AND (table_schema <> 'coaching' OR table_name NOT IN ('student_profiles','teacher_profiles','institution_memberships','teacher_student_assignments','invitations'))
ORDER BY table_name
\gexec
SQL
}
runVerifier() {
  local config=$1 mode=${2:-}
  "${compose[@]}" -f "$config" run --rm --no-deps --pull never --entrypoint sh \
    -e VERIFY_CONNECTION -e VERIFY_API -e VERIFY_ADMIN_ID -e ALLOW_LIVE_CATALOG \
    -v "$release/payload/verification:/verification:ro" coaching-service \
    -c 'cp -a /verification /tmp/verification && cp /app/*.dll /tmp/verification/ && dotnet /tmp/verification/Verification.dll "$@"' -- ${mode:+"$mode"}
}
makeDrillConfig() {
  local secret password
  secret=$(openssl rand -hex 48); password=$(openssl rand -hex 24)
  docker run -d --name "$rabbit" --network eduplatform-production --user 100:101 --tmpfs /var/lib/rabbitmq:rw,uid=100,gid=101,mode=0700 -e HOME=/var/lib/rabbitmq -e RABBITMQ_DEFAULT_USER=learning_drill -e "RABBITMQ_DEFAULT_PASS=$password" rabbitmq:4.3.4-management-alpine > drill-rabbit.txt
  docker run -d --name "$redis" --network eduplatform-production redis:7.2-alpine redis-server --requirepass "$password" > drill-redis.txt
  jq --arg identity "$identity_drill" --arg coaching "$coaching_drill" --arg name "$identity_name" --arg rabbit "$rabbit" --arg redis "$redis" --arg secret "$secret" --arg password "$password" '
    .services["identity-service"].environment.ConnectionStrings__DefaultConnection |= sub("Database=identity_db;"; "Database="+$identity+";") |
    .services["coaching-service"].environment.ConnectionStrings__DefaultConnection |= sub("Database=coaching_db;"; "Database="+$coaching+";") |
    .services["coaching-service"].environment.Services__IdentityService=("http://"+$name+":8080") |
    .services["coaching-service"].environment.Coaching__Reminders__Enabled="false" |
    .services["coaching-service"].environment.Coaching__Attachments__RootPath="/tmp/learning-drill-attachments" |
    .services["coaching-service"].environment.Coaching__CmsMedia__RootPath="/tmp/learning-drill-cms" |
    .services |= with_entries(if (.key=="identity-service" or .key=="coaching-service") then
      .value.environment += {JWT_SECRET:$secret,JWT_KEY_ID:"learning-drill",JWT_PREVIOUS_SECRETS:"",JWT_PREVIOUS_KEY_IDS:"",INTERNAL_SERVICE_API_KEY:$secret,
        RABBITMQ_HOST:$rabbit,RabbitMQ__Host:$rabbit,RABBITMQ_USERNAME:"learning_drill",RabbitMQ__Username:"learning_drill",RABBITMQ_PASSWORD:$password,RabbitMQ__Password:$password,
        REDIS_HOST:$redis,REDIS_PORT:"6379",REDIS_PASSWORD:$password,ConnectionStrings__Redis:($redis+":6379,password="+$password),DataProtection__KeysPath:"/tmp/learning-drill-keys"} |
      .value.volumes = ((.value.volumes // []) | map(select(.target | startswith("/run/secrets/"))))
      else . end)' production.compose.json > drill.compose.json
  jq -e --arg identity "$identity_drill" --arg coaching "$coaching_drill" '.services["identity-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$identity+";")' drill.compose.json >/dev/null
  jq -e --arg coaching "$coaching_drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$coaching+";")' drill.compose.json >/dev/null
  for attempt in $(seq 1 40); do docker exec "$rabbit" rabbitmq-diagnostics -q ping >/dev/null 2>&1 && return 0; sleep 2; done; return 1
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload; unchanged
    docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Image=="sha256:14d22302e4bb579dcea73b3626457509a3b148105335b77ac79da1b3b126ce7b"' >/dev/null
    docker inspect eduivme-production-coaching-service-1 | jq -e '.[0].Image=="sha256:79ce347b0e44bda398dd60e0891d09adeca0de1eba7adf60987187f84adfafd2"' >/dev/null
    docker inspect eduivme-production-admin-panel-1 | jq -e '.[0].Image=="sha256:81bcf9a7663e58e8378ad06089915dd7ab7eaba974a957244f402f81d36cdcbc"' >/dev/null
    docker inspect eduivme-production-staff-portal-1 | jq -e '.[0].Image=="sha256:f010cbea52da4124a706d0c733498e63700a3ac35cc463f3541c3ad554435401"' >/dev/null
    docker inspect eduivme-production-identity-service-1 eduivme-production-coaching-service-1 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 | jq -r '.[].Config.Labels["com.docker.compose.project.config_files"]|split(",")[]' | awk '!seen[$0]++' > compose-files.txt
    files=("${compose[@]}"); while read -r file; do test -f "$file"; files+=(-f "$file"); done < compose-files.txt
    export TEEN_API_IMAGE; TEEN_API_IMAGE=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{.Config.Image}}')
    "${files[@]}" config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 baseline/containers.txt) | jq '{services:(map(select(.Config.Labels["com.docker.compose.service"] != null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json; mv next.json frozen.compose.json
    done
    jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["identity-service"].image=("eduivme/identity-service:"+$tag)|.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    jq -e '.AuthRecaptcha__Enabled=="true" and (.JWT_ISSUER|length)>0 and (.JWT_AUDIENCE|length)>0' identity-service.environment.json >/dev/null
    date -u +%FT%TZ > prepared.txt
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload; baselineTargets
    for service in "${services[@]}"; do
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$(<source-revision.txt)" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    unchanged; date -u +%FT%TZ > built.txt
    ;;
  drill)
    test -s built.txt; test ! -e drill-verified.txt; baselineTargets; verifyPayload
    for database in "$identity_drill" "$coaching_drill"; do [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$database'") == 0 ]]; done
    for name in "$identity_name" "$coaching_name" "$rabbit" "$redis"; do ! docker inspect "$name" >/dev/null 2>&1; done
    for service in identity coaching; do
      database=${service}_db; target=$identity_drill; [[ "$service" != coaching ]] || target=$coaching_drill
      docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$service.before-drill.backup"
      connection=$(jq -er '.ConnectionStrings__DefaultConnection' "$service-service.environment.json")
      username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; owner=${BASH_REMATCH[2]}
      docker exec postgres createdb -U "$db_user" -O "$owner" "$target"
      docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$target" --exit-on-error --no-owner --no-privileges < "$service.before-drill.backup" > "$service-restore.log" 2>&1
      fingerprint "$target" "$service" > "$service.drill.before.txt"
    done
    sha256sum *.before-drill.backup > drill-backups.sha256
    makeDrillConfig
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migration.log 2>&1
    "${compose[@]}" -f drill.compose.json run -d --no-deps --pull never --name "$identity_name" identity-service > drill-identity.txt
    waitReady "$identity_name" identity-service
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --import-identity-profiles > drill-import-preview.log 2>&1
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --import-identity-profiles --apply > drill-import.log 2>&1
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --import-identity-profiles --apply > drill-import-replay.log 2>&1
    for service in identity coaching; do target=$identity_drill; [[ "$service" != coaching ]] || target=$coaching_drill; fingerprint "$target" "$service" > "$service.drill.after.txt"; cmp "$service.drill.before.txt" "$service.drill.after.txt"; done
    "${compose[@]}" -f drill.compose.json run -d --no-deps --pull never --name "$coaching_name" coaching-service > drill-coaching.txt
    waitReady "$coaching_name" coaching-service
    ;&
  drill-verify)
    test -s built.txt; test ! -e drill-verified.txt; baselineTargets; verifyPayload
    for service in identity coaching; do target=$identity_drill; [[ "$service" != coaching ]] || target=$coaching_drill; fingerprint "$target" "$service" > "$service.drill.verify-before.txt"; cmp "$service.drill.before.txt" "$service.drill.verify-before.txt"; done
    waitReady "$identity_name" identity-service; waitReady "$coaching_name" coaching-service
    export VERIFY_CONNECTION VERIFY_API VERIFY_ADMIN_ID ALLOW_LIVE_CATALOG
    VERIFY_CONNECTION=$(jq -er --arg database "$coaching_drill" '.ConnectionStrings__DefaultConnection | sub("Database=coaching_db;"; "Database="+$database+";")' coaching-service.environment.json)
    VERIFY_API="http://$coaching_name:8080"
    VERIFY_ADMIN_ID=$(docker exec postgres psql -U "$db_user" -d "$identity_drill" -Atc "SELECT \"Id\" FROM identity.users WHERE \"Email\"='admin@eduivme.com'")
    ALLOW_LIVE_CATALOG=''
    runVerifier drill.compose.json > drill-result.json 2> drill-error.log
    jq -e '.passed and .isolated and .preservedPlans==3 and .preservedWorkRecords==4' drill-result.json >/dev/null
    for name in "$coaching_name" "$identity_name" "$rabbit" "$redis"; do docker rm -f "$name" >/dev/null; done
    for database in "$identity_drill" "$coaching_drill"; do docker exec postgres dropdb -U "$db_user" "$database"; done
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat drill-result.json
    ;;
  promote)
    test -s drill-verified.txt; test ! -e deployed.txt; verifyPayload; baselineTargets; unchanged
    for service in "${services[@]}"; do [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]; done
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        for service in identity-service coaching-service admin-panel staff-portal; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1; waitReady "eduivme-production-$service-1" "$service" || exit 70; done
        echo 'Previous application images restored; additive schema and backups retained.' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    docker stop --time 30 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 eduivme-production-coaching-service-1 eduivme-production-identity-service-1 > writer-stop.log
    for service in identity coaching; do
      docker exec postgres pg_dump -U "$db_user" -d "${service}_db" -Fc > "$service.before-promotion.backup"
      fingerprint "${service}_db" "$service" > "$service.live.before.txt"
    done
    sha256sum *.before-promotion.backup > live-backups.sha256
    "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migration.log 2>&1
    "${compose[@]}" -f production.compose.json up -d --no-deps --pull never identity-service > identity-service-promotion.log 2>&1
    waitReady eduivme-production-identity-service-1 identity-service
    "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --import-identity-profiles > live-import-preview.log 2>&1
    "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --import-identity-profiles --apply > live-import.log 2>&1
    for service in identity coaching; do fingerprint "${service}_db" "$service" > "$service.live.after-import.txt"; cmp "$service.live.before.txt" "$service.live.after-import.txt"; done
    export VERIFY_CONNECTION VERIFY_API VERIFY_ADMIN_ID ALLOW_LIVE_CATALOG
    VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json); VERIFY_API=''
    VERIFY_ADMIN_ID=$(docker exec postgres psql -U "$db_user" -d identity_db -Atc "SELECT \"Id\" FROM identity.users WHERE \"Email\"='admin@eduivme.com'")
    ALLOW_LIVE_CATALOG=coaching-learning-20261016
    runVerifier production.compose.json --live-catalog > live-catalog.json 2> live-catalog-error.log
    jq -e '.passed and .lessons==169 and .inventedDurations==0' live-catalog.json >/dev/null
    for service in coaching-service admin-panel staff-portal; do
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitReady "eduivme-production-$service-1" "$service"
    done
    for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; done
    for service in identity coaching; do fingerprint "${service}_db" "$service" > "$service.live.after.txt"; cmp "$service.live.before.txt" "$service.live.after.txt"; done
    curl -fsS --max-time 15 https://eduivme.com/coaching-portal/setup >/dev/null
    curl -fsS --max-time 15 https://eduivme.com/staff/ >/dev/null
    unchanged; date -u +%FT%TZ > deployed.txt; trap - EXIT
    ;;
  validate)
    test -s deployed.txt
    for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat deployed.txt; cat live-catalog.json
    ;;
  *) exit 2;;
esac
