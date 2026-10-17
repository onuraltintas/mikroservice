set -euo pipefail
docker exec eduivme-production-staff-portal-1 nginx -T 2>&1 | sed -n '/server {/,/^[}]$/p'
for origin in https://eduivme.com https://onuraltintas.net; do
 printf '%s\n' "$origin"
 curl -fsSL --max-time 10 "$origin/staff/" | sed 's/></>\n</g' | awk '/<base|<script|stylesheet/{print}' | head -12
done
