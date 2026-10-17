import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const first='coaching-calendar-move-20261017',second=first+'-r2';
const sets=[
 [first,['deployed.txt','live-result.json','drill-result.json','drill-backup.sha256','live-backups.sha256','built-coaching-service-image.txt','built-admin-panel-image.txt','built-staff-portal-image.txt','accounts.live.before.txt','accounts.live.after.txt','coaching.live.before.txt','coaching.live.after.txt','original-protected.before.txt','original-protected.after.txt','original-rows-verified.txt','accounts.after-browser.txt','browser-db-result.json','browser-db-verified.txt']],
 [second,['deployed.txt','live-result.json','drill-result.json','live-backups.sha256','built-admin-panel-image.txt','built-staff-portal-image.txt','accounts.live.before.txt','accounts.live.after.txt','coaching.live.before.txt','coaching.live.after.txt','public-assets.txt','public-assets-verified.txt']]
];
if(process.argv.includes('--final'))sets[1][1].push('monitor.log','monitor-verified.txt','diagnostics.txt','diagnostics-verified.txt');
for(const [release,files] of sets){
 const target=resolve('artifacts',release.replace('coaching-calendar-move-','coaching-calendar-move-live-'),'live');mkdirSync(target,{recursive:true});
 // Explicit evidence allowlist: private backups, connection settings and configuration never leave the server.
 for(const file of files)execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',`root@185.170.198.83:/var/lib/eduivme/releases/${release}/${file}`,resolve(target,file)],{stdio:['ignore','pipe','pipe']});
}
console.log('Collected release hashes and preservation evidence only.');
