import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const target=resolve('artifacts/coaching-yearly-calendar-live-20261017/live');mkdirSync(target,{recursive:true});
// Explicitly allow evidence files only; database backups, compose and environment stay private on the server.
for(const file of ['deployed.txt','live-result.json','browser-result.json','browser-db-verified.txt','live-backups.sha256','built-admin-panel-image.txt','previous-admin-panel-image.txt','accounts.live.before.txt','accounts.after-browser.txt','coaching.live.before.txt','coaching.live.after.txt','protected.before.txt','protected.after-browser.txt','monitor.log','monitor-verified.txt']){
  execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',`root@185.170.198.83:/var/lib/eduivme/releases/coaching-yearly-calendar-20261017/${file}`,resolve(target,file)],{stdio:['ignore','pipe','pipe']});
}
console.log('Collected release and preservation evidence; private configuration and backups remain on the server.');
