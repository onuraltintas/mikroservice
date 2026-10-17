import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const name='coaching-followup-20261017', target=resolve('artifacts/coaching-followup-live-20261017/live');
const files=['deployed.txt','live-result.json','drill-result.json','drill-backup.sha256','live-backups.sha256','built-coaching-service-image.txt','built-notification-service-image.txt','built-staff-portal-image.txt','accounts.live.before.txt','accounts.live.after.txt','coaching.live.before.txt','coaching.live.after.txt','queue-ready.json','notification-live-result.json','notification-errors.json','notification-live-verified.txt','service-status.txt','diagnostics.txt','diagnostics-verified.txt','notification-drill-verified.txt','notification-drill-backup.sha256','notification-drill.before.txt','notification-drill.after.txt','live-verification/before.json','live-verification/after.json','live-verification/verified.txt','live-verification/accounts.before.txt','live-verification/accounts.after.txt','live-verification/protected.before.txt','live-verification/protected.after.txt','live-verification/notifications.before.txt','live-verification/notifications.after.txt'];
if(process.argv.includes('--final')) files.push('monitor.log','monitor-verified.txt');
mkdirSync(target,{recursive:true});
for(const file of process.argv.includes('--monitor-only') ? ['monitor.log','monitor-verified.txt'] : files){
 const output=resolve(target,file); mkdirSync(resolve(output,'..'),{recursive:true});
 execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',`root@185.170.198.83:/var/lib/eduivme/releases/${name}/${file}`,output],{stdio:['ignore','pipe','pipe']});
}
console.log('Collected allowlisted release evidence only; private backups and configuration remain on server.');
