set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/exercise-reports-20261016
bash deploy.sh validate
printf 'observed_at='; date -u +%FT%TZ
for destination in https://masterhizliokuma.com/auth/login https://onuraltintas.net/staff/; do
  curl -fsS --max-time 15 -o /dev/null -w 'public_status=%{http_code} elapsed=%{time_total}\n' "$destination"
done
docker logs --since "$(cat deployed.txt)" eduivme-production-speed-reading-service-1 2>&1 | awk '/HTTP .* responded /{ n++; for(i=1;i<=NF;i++){if($i=="responded"){code=$(i+1); if(code>=500) errors++; if($(i+3) ~ /^[0-9.]+$/){sum+=$(i+3); if($(i+3)>max)max=$(i+3)}}}} END {printf "requests=%d server_5xx=%d mean_ms=%.2f max_ms=%.2f\n",n,errors,n?sum/n:0,max}'
