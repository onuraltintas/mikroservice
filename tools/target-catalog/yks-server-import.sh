set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/yks-target-import-20261019
drill=coaching_lgs2026_rehearsal_20261019
app=eduivme-production-coaching-service-1
db_user=$(docker exec postgres printenv POSTGRES_USER)
mode=${1:?Explicit mode required}
cd "$dir"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
fingerprint(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < preservation.sql > "$mode-$2-protected.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" < catalog-preservation.sql > "$mode-$2-catalog.json"
}
case "$mode" in
prepare)
 test ! -f prepared
 sha256sum -c payload.sha256 > payload-verified.log
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM coaching.target_admission_batches WHERE "ExamCode"='YKS') OR EXISTS(SELECT 1 FROM coaching.target_university_programs WHERE "Source"='osym-2026') THEN
  RAISE EXCEPTION 'Existing YKS import requires a new reviewed preservation baseline';
 END IF;
END $$;
SQL
 docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching-before.dump
 sha256sum coaching-before.dump > backup.sha256
 docker exec postgres createdb -U "$db_user" "$drill"
 docker exec -i postgres pg_restore --exit-on-error --no-owner --no-acl -U "$db_user" -d "$drill" < coaching-before.dump
 cp -r /var/lib/eduivme/releases/lgs-followup-20261019/operator operator
 echo 'e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d  operator/TargetAdmissions.dll' | sha256sum -c -
 docker exec "$app" mkdir -p /tmp/yks-target-import-20261019/packages
 docker cp operator "$app:/tmp/yks-target-import-20261019/operator"
 docker cp packages/. "$app:/tmp/yks-target-import-20261019/packages/"
 touch prepared
 echo 'Server-local backup restored for rehearsal; production unchanged.'
 ;;
rehearsal|apply)
 test -f prepared
 database=$drill;extra=--rehearsal
 if [[ "$mode" == apply ]];then test -f rehearsal-passed;test ! -f apply-passed;database=coaching_db;extra=;fi
 fingerprint "$database" before
 docker exec "$app" sh -c '
  export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"
  exec dotnet /tmp/yks-target-import-20261019/operator/TargetAdmissions.dll --database "$1" --apply ${2:+$2} --package /tmp/yks-target-import-20261019/packages/tyt-ayt.json --review-output "/tmp/yks-target-import-20261019/$1.review.json"
 ' sh "$database" "$extra" > "$mode-import.log"
 cat "$mode-import.log"
 fingerprint "$database" after
 cmp "$mode-before-protected.json" "$mode-after-protected.json"
 cmp "$mode-before-catalog.json" "$mode-after-catalog.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < verify.sql > "$mode-verification.log"
 cat "$mode-verification.log"
 touch "$mode-passed"
 echo "$mode passed: existing schools, programs, admission records, goals and work tables unchanged."
 ;;
verify)
 test -f apply-passed
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db < verify.sql
 ;;
verify-ui)
 test -f apply-passed
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db < preservation.sql > ui-protected.json
 python3 - <<'PY'
import json
before=json.load(open('apply-after-protected.json'))
after=json.load(open('ui-protected.json'))
before={x['table_name']:x for x in before if x['table_name']!='academic_goals'}
after={x['table_name']:x for x in after if x['table_name']!='academic_goals'}
if before!=after:
    raise SystemExit('Unexpected UI side effect: '+str([k for k in before if before[k]!=after.get(k)]))
print('All non-goal protected tables unchanged after UI tests.')
PY
 for database in "$drill" coaching_db;do
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" <<'SQL' > "$database-old-goals.json"
SELECT count(*),md5(string_agg(md5(to_jsonb(g)::text),'' ORDER BY md5(to_jsonb(g)::text))) FROM coaching.academic_goals g WHERE title NOT IN ('YKS katalog canlı doğrulama · öğrenci · 19.10.2026','YKS katalog canlı doğrulama · öğretmen · 19.10.2026');
SQL
 done
 cmp "$drill-old-goals.json" coaching_db-old-goals.json
 cat coaching_db-old-goals.json
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT g.title,p."ProgramCode",p."ScoreType",p."IsActive" FROM coaching.academic_goals g JOIN coaching.target_university_programs p ON p."Id"=g.target_university_program_id WHERE title IN ('YKS katalog canlı doğrulama · öğrenci · 19.10.2026','YKS katalog canlı doğrulama · öğretmen · 19.10.2026') ORDER BY title;
SQL
 ;;
identity-rehearsal|identity-apply)
 test -f apply-passed
 database=$drill;extra=--rehearsal
 if [[ "$mode" == identity-apply ]];then
  test -f identity-rehearsal-passed;test ! -f identity-apply-passed;database=coaching_db;extra=
  docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching-before-identity.dump
  sha256sum coaching-before-identity.dump > identity-backup.sha256
 fi
 sha256sum -c identity-payload.sha256 > "$mode-payload.log"
 docker cp packages/tyt-ayt.json "$app:/tmp/yks-target-import-20261019/packages/tyt-ayt.json"
 fingerprint "$database" before
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-preservation.sql > "$mode-before-identity.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-review.log"
 # The scoped correction and the official package must both replay without adding rows.
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-review.sql > "$mode-replay.log"
 docker exec "$app" sh -c '
  export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"
  exec dotnet /tmp/yks-target-import-20261019/operator/TargetAdmissions.dll --database "$1" --apply ${2:+$2} --package /tmp/yks-target-import-20261019/packages/tyt-ayt.json
 ' sh "$database" "$extra" > "$mode-import.log"
 cat "$mode-import.log"
 fingerprint "$database" after
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < identity-preservation.sql > "$mode-after-identity.json"
 cmp "$mode-before-identity.json" "$mode-after-identity.json"
 cmp "$mode-before-protected.json" "$mode-after-protected.json"
 cmp "$mode-before-catalog.json" "$mode-after-catalog.json"
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < verify.sql > "$mode-verification.log"
 cat "$mode-verification.log"
 touch "$mode-passed"
 echo 'Reviewed identity withheld; every other admission row, legacy program and protected work table unchanged.'
 ;;
cleanup)
 test -f apply-passed;[[ "$drill" == coaching_lgs2026_rehearsal_20261019 ]]
 docker exec postgres dropdb -U "$db_user" "$drill"
 echo 'Only temporary rehearsal database removed; server backup retained.'
 ;;
*)exit 2;;
esac
