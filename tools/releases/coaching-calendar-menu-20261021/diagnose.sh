set -euo pipefail
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021
tail -n 4 package-verified.log
while read -r hash file; do actual=$(docker exec eduivme-production-coaching-service-1 sha256sum "$file" | cut -d' ' -f1); if [[ "$actual" != "$hash" ]]; then printf 'Baseline mismatch %s expected=%s actual=%s\n' "$file" "$hash" "$actual"; fi; done < baseline-coaching.sha256
ls -1 *.txt 2>/dev/null || true
