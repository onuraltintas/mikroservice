#!/usr/bin/env bash
set -euo pipefail
[[ "$MOCK_RELEASE" == /var/lib/eduivme/releases/baseline-gate-recovery-test-*/* ]] || exit 2
if [[ "${0##*/}" == curl ]]; then echo Healthy; exit 0; fi
printf '%s\n' "$*" >> "$MOCK_RELEASE/docker-calls.txt"
case "${1:-}" in
 inspect)
  case "$*" in
   *Config.Labels*) echo "$MOCK_RELEASE/mock-compose.yml" ;;
   *Config.Image*) echo old-tag ;;
   *Config.Env*) echo 'ConnectionStrings__DefaultConnection=Host=postgres;Database=speedreading_owned_db' ;;
   *IPAddress*) echo 127.0.0.1 ;;
   *NetworkSettings.Networks*) echo fake-network ;;
   *State.Health.Status*) echo healthy ;;
   *) echo old-image ;;
  esac ;;
 image) printf '%040d\n' 1 ;;
 compose) exit 0 ;;
 ps) echo 'other-service same-id same-image' ;;
 stop) [[ "$MOCK_MODE" != INT ]] || kill -s INT "$PPID" ;;
 exec)
  if [[ "$*" == *pg_dump* ]]; then
   [[ "$MOCK_MODE" != failure ]] || exit 9
   echo fake-dump
  elif [[ "$*" == *pg_restore* ]]; then echo fake-contents
  elif [[ "$*" == *psql* ]]; then cat >/dev/null; echo protected-inventory
  fi ;;
 run)
  [[ "$MOCK_MODE" != TERM ]] || kill -s TERM "$PPID"
  echo '{"Programs":20,"Days":560,"Slots":3640}' ;;
 *) exit 2 ;;
esac
