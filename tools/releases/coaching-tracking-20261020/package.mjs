import {mkdirSync,copyFileSync,readFileSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root=resolve('artifacts/coaching-tracking-live-20261020'),pack=resolve(root,'package');
if(existsSync(pack))throw Error('Sealed release already exists.');
const prefix='projects/staff-portal/src/app/features/coaching/';
const files=['coaching-teacher-tracking.component.ts','coaching-teacher-tracking.component.html','coaching-teacher-tracking.component.scss','coaching-teacher-tracking.service.ts','coaching-teacher-workspace.component.ts','coaching-teacher-workspace.component.html','coaching-teacher-assignment-detail.component.ts','coaching-teacher-assignment-detail.component.html'].map(n=>prefix+n);
files.push('src/app/features/coaching-portal/pages/student-study-plans.component.ts');
const manifest=[];const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
for(const name of files){const dest=resolve(pack,'overlay',name);mkdirSync(dirname(dest),{recursive:true});copyFileSync(resolve('clients/admin-panel',name),dest);manifest.push('overlay/'+name);}
for(const extension of ['dll','pdb']){const name=`payload/coaching/Coaching.API.${extension}`,dest=resolve(pack,name);mkdirSync(dirname(dest),{recursive:true});copyFileSync(resolve(root,'publish',`Coaching.API.${extension}`),dest);manifest.push(name);}
for(const name of ['deploy.sh']){copyFileSync(resolve('tools/releases/coaching-tracking-20261020',name),resolve(pack,name));manifest.push(name);}
writeFileSync(resolve(pack,'preservation.sh'),readFileSync('tools/releases/coaching-development-20261020/preservation.sh','utf8').replaceAll('coaching-development-20261020','coaching-tracking-20261020'));manifest.push('preservation.sh');
copyFileSync(resolve('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs'),resolve(pack,'build-portals.cjs'));manifest.push('build-portals.cjs');
writeFileSync(resolve(pack,'api-baseline.sha256'),hash('artifacts/coaching-practice-yks-regression-20261020/api-package/payload/Coaching.API.dll')+'\n');manifest.push('api-baseline.sha256');
writeFileSync(resolve(pack,'payload.sha256'),manifest.map(n=>hash(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);
console.log(JSON.stringify({path:resolve(root,'release.tar.gz'),sha256:hash(resolve(root,'release.tar.gz')),frontendFiles:files.length}));
