import {execFileSync} from 'node:child_process';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
const target=resolve('artifacts/coaching-teacher-publish-live-20261017/live');mkdirSync(target,{recursive:true});
const files=['before.json','after.json','protected.before.txt','protected.after.txt','accounts.before.txt','accounts.after.txt','backups.sha256','baseline-verified.txt','verified.txt'];
// Download only comparison hashes/results; backups and credentials stay private on the server.
execFileSync('scp',['-F','NUL','-i','C:/Users/onura/.ssh/eduivme_vps_deploy','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',...files.map(file=>`root@185.170.198.83:/var/lib/eduivme/releases/coaching-teacher-publish-20261017/${file}`),target],{stdio:['ignore','pipe','pipe']});
console.log('Collected publication/completion and preservation evidence.');
