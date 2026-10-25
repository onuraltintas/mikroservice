set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
python3 - <<'PY'
from pathlib import Path
import subprocess,json
root=Path('source/admin-panel/src')
base=Path('/var/lib/eduivme/releases/coaching-screen-audit-20261020-r2/source/admin-panel/src')
allowed={'app/core/services/coaching-weekly-review.service.ts'}|{'app/features/coaching-portal/pages/'+name for name in ['weekly-review.component.ts','weekly-review.component.html','weekly-review.component.scss','weekly-decision-work.component.ts','weekly-decision-work.component.html']}
names={str(p.relative_to(root)) for p in root.rglob('*') if p.is_file()}|{str(p.relative_to(base)) for p in base.rglob('*') if p.is_file()}
changed={name for name in names if not (root/name).exists() or not (base/name).exists() or (root/name).read_bytes()!=(base/name).read_bytes()}
assert changed<=allowed,changed-allowed
print('Portal source changes limited to decision-work files: '+str(len(changed)))
for service in ['coaching-service','admin-panel','staff-portal']:
 old,new=json.loads(subprocess.check_output(['docker','image','inspect',Path('previous-'+service+'.txt').read_text().strip(),Path('built-'+service+'.txt').read_text().strip()]))
 for key in ['User','WorkingDir','Entrypoint','Cmd','Env','ExposedPorts','StopSignal','Healthcheck']:
  assert old['Config'].get(key)==new['Config'].get(key),(service,key)
print('All three runtime configurations preserved.')
PY
diff -qr /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2/source/admin-panel/projects source/admin-panel/projects
diff -qr /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2/source/admin-panel/shared source/admin-panel/shared
bash deploy.sh drill
bash deploy.sh promote
bash deploy.sh verify
