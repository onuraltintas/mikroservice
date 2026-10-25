set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2
sed -i "s/table_name='Users'/table_name='users'/g" protected-records.sh
bash protected-records.sh reference
bash protected-records.sh verify
