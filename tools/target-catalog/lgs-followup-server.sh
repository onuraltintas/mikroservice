set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/lgs-followup-20261019
drill=coaching_lgs2026_rehearsal_20261019
app=eduivme-production-coaching-service-1
db_user=$(docker exec postgres printenv POSTGRES_USER)
cd "$dir"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
mode=${1:?Explicit mode required}
fingerprint(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < preservation.sql > "$mode-$2-protected.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' > "$mode-$2-schools.json"
SELECT count(*),md5(coalesce(string_agg(md5(to_jsonb(s)::text),'' ORDER BY md5(to_jsonb(s)::text)),'')) FROM coaching.target_schools s WHERE "SourceId" NOT LIKE 'reviewed-lgs2026:%';
SQL
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' > "$mode-$2-source-cells.json"
SELECT count(*),md5(coalesce(string_agg(md5((to_jsonb(r)-'{SchoolId,MatchStatus,Issue}'::text[])::text),'' ORDER BY md5((to_jsonb(r)-'{SchoolId,MatchStatus,Issue}'::text[])::text)),'')) FROM coaching.target_admission_records r WHERE "ExternalCode" NOT LIKE 'reviewed-lgs2026:%';
SQL
}
case "$mode" in
prepare)
 test ! -f prepared
 sha256sum -c payload.sha256 > payload-verified.log
 docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching-before.dump
 sha256sum coaching-before.dump > backup.sha256
 docker exec postgres createdb -U "$db_user" "$drill"
 docker exec -i postgres pg_restore --exit-on-error --no-owner --no-acl -U "$db_user" -d "$drill" < coaching-before.dump
 mkdir operator
 cp /var/lib/eduivme/releases/lgs-target-import-20261019/bundle-r1/operator/* operator/
 cp /var/lib/eduivme/releases/lgs-target-import-20261019/TargetAdmissions-diagnostic.dll operator/TargetAdmissions.dll
 echo 'e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d  operator/TargetAdmissions.dll' | sha256sum -c -
 docker exec "$app" mkdir -p /tmp/lgs-followup-20261019
 docker cp operator "$app:/tmp/lgs-followup-20261019/operator"
 docker cp packages "$app:/tmp/lgs-followup-20261019/packages"
 touch prepared
 echo 'Private server backup and restored rehearsal database prepared; production unchanged.'
 ;;
rehearsal|apply)
 test -f prepared
 database=$drill;extra=--rehearsal
 if [[ "$mode" == apply ]];then test -f rehearsal-passed;database=coaching_db;extra=;test ! -f apply-passed;fi
 fingerprint "$database" before
 for package in packages/*.json;do
  file=$(basename "$package")
  docker exec "$app" sh -c '
   export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"
   exec dotnet /tmp/lgs-followup-20261019/operator/TargetAdmissions.dll --database "$1" --apply ${3:+$3} --package "/tmp/lgs-followup-20261019/packages/$2" --review-output "/tmp/lgs-followup-20261019/$1-$2.review.json"
  ' sh "$database" "$file" "$extra" > "$mode-$file.log"
  cat "$mode-$file.log"
 done
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-identity-review.log"
 # The exact reviewed correction must replay as a no-op.
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-identity-replay.log"
 fingerprint "$database" after
 cmp "$mode-before-protected.json" "$mode-after-protected.json"
 cmp "$mode-before-schools.json" "$mode-after-schools.json"
 cmp "$mode-before-source-cells.json" "$mode-after-source-cells.json"
 touch "$mode-passed"
 echo "$mode passed: old source cells, all original schools and protected user/work tables unchanged."
 ;;
verify)
 test -f apply-passed
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT b."Stage",r."MatchStatus",count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" GROUP BY 1,2 ORDER BY 1,2;
SELECT r."City",count(*) FROM coaching.target_admission_records r WHERE r."ExternalCode" LIKE 'reviewed-lgs2026:%' GROUP BY 1 ORDER BY 1;
SELECT count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE b."Stage"='Catalog' AND (r."MinimumScore" IS NOT NULL OR r."Percentile" IS NOT NULL);
SELECT count(*) FROM coaching.target_admission_records WHERE "ExternalCode" IN ('lgs-kayseri-mtal-research:3','lgs-kayseri-mtal-research:14','lgs-kayseri-mtal-research:15','lgs-kayseri-mtal-research:17') AND "MatchStatus"='Verified' AND "SchoolId" IS NOT NULL;
COMMIT;
SQL
 ;;
cleanup)
 test -f apply-passed;[[ "$drill" == coaching_lgs2026_rehearsal_20261019 ]]
 docker exec postgres dropdb -U "$db_user" "$drill"
 echo 'Only the temporary rehearsal database was removed; server backup retained.'
 ;;
*)exit 2;;
esac
