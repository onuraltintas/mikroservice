set -euo pipefail
docker ps --format '{{.Names}} {{.Ports}}' | awk '/nginx|proxy|staff/{print}'
for folder in /etc/nginx /etc/caddy; do if test -d "$folder"; then ls "$folder"; fi; done
ps -C nginx,caddy -o comm= || true
nginx -T 2>/dev/null | grep -nE 'staff|server_name|location|proxy_pass|root |include |listen' | head -140 || true
for edge in production-edge speed-reading-edge; do docker inspect "eduivme-production-$edge-1" --format '{{json .Mounts}}' | jq -r '.[]|select(.Destination|test("caddy|Caddy"))|.Destination'; done
docker exec eduivme-production-production-edge-1 cat /etc/caddy/Caddyfile | sed -n '1,105p'
docker exec eduivme-production-speed-reading-edge-1 cat /etc/caddy/Caddyfile | grep -nE 'staff|onuraltintas|eduivme\.com' || true
