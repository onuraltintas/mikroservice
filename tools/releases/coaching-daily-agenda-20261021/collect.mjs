import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const target=resolve('artifacts/coaching-daily-agenda-live-20261021/server');mkdirSync(target,{recursive:true});
const files=['package-verified.log','backups.sha256','prepared.txt','built.txt','drill-verified.txt','drill-probe.log','deployed.txt','promotion-start.txt','source-overlay.diff','portals-build.log','api-errors.txt','coaching_db.missing-old-rows.txt','identity_db.missing-old-rows.txt','notification_db.missing-old-rows.txt'];
execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',...files.map(name=>'root@185.170.198.83:/var/lib/eduivme/releases/coaching-daily-agenda-20261021/'+name),target],{stdio:['ignore','pipe','pipe']});
console.log('Collected release proof; backups and row hashes remain on the server.');
