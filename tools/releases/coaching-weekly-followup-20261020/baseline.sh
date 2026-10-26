set -euo pipefail
umask 077
directory=/var/lib/eduivme/releases/coaching-weekly-followup-20261020-baseline
test ! -e "$directory"; mkdir -m 700 "$directory"
for pair in 'coaching-service Coaching' 'notification-service Notification'; do
 read -r service prefix <<< "$pair"
 mkdir "$directory/$prefix"
 for module in "$prefix.API" "$prefix.Application" "$prefix.Infrastructure" "$prefix.Domain" EduPlatform.Shared.Contracts; do
  for extension in dll pdb; do
   docker cp "eduivme-production-$service-1:/app/$module.$extension" "$directory/$prefix/$module.$extension"
   docker exec "eduivme-production-$service-1" sha256sum "/app/$module.$extension" >> "$directory/$prefix/modules.sha256"
  done
 done
done
tar -czf "$directory/baseline.tar.gz" -C "$directory" Coaching Notification
echo 'Live module DLL/PDB baselines collected.'
