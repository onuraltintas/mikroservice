set -euo pipefail
cd /var/lib/eduivme/releases/coaching-workspace-depth-20261020-r2
base=/var/lib/eduivme/releases/coaching-teacher-design-20261020-r6
test -s built.txt
diff -qr --exclude=styles.scss --exclude=coaching-workspace-depth.scss "$base/source/admin-panel/src" source/admin-panel/src
diff -qr --exclude=styles.scss "$base/source/admin-panel/projects" source/admin-panel/projects
diff -qr "$base/source/admin-panel/shared" source/admin-panel/shared
diff -qr "$base/source/shared" source/shared
python3 - <<'PY'
from pathlib import Path
base=Path('/var/lib/eduivme/releases/coaching-teacher-design-20261020-r6/source/admin-panel')
for path,anchor,line in [
 ('src/styles.scss',"@use './app/features/coaching-portal/layout/coaching-student-surfaces';","@use './app/features/coaching-portal/layout/coaching-workspace-depth';"),
 ('projects/staff-portal/src/styles.scss',"@use './app/coaching-teacher-theme';","@use '../../../src/app/features/coaching-portal/layout/coaching-workspace-depth';")]:
 assert (Path('source/admin-panel')/path).read_text()==(base/path).read_text().replace(anchor,anchor+'\n'+line),path
print('Only shared decorative SCSS and its two imports changed.')
PY
for s in admin-panel staff-portal; do
 docker image inspect "$(<"previous-$s.txt")" "$(<"built-$s.txt")" > "runtime-comparison-$s.json"
done
python3 - <<'PY'
import json
for service in ['admin-panel','staff-portal']:
 old,new=json.load(open('runtime-comparison-'+service+'.json'))
 for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal','Healthcheck']:
  assert old['Config'].get(key)==new['Config'].get(key),(service,key)
print('Both existing runtime configurations preserved.')
PY
bash deploy.sh promote
bash deploy.sh verify
bash public-check.sh > public-assets.log
cat deployed.txt
cat public-assets.log
