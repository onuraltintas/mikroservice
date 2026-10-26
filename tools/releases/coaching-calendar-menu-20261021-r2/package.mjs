import {readFileSync,writeFileSync,copyFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const tag='coaching-calendar-menu-20261021-r2',root=resolve('artifacts/coaching-calendar-menu-live-20261021'),pack=resolve(root,'package-r2');
if(existsSync(pack))throw Error('Package already sealed.');
const files=[],hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
function write(path,text){const p=resolve(pack,path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,text.replaceAll('\r\n','\n'));files.push(path);}
function copy(path,name){const p=resolve(pack,name);mkdirSync(dirname(p),{recursive:true});copyFileSync(path,p);files.push(name);}
const frontend=["src/app/core/services/coaching-study-planning.service.ts", "src/app/features/coaching-portal/layout/coaching-student-surfaces.scss", "src/app/features/coaching-portal/pages/student-study-plans.component.ts", "src/app/features/coaching-portal/pages/student-study-plans.component.html", "src/app/features/coaching-portal/pages/student-study-plans.component.scss", "src/app/features/coaching-portal/pages/study-plan-calendar.component.ts", "src/app/features/coaching-portal/pages/study-plan-calendar.component.html", "src/app/features/coaching-portal/pages/study-plan-calendar.component.scss", "src/app/features/coaching-portal/pages/study-plan-colors.ts"];
for(const path of frontend)write('overlay/'+path,readFileSync('clients/admin-panel/'+path,'utf8'));
for(const name of ['deploy.sh','history.sh'])write(name,readFileSync('tools/releases/'+tag+'/'+name,'utf8'));
const base='artifacts/coaching-screen-audit-20261020-r2/package/';
for(const name of ['runtime-admin-panel.cjs','runtime-staff-portal.cjs','build-portals.cjs'])write(name,readFileSync(base+name,'utf8'));
write('public-check.sh',readFileSync(base+'public-check.sh','utf8').replaceAll('coaching-screen-audit-20261020-r2',tag));
write('.dockerignore','source\n*.before.backup\n*.json\n*.txt\n*.log\n*.sha256\n');
const modules=['Coaching.API','Coaching.Application','Coaching.Infrastructure','Coaching.Domain'];
for(const module of modules)for(const ext of ['dll','pdb'])copy(resolve(root,'publish/coaching',module+'.'+ext),'payload/coaching/'+module+'.'+ext);
write('baseline-coaching.sha256',[...modules,'EduPlatform.Shared.Contracts'].flatMap(m=>['dll','pdb'].map(e=>hash(resolve(root,'baseline/Coaching',m+'.'+e))+'  /app/'+m+'.'+e)).join('\n')+'\n');
copy(resolve(root,'verification/Verification.dll'),'Verification.dll');
write('coaching-service.Dockerfile','ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY payload/coaching/ /app/\nLABEL org.opencontainers.image.revision="${SOURCE_REVISION}"\n');
writeFileSync(resolve(pack,'payload.sha256'),files.map(x=>hash(resolve(pack,x))+'  '+x).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release-r2.tar.gz'),'-C',pack,'.']);
const sha=hash(resolve(root,'release-r2.tar.gz'));writeFileSync(resolve(root,'archive-r2.sha256'),sha+'\n');
writeFileSync('tools/releases/'+tag+'/start.sh',`set -euo pipefail\numask 077\nrelease=/var/lib/eduivme/releases/${tag}\necho '${sha}  /var/lib/eduivme/releases/${tag}.tar.gz' | sha256sum -c -\ntest ! -e "$release"\nmkdir -m 700 "$release"\ntar -xzf "$release.tar.gz" -C "$release"\ncd "$release"\nbash -n deploy.sh\nbash deploy.sh prepare\nnohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &\necho 'Isolated calendar-menu build started.'\n`);
console.log(JSON.stringify({tag,sha256:sha,frontendFiles:frontend.length}));
