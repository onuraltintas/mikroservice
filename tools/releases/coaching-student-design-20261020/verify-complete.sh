set -euo pipefail
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r5
cd "$release"
test -s built.txt
cmp overlay/src/app/features/coaching-portal/layout/coaching-student-surfaces.scss source/admin-panel/src/app/features/coaching-portal/layout/coaching-student-surfaces.scss
if grep -R -n -E 'window\.(confirm|alert|prompt)[[:space:]]*\(|window:beforeunload' source/admin-panel/src/app/features/coaching-portal/pages --include='*.component.ts'; then exit 1; fi
if grep -q 'work-links\|study-apply' source/admin-panel/src/app/features/coaching-portal/pages/weekly-review.component.ts; then exit 1; fi
docker image inspect "$(<previous-admin-panel.txt)" "$(<built-admin-panel.txt)" > runtime-comparison.json
python3 - <<'PY'
import json
old,new=json.load(open('runtime-comparison.json'))
for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal']:
    assert old['Config'].get(key)==new['Config'].get(key), key
assert len(new['RootFS']['Layers']) < len(old['RootFS']['Layers'])
print('Runtime settings preserved; image layer history reduced.')
PY
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
cd /var/lib/eduivme/releases/coaching-student-design-20261020
bash history.sh after
bash history.sh compare
cd "$release"
cat deployed.txt
tail -n 6 public-assets.log
