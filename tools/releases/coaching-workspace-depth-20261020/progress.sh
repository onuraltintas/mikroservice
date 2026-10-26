set -euo pipefail
cd /var/lib/eduivme/releases/coaching-workspace-depth-20261020-r2
if test -s built.txt; then echo BUILD_COMPLETE; cat built.txt; else tail -n 18 build-driver.log; tail -n 10 build.log; fi
