set -euo pipefail
dir=/var/lib/eduivme/releases/lgs-target-import-20261019
test -f "$dir/apply-passed"
test -s "$dir/coaching-before.dump"
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec postgres dropdb -U "$db_user" coaching_lgs2026_rehearsal_20261019
echo 'Only the temporary rehearsal database removed; production database and backup retained.'
