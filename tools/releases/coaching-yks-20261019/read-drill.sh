set -euo pipefail
cd /var/lib/eduivme/releases/coaching-yks-20261019-r3
for file in drill-migrate.log drill-error.log drill-result.json;do
 if [[ -f "$file" ]];then printf '%s\n' "$file";tail -n 16 "$file"|sed -E 's/(Password|password|accessToken|refreshToken)[=:][^; "}]+/\1=[REDACTED]/g';fi
done
