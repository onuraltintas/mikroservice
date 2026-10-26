import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const target=resolve('artifacts/coaching-calendar-menu-live-20261021/server-r4');mkdirSync(target,{recursive:true});
const files=['package-verified.log','backups.sha256','prepared.txt','built.txt','deployed.txt','promotion-start.txt','source-overlay.diff','portals-build.log'];
execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',...files.map(name=>'root@185.170.198.83:/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4/'+name),target],{stdio:['ignore','pipe','pipe']});
console.log('Collected frontend release verification; database backups remain on the server.');
