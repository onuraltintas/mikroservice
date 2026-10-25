set -euo pipefail
release=/var/lib/eduivme/releases/coaching-student-design-20261020-r5
cd "$release"
bash deploy.sh verify
for s in admin-panel staff-portal coaching-service; do
 docker inspect "eduivme-production-$s-1" --format '{{.Name}} {{.State.Status}} restarts={{.RestartCount}}'
done
test "$(docker inspect eduivme-production-staff-portal-1 --format '{{.Id}}')" = "$(awk '$1=="eduivme-production-staff-portal-1" {print $2}' containers.before.txt)"
diff -qr /var/lib/eduivme/releases/coaching-student-design-20261020-r2/source/admin-panel/src/app/features/coaching-portal/pages source/admin-panel/src/app/features/coaching-portal/pages > final-source-diff.txt || test "$?" = 1
test "$(wc -l < final-source-diff.txt)" -eq 3
echo 'Final source differences: assignment presentation only, plus exact student surface overlay.'
grep -q 'assignment-heading' source/admin-panel/src/app/features/coaching-portal/pages/student-assignment-detail.component.html
if grep -n -E 'window\.(confirm|alert|prompt)[[:space:]]*\(|window:beforeunload' source/admin-panel/shared/coaching-profile.component.ts; then exit 1; fi
grep -E 'Initial total|Application bundle generation complete' build.log | head -n 3
cd /var/lib/eduivme/releases/coaching-student-design-20261020
bash history.sh after
bash history.sh compare
