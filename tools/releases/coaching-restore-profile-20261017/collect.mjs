import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const name='coaching-restore-profile-20261017',target=resolve('artifacts/coaching-restore-profile-live-20261017/live');mkdirSync(target,{recursive:true});
const files=['deployed.txt','live-result.json','drill-result.json','drill-backup.sha256','live-backups.sha256','built-coaching-service-image.txt','built-admin-panel-image.txt','built-staff-portal-image.txt','accounts.live.before.txt','accounts.live.after.txt','coaching.live.before.txt','coaching.live.after.txt','diagnostics.txt','diagnostics-verified.txt','live-verification/before.json','live-verification/after.json','live-verification/backups.sha256','live-verification/verified.txt','live-verification/accounts.before.txt','live-verification/accounts.after.txt','live-verification/protected.before.txt','live-verification/protected.after.txt'];
if(process.argv.includes('--final'))files.push('monitor.log','monitor-verified.txt');
for(const file of files){const output=resolve(target,file);mkdirSync(resolve(output,'..'),{recursive:true});execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',`root@185.170.198.83:/var/lib/eduivme/releases/${name}/${file}`,output],{stdio:['ignore','pipe','pipe']});}
console.log('Collected only hashes, preservation results and health evidence; private backups/configuration stay on server.');
