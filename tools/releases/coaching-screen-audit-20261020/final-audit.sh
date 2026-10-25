set -euo pipefail
cd /var/lib/eduivme/releases/coaching-screen-audit-20261020-r2
while read -r name id; do
 case "$name" in eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
 current=$(docker inspect "$name" --format '{{.Id}}')
 if [[ "$current" != "$id" ]]; then echo "External container changed since release preparation: $name"; fi
done < containers.before.txt
bash history.sh after
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db -v started="$(<prepared.txt)" <<'SQL' > authorized-login-row.txt
BEGIN READ ONLY;
SELECT 'identity.users|' || md5(to_jsonb(u)::text) FROM identity.users u
WHERE coalesce(to_jsonb(u)->>'Email',to_jsonb(u)->>'email') = 'testogrenci@eduivme.com'
AND coalesce(to_jsonb(u)->>'LastLoginAt',to_jsonb(u)->>'last_login_at')::timestamptz >= :'started'::timestamptz;
COMMIT;
SQL
python3 - <<'PY'
from collections import Counter
from pathlib import Path
for database in ['coaching_db', 'identity_db', 'notification_db']:
 before=Counter(Path(database+'.rows.before.txt').read_text().splitlines())
 after=Counter(Path(database+'.rows.after.txt').read_text().splitlines())
 missing=before-after
 if database != 'identity_db':
  assert not missing, database+' existing records changed'
 else:
  allowed=Path('authorized-login-row.txt').read_text().splitlines()
  assert len(allowed)==1, 'Expected one authorized test-student login'
  assert sum(missing.values())==1 and all(row.startswith('identity.users|') for row in missing), 'Unexpected Identity changes'
  changed_users=Counter({row:n for row,n in (after-before).items() if row.startswith('identity.users|')})
  assert changed_users==Counter(allowed), 'A different account changed'
  assert sum(n for row,n in before.items() if row.startswith('identity.users|'))==sum(n for row,n in after.items() if row.startswith('identity.users|')), 'Account count changed'
 print(database+': all existing records preserved'+(' except the authorized test-student login activity row; account count unchanged' if database=='identity_db' else ' byte-for-byte'))
PY
for service in admin-panel staff-portal; do
 expected=$(<"built-$service.txt")
 actual=$(docker inspect "eduivme-production-$service-1" --format '{{.Image}}')
 [[ "$actual" == "$expected" ]]
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'
done
bash public-check.sh
