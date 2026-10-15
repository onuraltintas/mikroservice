set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/regression-answer-label-20261015
bash deploy.sh validate
printf 'observed_at='; date -u +%FT%TZ
for service in speed-reading-service speed-reading-frontend; do
  docker inspect "eduivme-production-$service-1" --format '{{.Name}} image={{.Image}} status={{.State.Status}} restarts={{.RestartCount}}'
done
curl -fsS --max-time 15 -o /dev/null -w 'public_status=%{http_code} elapsed=%{time_total}\n' https://masterhizliokuma.com/auth/login
docker inspect eduivme-production-identity-service-1 | jq -e '.[0].Config.Env | any(. == "AuthRecaptcha__Enabled=true") and any(. == "AuthRecaptcha__MinimumScore=0.5")' >/dev/null
docker logs --since "$(cat deployed.txt)" eduivme-production-speed-reading-service-1 2>&1 | awk '/HTTP .* responded /{n++;for(i=1;i<=NF;i++){if($i=="responded"){if($(i+1)>=500) errors++;if($(i+3) ~ /^[0-9.]+$/){sum+=$(i+3);if($(i+3)>max)max=$(i+3)}}}} END{printf "requests=%d server_5xx=%d mean_ms=%.2f max_ms=%.2f\n",n,errors,n?sum/n:0,max}'
