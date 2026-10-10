#!/usr/bin/env bash
set -euo pipefail
# Exercise the actual recovery function with mocked Docker/DB calls: no live writes.
directory=$(cd "$(dirname "$0")" && pwd)
recovery=$(sed -n '/^recover() {/,/^}/p' "$directory/deploy.sh")
test -n "$recovery"
run_case() {
  local published=$1 trigger=$2 expected_action=$3 expected_exit=$4 code=0 output
  output=$(bash -c '
    set -e
    eval "$1"
    published_mock=$2
    trigger=$3
    release=$4
    backend_stopped=true
    backend=mock-backend
    psql_owned() { if [[ "$published_mock" == unknown ]]; then return 1; fi; echo "$published_mock"; }
    docker() { echo OLD_RESTART >&2; }
    forward_start() { echo FORWARD_RESTART; }
    compose=(forward_start)
    trap recover EXIT
    trap "exit 130" INT
    trap "exit 143" TERM
    case "$trigger" in
      failure) false ;;
      term) kill -TERM $$ ;;
      int) kill -INT $$ ;;
    esac
  ' _ "$recovery" "$published" "$trigger" "$directory" 2>&1) || code=$?
  [[ "$code" == "$expected_exit" && "$output" == "$expected_action" ]] \
    || { echo "Recovery failed: $published / $trigger / $code / $output" >&2; exit 1; }
}
run_case 0 failure OLD_RESTART 1
run_case 5 failure FORWARD_RESTART 1
run_case 0 term OLD_RESTART 143
run_case 5 int FORWARD_RESTART 130
for published in 1 2 3 4 unknown; do
  run_case "$published" failure 'CRITICAL: publication state is not verified; manual service recovery required' 1
done
echo 'PASS: nine failure/signal recovery paths; Docker and DB calls mocked'
