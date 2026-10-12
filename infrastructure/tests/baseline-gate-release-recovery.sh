#!/usr/bin/env bash
set -euo pipefail
# Isolated fake Docker only; never talks to a daemon or a real database.
source_root=$(cd "$(dirname "$0")/.." && pwd)
test_root=$(mktemp -d /var/lib/eduivme/releases/baseline-gate-recovery-test-XXXXXX)
[[ "$test_root" == /var/lib/eduivme/releases/baseline-gate-recovery-test-* ]] || exit 2
trap 'rm -rf -- "$test_root"' EXIT
for mode in success failure INT TERM; do
 release="$test_root/$mode"
 mkdir -p "$release/mock-bin"
 cp "$source_root/scripts/deploy-speed-reading-baseline-gate.sh" "$release/deploy.sh"
 for command in docker curl; do
  cp "$source_root/tests/fixtures/baseline-gate-docker-mock.sh" "$release/mock-bin/$command"
  chmod +x "$release/mock-bin/$command"
 done
 printf '%040d\n' 1 > "$release/source-revision.txt"
 for item in api web; do printf 'old-image\n' > "$release/previous-$item-image.txt"; done
 for file in production.override.yml rollback.override.yml mock-compose.yml; do printf 'services: {}\n' > "$release/$file"; done
 touch "$release/inventory.sql"
 set +e
 (cd "$release"; export MOCK_MODE="$mode" MOCK_RELEASE="$release"; PATH="$release/mock-bin:$PATH" bash ./deploy.sh promote > output.txt 2>&1)
 status=$?
 set -e
 if [[ "$mode" == success ]]; then
  [[ "$status" == 0 && -s "$release/deployed-at.txt" ]]
  ! grep -q rollback.override.yml "$release/docker-calls.txt"
 else
  [[ "$status" != 0 && ! -e "$release/deployed-at.txt" ]]
  grep -q 'rollback.override.yml up -d --no-deps --pull never speed-reading-service speed-reading-frontend' "$release/docker-calls.txt"
  [[ "$mode" != INT || "$status" == 130 ]]
  [[ "$mode" != TERM || "$status" == 143 ]]
 fi
 ! grep -Eq 'migrate-only|staff-portal|identity_db|permission.sql' "$release/docker-calls.txt"
 echo "$mode recovery check passed (status $status)"
done
