set -euo pipefail
app=eduivme-production-coaching-service-1
echo 'e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d  TargetAdmissions-diagnostic.dll' | (cd /var/lib/eduivme/releases/lgs-target-import-20261019 && sha256sum -c -)
sha256sum /var/lib/eduivme/releases/lgs-target-import-20261019/TargetAdmissions-diagnostic.dll > /var/lib/eduivme/releases/lgs-target-import-20261019/operator-final.sha256
docker cp /var/lib/eduivme/releases/lgs-target-import-20261019/TargetAdmissions-diagnostic.dll "$app:/tmp/lgs-target-import-20261019/operator/TargetAdmissions.dll"
docker exec "$app" sh -c 'export TARGET_ADMISSIONS_CONNECTION="$ConnectionStrings__DefaultConnection"; exec dotnet /tmp/lgs-target-import-20261019/operator/TargetAdmissions.dll --database coaching_lgs2026_rehearsal_20261019 --rehearsal --apply --package /tmp/lgs-target-import-20261019/packages/01-catalog.json'
