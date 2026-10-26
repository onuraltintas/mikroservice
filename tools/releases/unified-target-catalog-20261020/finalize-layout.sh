set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/unified-target-catalog-20261020
source <(sed '/^case /,$d' deploy.sh)
for service in "${services[@]}"; do
 [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]
done
protected coaching_db > layout-protected.before.txt
tar -xzf backend-integrity.tar.gz
cp -a /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/dist/admin-panel/. payload/admin/
cp -a /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/dist/staff-portal/browser/. payload/staff/
{ find payload -type f; printf '%s\n' *.Dockerfile incoming.sql lgs-schema.sql replace-mebi-lgs.sql unified-schema.sql constraints.sql verify.sql deploy.sh; } | LC_ALL=C sort | xargs sha256sum > payload.sha256
for service in "${services[@]}"; do
 docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$tag" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-layout-build.log" 2>&1
done
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching_db.before-constraints.backup
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db < constraints.sql > live-constraints.log
for service in "${services[@]}"; do
 "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-layout-promote.log" 2>&1
 ready "$service"
 docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
done
protected coaching_db > layout-protected.after.txt
cmp layout-protected.before.txt layout-protected.after.txt
stable_others
verify_catalog coaching_db
cat coaching_db-catalog-verification.log
docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db -c "SELECT conname FROM pg_constraint WHERE conrelid='coaching.target_catalog'::regclass AND conname IN ('ck_target_catalog_degree','ck_target_catalog_score_type','ck_target_catalog_location_pair') ORDER BY 1"
date -u +%FT%TZ > finalized.txt
echo 'Readable shared target filters and all catalog integrity checks deployed; protected data is unchanged.'
