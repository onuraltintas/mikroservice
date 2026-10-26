set -euo pipefail
release=/var/lib/eduivme/releases/coaching-student-design-20261020
base=/var/lib/eduivme/releases/coaching-weekly-followup-20261020
cd "$release"
test -s built.txt
for part in src projects shared; do
 diff -qr --exclude=node_modules --exclude=dist --exclude=.angular "$base/source/admin-panel/$part" "$release/source/admin-panel/$part" || [[ $? == 1 ]]
done > source-differences.txt
if grep -R -n -E 'window\.(confirm|alert|prompt)[[:space:]]*\(|window:beforeunload' source/admin-panel/src/app/features/coaching-portal/pages --include='*.component.ts'; then exit 1; fi
if grep -q 'work-links\|study-apply' source/admin-panel/src/app/features/coaching-portal/pages/weekly-review.component.ts; then echo 'Unexpected undeployed decision API dependency'; exit 1; fi
while IFS= read -r file; do file=${file#overlay/}; cmp "overlay/$file" "source/admin-panel/$file"; done < <(find overlay -type f | sort)
echo 'Student browser-dialog scan and exact design overlays verified.'
cat source-differences.txt
