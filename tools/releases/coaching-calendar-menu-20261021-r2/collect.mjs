import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const target=resolve('artifacts/coaching-calendar-menu-live-20261021/server');mkdirSync(target,{recursive:true});
const files=['package-verified.log','backups.sha256','drill-probe.log','prepared.txt','built.txt','drill-verified.txt','deployed.txt','promotion-start.txt','api-errors.txt','coaching_db.protected.missing.txt','identity_db.protected.missing.txt','notification_db.protected.missing.txt'];
for(const name of files)execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','root@185.170.198.83:/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r2/'+name,resolve(target,name)],{stdio:['ignore','pipe','pipe']});
console.log('Collected '+files.length+' nonsecret verification files; database backups remain on the server.');
