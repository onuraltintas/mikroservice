set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/lgs-target-import-20261019
mode=${1:?Explicit rehearsal or apply mode required}
case "$mode" in rehearsal|apply) ;; *) exit 2;; esac
db_user=$(docker exec postgres printenv POSTGRES_USER)
app=eduivme-production-coaching-service-1
if [ "$mode" = rehearsal ]; then
  if [ ! -d "$dir/bundle-r1" ]; then
    echo '0baa23f65d28bdc4462f24885975862ac651087e79ae4d710ff16553cf4dbd20  lgs-import-bundle-r1.tar.gz' | (cd "$dir" && sha256sum -c -)
    mkdir "$dir/bundle-r1"
    tar -xzf "$dir/lgs-import-bundle-r1.tar.gz" -C "$dir/bundle-r1"
    docker exec postgres createdb -U "$db_user" coaching_lgs2026_rehearsal_20261019
    docker exec -i postgres pg_restore --exit-on-error --no-owner --no-acl -U "$db_user" -d coaching_lgs2026_rehearsal_20261019 < "$dir/coaching-before.dump"
    docker cp "$dir/bundle-r1" "$app:/tmp/lgs-target-import-20261019"
  fi
  database=coaching_lgs2026_rehearsal_20261019
  extra=--rehearsal
else
  test -f "$dir/rehearsal-passed"
  database=coaching_db
  extra=
fi
echo 'e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d  TargetAdmissions-diagnostic.dll' | (cd "$dir" && sha256sum -c -)
docker cp "$dir/TargetAdmissions-diagnostic.dll" "$app:/tmp/lgs-target-import-20261019/operator/TargetAdmissions.dll"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < "$dir/preservation.sql" > "$dir/$mode-before.json"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" > "$dir/$mode-schools-before.json" <<'SQL'
SELECT json_build_object('count',count(*),'fingerprint',md5(string_agg(md5(to_jsonb(t)::text),'' ORDER BY md5(to_jsonb(t)::text)))) FROM coaching.target_schools t WHERE "Source"<>'meb-2026';
SQL
applied=$(docker exec postgres psql -X -At -U "$db_user" -d "$database" -c 'SELECT count(*) FROM coaching.__ef_migrations_history WHERE "MigrationId"='"'20261019095937_TargetAdmissions2026'"';')
if [ "$applied" = 0 ]; then
  docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < "$dir/bundle-r1/migration.sql" > "$dir/$mode-migration.log"
fi
for package in "$dir/bundle-r1/packages/"*.json; do
  file=$(basename "$package")
  docker exec "$app" sh -c '
    set -eu
    export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"
    exec dotnet /tmp/lgs-target-import-20261019/operator/TargetAdmissions.dll --database "$1" --apply ${3:+$3} --package "/tmp/lgs-target-import-20261019/packages/$2" --review-output "/tmp/lgs-target-import-20261019/$1-$2.review.json"
  ' sh "$database" "$file" "$extra" | tee "$dir/$mode-$file.log"
done
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < "$dir/preservation.sql" > "$dir/$mode-after.json"
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" > "$dir/$mode-schools-after.json" <<'SQL'
SELECT json_build_object('count',count(*),'fingerprint',md5(string_agg(md5(to_jsonb(t)::text),'' ORDER BY md5(to_jsonb(t)::text)))) FROM coaching.target_schools t WHERE "Source"<>'meb-2026';
SQL
cmp "$dir/$mode-before.json" "$dir/$mode-after.json"
cmp "$dir/$mode-schools-before.json" "$dir/$mode-schools-after.json"
docker exec -i postgres psql -X -At -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" <<'SQL'
SELECT b."Stage",r."MatchStatus",count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" GROUP BY 1,2 ORDER BY 1,2;
SELECT count(*) AS forbidden_catalog_scores FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE b."Stage"='Catalog' AND (r."MinimumScore" IS NOT NULL OR r."Percentile" IS NOT NULL);
SELECT b."Year",b."Stage",r."MinimumScore",r."Percentile" FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE r."MinimumScore" IS NOT NULL ORDER BY b."Stage";
SQL
touch "$dir/$mode-passed"
echo "$mode: protected tables and original schools unchanged"
