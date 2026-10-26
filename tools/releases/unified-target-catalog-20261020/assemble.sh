set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/unified-target-catalog-20261020
test -s drill-verified.txt
# SQL used in the successful rehearsal must remain unchanged.
sha256sum -c payload.sha256 > rehearsal-payload-check.log
tar -xzf backend-final.tar.gz
cp -a /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/dist/admin-panel/. payload/admin/
cp -a /var/lib/eduivme/releases/unified-target-catalog-20261020-build/portal-source/dist/staff-portal/browser/. payload/staff/
test -s payload/admin/browser/index.csr.html
test -s payload/staff/index.html
grep -q 'base href="/staff/"' payload/staff/index.html
cp deploy-final.sh deploy.sh
{ find payload -type f; printf '%s\n' *.Dockerfile incoming.sql lgs-schema.sql replace-mebi-lgs.sql unified-schema.sql verify.sql deploy.sh; } | LC_ALL=C sort | xargs sha256sum > payload.sha256
bash -n deploy.sh
bash deploy.sh build
