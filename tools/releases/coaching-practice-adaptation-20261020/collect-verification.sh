set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-practice-adaptation-20261020
test -s live-preservation-verified.txt
for service in coaching-service admin-panel staff-portal; do
  count=$(grep -Eic '(Unhandled|fail:|error:|Exception)' "live-$service.log" || true)
  printf '%s error-pattern-lines: %s\n' "$service" "$count"
done
mkdir -p public-verification
cp deployed.txt hotfix-deployed.txt live-preservation-verified.txt live-e2e-counts.txt live-original-coaching.before.txt live-original-coaching.after.txt live-identity-accounts.before.txt live-identity-accounts.after.txt public-verification/
cp portals-build.log hotfix-portals-build.log coaching_db-migrate.log public-verification/
tar -czf public-verification.tar.gz public-verification
echo 'Public-safe verification artifacts prepared; secrets and backups excluded.'
