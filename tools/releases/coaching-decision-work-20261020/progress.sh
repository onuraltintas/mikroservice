set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
if test -s built.txt; then echo BUILD_COMPLETE; cat built.txt; else tail -n 5 build-driver.log; tail -n 8 portals-build.log; fi
