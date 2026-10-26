set -euo pipefail
umask 077
case "${1:-}" in drill) database=coaching_yks_drill_20261019_r3;; live) database=coaching_db;; *)exit 2;;esac
cd /var/lib/eduivme/releases/coaching-yks-20261019-r3
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc -t coaching.study_catalog_topics -t coaching.study_catalog_units > "catalog-order-$1.before.backup"
docker exec -i postgres psql -X -v ON_ERROR_STOP=1 -U "$db_user" -d "$database" < order-apply.sql
