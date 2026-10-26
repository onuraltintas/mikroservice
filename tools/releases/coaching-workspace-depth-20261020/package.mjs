import {readFileSync, writeFileSync, mkdirSync, existsSync} from 'node:fs';
import {resolve, dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';

const sourceTag='coaching-workspace-depth-20261020',tag=process.argv[2]||sourceTag,base='coaching-teacher-design-20261020-r6';
const out=resolve('artifacts/'+tag),pack=resolve(out,'package'),files=[];
if(existsSync(pack))throw Error('Package already sealed');
mkdirSync(pack,{recursive:true});
function write(name,text){const file=resolve(pack,name);mkdirSync(dirname(file),{recursive:true});writeFileSync(file,text.replaceAll('\r\n','\n'));files.push(name);}
const old=readFileSync('artifacts/'+base+'/package/deploy.sh','utf8');
let deploy=old.replaceAll(base,tag).replace('base=/var/lib/eduivme/releases/coaching-student-design-20261020-r7','base=/var/lib/eduivme/releases/'+base);
deploy=deploy.replace('services=(staff-portal)','services=(admin-panel staff-portal)').replace('case "$name" in eduivme-production-staff-portal-1)','case "$name" in eduivme-production-admin-panel-1|eduivme-production-staff-portal-1)');
deploy=deploy.replace("'.services[\"staff-portal\"].image=(\"eduivme/staff-portal:\"+$tag)'","'.services[\"admin-panel\"].image=(\"eduivme/admin-panel:\"+$tag)|.services[\"staff-portal\"].image=(\"eduivme/staff-portal:\"+$tag)'");
deploy=deploy.replace('node transform.cjs ./source/admin-panel > transform.log 2>&1','node add-style.cjs > transform.log 2>&1');
deploy=deploy.replace('coaching-teacher-design-builder-20261020','coaching-workspace-depth-builder-20261020');
deploy=deploy.replace('mkdir -p payload/admin payload/staff;','mkdir -p payload/admin payload/staff; cp -a source/admin-panel/dist/admin-panel/. payload/admin/;');
deploy=deploy.replace('docker image inspect "$(<"previous-staff-portal.txt")" > runtime-image.json','docker image inspect "$(<"previous-$s.txt")" > runtime-image.json');
deploy=deploy.replace('node runtime-image.cjs > runtime-image.log','node "runtime-$s.cjs" > "runtime-$s.log"');
write('deploy.sh',deploy);
write('add-style.cjs',`const fs=require('node:fs');
for(const [file,anchor,line] of [
 ['src/styles.scss',"@use './app/features/coaching-portal/layout/coaching-student-surfaces';","@use './app/features/coaching-portal/layout/coaching-workspace-depth';"],
 ['projects/staff-portal/src/styles.scss',"@use './app/coaching-teacher-theme';","@use '../../../src/app/features/coaching-portal/layout/coaching-workspace-depth';"]
]){const path='source/admin-panel/'+file,source=fs.readFileSync(path,'utf8');if(source.includes('coaching-workspace-depth')||!source.includes(anchor))throw Error('Unexpected style source');fs.writeFileSync(path,source.replace(anchor,anchor+'\\n'+line));}
`);
write('overlay/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss',readFileSync('clients/admin-panel/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss','utf8'));
write('runtime-staff-portal.cjs',readFileSync('tools/releases/coaching-teacher-design-20261020/runtime-image.cjs','utf8'));
write('runtime-admin-panel.cjs',readFileSync('tools/releases/coaching-student-design-20261020/runtime-image.cjs','utf8'));
write('build-portals.cjs',readFileSync('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs','utf8'));
for(const name of ['history.sh','public-check.sh'])write(name,readFileSync('artifacts/'+base+'/package/'+name,'utf8').replaceAll(base,tag));
write('.dockerignore','**\n!admin-panel.Dockerfile\n!staff-portal.Dockerfile\n!payload\n!payload/**\n');
const sha=file=>createHash('sha256').update(readFileSync(file)).digest('hex');
writeFileSync(resolve(pack,'package.sha256'),files.map(n=>sha(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(out,'release.tar.gz'),'-C',pack,'.']);
const digest=sha(resolve(out,'release.tar.gz'));
writeFileSync(resolve(out,'archive.sha256'),digest+'\n');
writeFileSync('tools/releases/'+sourceTag+'/start'+tag.slice(sourceTag.length)+'.sh',`set -euo pipefail\numask 077\nrelease=/var/lib/eduivme/releases/${tag}\necho '${digest}  /var/lib/eduivme/releases/${tag}.tar.gz' | sha256sum -c -\ntest ! -e "$release"\nmkdir -m 700 "$release"\ntar -xzf "$release.tar.gz" -C "$release"\ncd "$release"\nbash -n deploy.sh\nbash deploy.sh prepare\nnohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &\necho 'Isolated frontend style build started.'\n`);
console.log(JSON.stringify({tag,sha256:digest}));
