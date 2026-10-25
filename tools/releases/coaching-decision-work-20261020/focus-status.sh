set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
if test -s focus-built.txt; then echo FOCUS_BUILD_COMPLETE; cat focus-built.txt; else tail -n 6 focus-driver.log; tail -n 6 focus-portals-build.log; fi
