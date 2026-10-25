set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/lgs-review-r3-20261019
drill=coaching_lgs2026_rehearsal_20261019
app=eduivme-production-coaching-service-1
db_user=$(docker exec postgres printenv POSTGRES_USER)
cd "$dir"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
mode=${1:?Explicit mode required}
fingerprint(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < preservation.sql > "$mode-$2-protected.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < catalog-preservation.sql > "$mode-$2-catalog.json"
}
case "$mode" in
prepare)
 test ! -f prepared
 sha256sum -c payload.sha256 > payload-verified.log
 docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching-before.dump
 sha256sum coaching-before.dump > backup.sha256
 docker exec postgres createdb -U "$db_user" "$drill"
 docker exec -i postgres pg_restore --exit-on-error --no-owner --no-acl -U "$db_user" -d "$drill" < coaching-before.dump
 cp -r /var/lib/eduivme/releases/lgs-followup-20261019/operator operator
 echo 'e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d  operator/TargetAdmissions.dll' | sha256sum -c -
 docker exec "$app" mkdir -p /tmp/lgs-review-r3-20261019
 docker cp operator "$app:/tmp/lgs-review-r3-20261019/operator"
 docker exec "$app" mkdir -p /tmp/lgs-review-r3-20261019/packages
 docker cp packages/. "$app:/tmp/lgs-review-r3-20261019/packages/"
 touch prepared
 echo 'Server-local backup and restored rehearsal ready; production unchanged.'
 ;;
refresh)
 test -f prepared;test ! -f apply-passed
 sha256sum -c payload.sha256 > payload-verified.log
 docker cp packages/. "$app:/tmp/lgs-review-r3-20261019/packages/"
 if [[ -f rehearsal-passed ]];then mv rehearsal-passed rehearsal-two-provinces-passed;fi
 echo 'Expanded evidence package verified; production unchanged; rehearsal must pass again.'
 ;;
rehearsal|apply)
 test -f prepared
 database=$drill;extra=--rehearsal
 if [[ "$mode" == apply ]];then test -f rehearsal-passed;test ! -f apply-passed;database=coaching_db;extra=;fi
 fingerprint "$database" before
 for package in packages/*.json;do
  file=$(basename "$package")
  docker exec "$app" sh -c '
   export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"
   exec dotnet /tmp/lgs-review-r3-20261019/operator/TargetAdmissions.dll --database "$1" --apply ${3:+$3} --package "/tmp/lgs-review-r3-20261019/packages/$2" --review-output "/tmp/lgs-review-r3-20261019/$1-$2.review.json"
  ' sh "$database" "$file" "$extra" > "$mode-$file.log"
  cat "$mode-$file.log"
 done
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-identity.log"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < full-catalog-hash.sql > "$mode-replay-before.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-replay.log"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < full-catalog-hash.sql > "$mode-replay-after.json"
 cmp "$mode-replay-before.json" "$mode-replay-after.json"
 if [[ "$mode" == rehearsal ]];then
  { printf '%s\n' 'BEGIN;' 'UPDATE coaching.target_admission_records SET "City"='"'INVALID-REVIEW-CITY'"' WHERE "ExternalCode"='"'lgs-kayseri-mtal-research:1'"';';cat identity-review.sql; } > invalid-review.sql
  if docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < invalid-review.sql > negative-test.log 2>&1;then
   echo 'Invalid source identity was accepted';exit 1
  fi
  grep -q 'Kayseri source identity or review scope changed' negative-test.log
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < full-catalog-hash.sql > negative-after.json
  cmp "$mode-replay-after.json" negative-after.json
  echo 'Invalid identity rejected and rehearsal transaction rolled back.'
 fi
 fingerprint "$database" after
 cmp "$mode-before-protected.json" "$mode-after-protected.json"
 cmp "$mode-before-catalog.json" "$mode-after-catalog.json"
 touch "$mode-passed"
 echo "$mode passed: all existing schools, source cells, batches, goals and work tables preserved; replay unchanged."
 ;;
verify)
 test -f apply-passed
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT r."MatchStatus",count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE b."Year"=2026 AND b."ExamCode"='LGS' GROUP BY 1 ORDER BY 1;
SELECT r."City",r."Institution",r."Quota",r."MatchStatus" FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE b."SourceUrl" IN ('https://bif.meb.k12.tr/tema/okulumuz_hakkinda.html','https://hcesb.meb.k12.tr/tema/okulumuz_hakkinda.php','https://haydarfadimkocaerfenlisesi.meb.k12.tr/tema/okulumuz_hakkinda.html');
SELECT r."ExternalCode",r."MatchStatus",s."Name",s."District" FROM coaching.target_admission_records r LEFT JOIN coaching.target_schools s ON s."Id"=r."SchoolId" WHERE r."ExternalCode"='lgs-kayseri-mtal-research:1';
SELECT count(*) FROM coaching.target_admission_records WHERE "ExternalCode" IN ('ist:751600:2036f00ba075bf5a90969e62','ist:751600:1d09de8afd34393d38426ce5') AND "MatchStatus"='Ambiguous' AND "SchoolId" IS NULL AND "Issue" LIKE '%768129%';
SELECT count(*) FROM coaching.target_admission_records r JOIN coaching.target_admission_batches b ON b."Id"=r."BatchId" WHERE b."Stage"='Catalog' AND (r."MinimumScore" IS NOT NULL OR r."Percentile" IS NOT NULL);
COMMIT;
SQL
 ;;
cleanup)
 test -f apply-passed;[[ "$drill" == coaching_lgs2026_rehearsal_20261019 ]]
 docker exec postgres dropdb -U "$db_user" "$drill"
 echo 'Temporary restored rehearsal removed; private server backup retained.'
 ;;
*)exit 2;;
esac
