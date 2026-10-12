#!/usr/bin/env bash
set -euo pipefail
# Runs only with an isolated fake Docker; never talks to the daemon or a real DB.
source_root=$(cd "$(dirname "$0")/.." && pwd)
test_root=$(mktemp -d /var/lib/eduivme/releases/continuation-recovery-test-XXXXXX)
[[ "$test_root" == /var/lib/eduivme/releases/continuation-recovery-test-* ]] || exit 2
trap 'rm -rf -- "$test_root"' EXIT
for mode in success failure INT TERM; do
 release="$test_root/$mode"
 mkdir -p "$release/mock-bin"
 cp "$source_root/scripts/deploy-speed-reading-continuation.sh" "$release/deploy.sh"
 cp "$source_root/tests/fixtures/continuation-docker-mock.sh" "$release/mock-bin/docker"
 cp "$source_root/tests/fixtures/continuation-docker-mock.sh" "$release/mock-bin/curl"
 chmod +x "$release/mock-bin/docker"
 chmod +x "$release/mock-bin/curl"
 printf '%040d\n' 1 > "$release/source-revision.txt"
 for item in api web staff; do printf 'old-image\n' > "$release/previous-$item-image.txt"; done
 printf 'services: {}\n' > "$release/production.override.yml"
 printf 'services: {}\n' > "$release/rollback.override.yml"
 printf 'services: {}\n' > "$release/mock-compose.yml"
 printf 'passed\n' > "$release/rehearsal-passed.txt"
 touch "$release/inventory.sql" "$release/permission.sql"
 set +e
 (cd "$release"; export MOCK_MODE="$mode" MOCK_RELEASE="$release"; PATH="$release/mock-bin:$PATH" bash ./deploy.sh promote > output.txt 2>&1)
 status=$?
 set -e
 if [[ "$mode" == success && "$status" != 0 ]]; then
  cat "$release/output.txt" >&2
  cat "$release/docker-calls.txt" >&2
 fi
 if [[ "$mode" == success ]]; then
  [[ "$status" == 0 && -s "$release/deployed-at.txt" ]]
  ! grep -q rollback.override.yml "$release/docker-calls.txt"
 else
  [[ "$status" != 0 && ! -e "$release/deployed-at.txt" ]]
  grep -q 'rollback.override.yml up -d --no-deps --pull never speed-reading-service speed-reading-frontend staff-portal' "$release/docker-calls.txt"
  [[ "$mode" != INT || "$status" == 130 ]]
  [[ "$mode" != TERM || "$status" == 143 ]]
 fi
 echo "$mode recovery check passed (status $status)"
done
