set -euo pipefail
cd /var/lib/eduivme/releases/coaching-teacher-design-20261020
python3 - <<'PY'
import json
c=json.load(open('runtime-image.json'))[0]['Config']
print({k:c.get(k) for k in ['User','WorkingDir','Volumes','OnBuild']})
print('Healthcheck fields:',list((c.get('Healthcheck') or {}).keys()))
PY
