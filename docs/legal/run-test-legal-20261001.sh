#!/usr/bin/env bash
set -euo pipefail
release=/var/lib/eduivme/releases/test-legal-20261001
backup=/var/lib/eduivme/backups/test-legal-20261001
mkdir -p "$backup"
chmod 700 "$backup"
if [[ "${1:-check}" == check ]]; then
  test ! -e "$backup/identity_db.dump"
  docker exec postgres sh -c 'exec pg_dump -U "$POSTGRES_USER" -Fc identity_db' > "$backup/identity_db.dump"
  test -s "$backup/identity_db.dump"
  docker exec -i postgres pg_restore -l > /dev/null < "$backup/identity_db.dump"
  sed 's/^COMMIT;$/ROLLBACK;/' "$release/publish.sql" | docker exec -i postgres sh -c 'exec psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d identity_db'
elif [[ "$1" == publish ]]; then
  test -s "$backup/identity_db.dump"
  docker exec -i postgres sh -c 'exec psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d identity_db' < "$release/publish.sql"
else
  exit 2
fi
