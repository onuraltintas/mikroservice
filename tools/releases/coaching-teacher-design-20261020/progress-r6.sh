set -euo pipefail
cd /var/lib/eduivme/releases/coaching-teacher-design-20261020-r6
if test -s built.txt; then echo 'BUILD_COMPLETE'; cat built.txt; else tail -n 20 build-driver.log; tail -n 12 build.log; fi

