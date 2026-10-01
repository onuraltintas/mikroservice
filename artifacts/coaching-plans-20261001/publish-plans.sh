#!/usr/bin/env bash
set -euo pipefail
cd /var/lib/eduivme/releases/coaching-plans-20261001
actor=$(docker exec postgres psql -U eduplatform -d identity_db -Atc "SELECT \"Id\" FROM identity.users WHERE lower(\"Email\") = 'admin@eduivme.com';")
[[ "$actor" =~ ^[0-9a-fA-F-]{36}$ ]]
case "${1:-dry-run}" in
backup)
  install -d -m 700 /var/lib/eduivme/backups/coaching-plans-20261001
  backup=/var/lib/eduivme/backups/coaching-plans-20261001/coaching_db.dump
  test ! -e "$backup"
  umask 077
  docker exec postgres pg_dump -U eduplatform -d coaching_db -Fc > "$backup"
  test -s "$backup"
  docker exec -i postgres pg_restore --list < "$backup" >/dev/null
  ;;
dry-run|apply)
  test -s /var/lib/eduivme/backups/coaching-plans-20261001/coaching_db.dump
  apply=false
  if [[ "$1" == apply ]]; then apply=true; fi
  docker exec -i postgres psql -U eduplatform -d coaching_db -v ON_ERROR_STOP=1 -v actor_id="$actor" -v apply="$apply" < plans.sql
  ;;
*) exit 2 ;;
esac
