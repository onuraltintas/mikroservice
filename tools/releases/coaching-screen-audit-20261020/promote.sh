set -euo pipefail
cd /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2
base=/var/lib/eduivme/releases/coaching-workspace-depth-20261020-r2
test -s built.txt
diff -qr --exclude=coaching-workspace-depth.scss "$base/source/admin-panel/src" source/admin-panel/src
diff -qr "$base/source/admin-panel/projects" source/admin-panel/projects
diff -qr "$base/source/admin-panel/shared" source/admin-panel/shared
diff -qr "$base/source/shared" source/shared
cmp overlay/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss source/admin-panel/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss
echo 'Only the shared coaching SCSS changed.'
for s in admin-panel staff-portal; do
 docker image inspect "$(<"previous-$s.txt")" "$(<"built-$s.txt")" > "runtime-comparison-$s.json"
done
python3 - <<'PY'
import json
for service in ['admin-panel','staff-portal']:
 old,new=json.load(open('runtime-comparison-'+service+'.json'))
 for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal','Healthcheck']:
  assert old['Config'].get(key)==new['Config'].get(key),(service,key)
print('Both runtime configurations preserved.')
PY
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
cat deployed.txt
cat public-assets.log
