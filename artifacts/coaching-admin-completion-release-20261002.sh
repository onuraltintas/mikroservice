#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/coaching-admin-completion-20261002
drill=coaching_admin_completion_drill_20261002
tag=admin-completion-20261002
cd "$release"
db_user=$(docker exec postgres printenv POSTGRES_USER)
case "${1:-}" in
build)
  rm -f "$release/drill-complete" "$release/migrations-complete"
  docker build -t "eduivme/coaching-service:$tag" -f source/services/coaching-service/Dockerfile source > coaching-build.log 2>&1
  docker build -t "eduivme/admin-panel:$tag" -f source/clients/admin-panel/Dockerfile source/clients > admin-build.log 2>&1
  ;;
prepare)
  test ! -e production.override.yml
  docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].Config.Labels["com.docker.compose.project.config_files"]' > compose-files.txt
  printf 'services:\n' > rollback.override.yml
  for service in coaching-service admin-panel; do
    previous=$(docker inspect "eduivme-production-${service}-1" --format '{{.Config.Image}}')
    printf '  %s:\n    image: %s\n' "$service" "$previous" >> rollback.override.yml
  done
  printf 'services:\n  coaching-service:\n    image: eduivme/coaching-service:%s\n  admin-panel:\n    image: eduivme/admin-panel:%s\n' "$tag" "$tag" > production.override.yml
  chmod 600 compose-files.txt *.override.yml
  ;;
pin-rollback)
  test ! -e migrations-complete
  printf 'services:\n' > rollback.override.yml
  for service in coaching-service admin-panel; do
    previous=$(docker inspect "eduivme-production-${service}-1" --format '{{.Image}}')
    printf '  %s:\n    image: %s\n    pull_policy: never\n' "$service" "$previous" >> rollback.override.yml
  done
  chmod 600 rollback.override.yml
  ;;
backup-drill)
  rm -f "$release/drill-complete" "$release/migrations-complete"
  test ! -e coaching.backup
  docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching.backup
  chmod 600 coaching.backup
  docker exec -i postgres pg_restore --list < coaching.backup > backup-manifest.txt
  docker exec postgres createdb -U "$db_user" "$drill"
  docker exec -i postgres pg_restore -U "$db_user" -d "$drill" --exit-on-error --no-owner < coaching.backup
  # A parse error also fails closed: malformed historical JSON must be reviewed before release.
  invalid=$(docker exec postgres psql -U "$db_user" -d "$drill" -v ON_ERROR_STOP=1 -Atc '
    SELECT count(*) FROM coaching.exam_results r
    CROSS JOIN LATERAL jsonb_array_elements(r.lesson_answers) item
    LEFT JOIN coaching.study_catalog_lessons l ON l."Id"=COALESCE(item->>$$LessonId$$,item->>$$lessonId$$)::uuid
    LEFT JOIN coaching.study_catalog_topics t ON t."Id"=COALESCE(item->>$$TopicId$$,item->>$$topicId$$)::uuid
    WHERE l."Id" IS NULL OR (COALESCE(item->>$$TopicId$$,item->>$$topicId$$) IS NOT NULL AND (t."Id" IS NULL OR t."LessonId"<>l."Id"))')
  test "$invalid" = 0
  export ConnectionStrings__DefaultConnection
  ConnectionStrings__DefaultConnection=$(docker inspect eduivme-production-coaching-service-1 | jq -er '.[0].Config.Env[] | select(startswith("ConnectionStrings__DefaultConnection=")) | ltrimstr("ConnectionStrings__DefaultConnection=")')
  [[ "$ConnectionStrings__DefaultConnection" == *"Database=coaching_db;"* ]]
  ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection/Database=coaching_db;/Database=$drill;}
  docker run --rm --network eduplatform-production --env ConnectionStrings__DefaultConnection "eduivme/coaching-service:$tag" --migrate-only > migration-drill.log 2>&1
  docker exec postgres psql -U "$db_user" -d "$drill" -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history' > drill-migration-count.txt
  test "$(cat drill-migration-count.txt)" = 33
  test "$(docker exec postgres psql -U "$db_user" -d "$drill" -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId" IN ($$20261002151257_LinkTargetSchoolAdministrativeLocations$$,$$20261002160724_GuardExamCatalogReferences$$)')" = 2
  docker image inspect "eduivme/coaching-service:$tag" --format '{{.Id}}' > drill-complete
  ;;
migrate|deploy|rollback)
  if [ "$1" != rollback ]; then
    test -f drill-complete
    test "$(cat drill-complete)" = "$(docker image inspect "eduivme/coaching-service:$tag" --format '{{.Id}}')"
  fi
  IFS=',' read -ra files < compose-files.txt
  compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
  for file in "${files[@]}"; do test -f "$file"; compose+=(-f "$file"); done
  override=production.override.yml
  if [ "$1" = rollback ]; then override=rollback.override.yml; fi
  compose+=(-f "$release/$override")
  "${compose[@]}" config --quiet
  case "$1" in
    migrate)
      rm -f "$release/migrations-complete"
      "${compose[@]}" run --rm --no-deps coaching-service --migrate-only > migration-production.log 2>&1
      test "$(docker exec postgres psql -U "$db_user" -d coaching_db -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history')" = 33
      test "$(docker exec postgres psql -U "$db_user" -d coaching_db -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId" IN ($$20261002151257_LinkTargetSchoolAdministrativeLocations$$,$$20261002160724_GuardExamCatalogReferences$$)')" = 2
      docker image inspect "eduivme/coaching-service:$tag" --format '{{.Id}}' > migrations-complete
      ;;
    deploy)
      test -f migrations-complete
      test "$(cat migrations-complete)" = "$(docker image inspect "eduivme/coaching-service:$tag" --format '{{.Id}}')"
      test "$(docker exec postgres psql -U "$db_user" -d coaching_db -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId" IN ($$20261002151257_LinkTargetSchoolAdministrativeLocations$$,$$20261002160724_GuardExamCatalogReferences$$)')" = 2
      "${compose[@]}" up -d --no-deps coaching-service admin-panel
      ;;
    rollback) "${compose[@]}" up -d --no-deps coaching-service admin-panel ;;
  esac
  ;;
cleanup-drill)
  test -f migrations-complete
  # Exact disposable restore target only; never drop a production database.
  test "$drill" = coaching_admin_completion_drill_20261002
  docker exec postgres dropdb -U "$db_user" "$drill"
  ;;
*) exit 2 ;;
esac
