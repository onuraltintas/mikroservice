set -euo pipefail
release=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r3
base=/var/lib/eduivme/releases/coaching-student-design-20261020-r7
cd "$release"
test -s built.txt
diff -qr "$base/source/admin-panel/src" source/admin-panel/src
diff -qr "$base/source/admin-panel/shared" source/admin-panel/shared
diff -qr "$base/source/shared" source/shared
if grep -R -n -E 'window\.(confirm|alert|prompt)[[:space:]]*\(|window:beforeunload' source/admin-panel/projects/staff-portal/src/app/features/coaching --include='coaching-teacher*.ts' --exclude='*.spec.ts'; then exit 1; fi
if grep -q 'work-links\|study-apply' source/admin-panel/src/app/features/coaching-portal/pages/weekly-review.component.ts; then exit 1; fi
while IFS= read -r file; do file=${file#overlay/}; cmp "overlay/$file" "source/admin-panel/$file"; done < <(find overlay -type f | sort)
docker image inspect "$(<previous-staff-portal.txt)" "$(<built-staff-portal.txt)" > runtime-comparison.json
python3 - <<'PY'
import json
old,new=json.load(open('runtime-comparison.json'))
for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal','Healthcheck']:
 assert old['Config'].get(key)==new['Config'].get(key),key
print('Existing Nginx runtime settings and health check preserved.')
PY
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
cat deployed.txt
tail -n 6 public-assets.log

