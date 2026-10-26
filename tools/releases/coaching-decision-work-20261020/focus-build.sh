set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
test -s validation-fixed.txt
test ! -e focus-built.txt
cp source/admin-panel/src/app/features/coaching-portal/pages/student-study-plans.component.ts student-study-plans.before-focus.ts
cp candidate-student-study-plans.ts source/admin-panel/src/app/features/coaching-portal/pages/student-study-plans.component.ts
docker run --rm --name coaching-decision-focus-builder-20261021 --memory=4g --cpus=2 --network none -v "$PWD/source/admin-panel:/work/admin-panel" -v "$PWD/source/shared:/work/shared:ro" -w /work/admin-panel node:24-alpine node build-portals.cjs > focus-portals-build.log 2>&1
cp -a source/admin-panel/dist/admin-panel/. payload/admin/
cp -a source/admin-panel/dist/staff-portal/browser/. payload/staff/
for s in admin-panel staff-portal; do
 docker image inspect "$(<"built-$s.txt")" > runtime-image.json
 docker run --rm --memory=1g --network none -v "$PWD:/release" -w /release node:24-alpine node "runtime-$s.cjs" > "focus-runtime-$s.log"
 docker tag "$(<"built-$s.txt")" "eduivme/$s:coaching-decision-focus-base"
 docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$s:coaching-decision-focus-base" --build-arg SOURCE_REVISION=coaching-decision-work-focus-20261021 -f "$s.Dockerfile" -t "eduivme/$s:coaching-decision-work-focus-20261021" . > "focus-$s-build.log" 2>&1
 docker image inspect "eduivme/$s:coaching-decision-work-focus-20261021" --format '{{.Id}}' > "focus-built-$s.txt"
done
date -u +%FT%TZ > focus-built.txt
