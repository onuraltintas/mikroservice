set -euo pipefail
umask 077
dir=/var/lib/eduivme/releases/lgs-target-import-20261019
mkdir -p "$dir"
chmod 700 "$dir"
db_user=$(docker exec postgres printenv POSTGRES_USER)
test ! -e "$dir/coaching-before.dump"
docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > "$dir/coaching-before.dump"
test -s "$dir/coaching-before.dump"
sha256sum "$dir/coaching-before.dump" > "$dir/coaching-before.dump.sha256"
docker exec eduivme-production-coaching-service-1 dotnet --list-runtimes
du -h "$dir/coaching-before.dump"
