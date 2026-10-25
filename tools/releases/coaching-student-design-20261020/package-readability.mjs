import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const old='coaching-student-design-20261020',tag=old+'-r7',root=resolve('artifacts/'+tag),pack=resolve(root,'package');
if(existsSync(pack))throw Error('Already sealed');mkdirSync(pack,{recursive:true});const files=[];
function write(name,text){const p=resolve(pack,name);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,text.replaceAll('\r\n','\n'));files.push(name);}
write('overlay/src/app/features/coaching-portal/layout/coaching-student-surfaces.scss',readFileSync('clients/admin-panel/src/app/features/coaching-portal/layout/coaching-student-surfaces.scss','utf8'));
let deploy=readFileSync('tools/releases/'+old+'/deploy.sh','utf8').replaceAll(old,tag).replace('/coaching-weekly-followup-20261020','/'+old+'-r5').replace('services=(admin-panel staff-portal)','services=(admin-panel)');
deploy=deploy.split('\n').filter(l=>!/(transform\.cjs|feedback\.cjs|finalize-source\.cjs|mkdir source\/clients)/.test(l)).join('\n');
deploy=deploy.replace('|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)','');write('deploy.sh',deploy); write('.dockerignore','**\n!admin-panel.Dockerfile\n!payload\n!payload/admin\n!payload/admin/**\n');
write('build-portals.cjs',readFileSync('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs','utf8').replaceAll("['admin-panel','staff-portal']","['admin-panel']"));
for(const n of ['history.sh','public-check.sh'])write(n,readFileSync('artifacts/'+old+'/package/'+n,'utf8').replaceAll(old,tag));
const hash=f=>createHash('sha256').update(readFileSync(f)).digest('hex');writeFileSync(resolve(pack,'package.sha256'),files.map(n=>hash(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);const digest=hash(resolve(root,'release.tar.gz'));
writeFileSync(resolve(root,'archive.sha256'),digest+'\n');
writeFileSync('tools/releases/'+old+'/start-readability.sh',`set -euo pipefail\numask 077\nrelease=/var/lib/eduivme/releases/${tag}\necho '${digest}  /var/lib/eduivme/releases/${tag}.tar.gz' | sha256sum -c -\ntest ! -e "$release"; mkdir -m 700 "$release"\ntar -xzf "$release.tar.gz" -C "$release"\ncd "$release"; bash -n deploy.sh; bash deploy.sh prepare\nnohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &\necho 'Calendar contrast fix isolated build started.'\n`);
console.log(JSON.stringify({archive:resolve(root,'release.tar.gz'),sha256:digest}));

