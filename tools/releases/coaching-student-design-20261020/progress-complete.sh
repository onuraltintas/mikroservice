set -euo pipefail
cd /var/lib/eduivme/releases/coaching-student-design-20261020-r5
if test -s built.txt; then echo 'BUILD_COMPLETE'; cat built.txt; else tail -n 24 build-driver.log; tail -n 14 build.log; fi
if test -s deployed.txt; then echo 'DEPLOYED'; cat deployed.txt; fi

