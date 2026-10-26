set -euo pipefail
release=/var/lib/eduivme/releases/coaching-development-20261020
cd "$release"
for step in prepared built deployed; do test ! -s "$step.txt" || cat "$step.txt"; done
test ! -f portals-build.log || tail -12 portals-build.log
if test -f prepared.txt; then
 for name in coaching identity notification; do printf '%s protected tables: ' "$name"; wc -l < "$name.before.txt"; done
 diff -u /var/lib/eduivme/releases/coaching-practice-adaptation-20261020/source/admin-panel/projects/staff-portal/src/app/app.component.ts source/admin-panel/projects/staff-portal/src/app/app.component.ts || [[ $? == 1 ]]
fi
