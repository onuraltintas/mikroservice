import {mkdirSync,copyFileSync,readFileSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root=resolve('artifacts/coaching-development-live-20261020-r1'),pack=resolve(root,'package');
if(existsSync(pack)) throw Error('Do not overwrite a sealed release.');
const files=[
 'src/app/core/models/practice-exam.ts',
 'src/app/features/coaching-portal/pages/practice-progress.component.ts',
 'src/app/features/coaching-portal/pages/practice-progress.component.html',
 'src/app/features/coaching-portal/pages/practice-adaptation.component.ts',
 'src/app/features/coaching-portal/pages/coaching-portal-progress.component.html',
 'src/app/features/coaching-portal/pages/coaching-notifications.component.ts',
 'src/app/features/coaching-portal/pages/coaching-notifications.component.html',
 'src/app/features/coaching-portal/pages/coaching-portal-home.component.html',
 'shared/coaching-notification-target.ts',
 'projects/staff-portal/src/app/app.component.ts',
 'projects/staff-portal/src/app/app.component.html',
 'projects/staff-portal/src/app/notifications/staff-notifications.service.ts',
 'projects/staff-portal/src/app/notifications/staff-notification-center.component.ts',
 'projects/staff-portal/src/app/notifications/staff-notification-center.component.html',
 'projects/staff-portal/src/app/features/coaching/coaching-staff-study.component.ts',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-workspace.component.ts',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-workspace.component.html',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-assignments.component.ts',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-assignments.component.html',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-assignment-detail.component.ts'
];
const manifest=[];
for(const name of files){const dest=resolve(pack,'overlay',name);mkdirSync(dirname(dest),{recursive:true});copyFileSync(resolve('clients/admin-panel',name),dest);manifest.push('overlay/'+name);}
for(const name of ['deploy.sh','preservation.sh']){copyFileSync(resolve('tools/releases/coaching-development-20261020',name),resolve(pack,name));manifest.push(name);}
copyFileSync(resolve('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs'),resolve(pack,'build-portals.cjs'));manifest.push('build-portals.cjs');
const hash=path=>createHash('sha256').update(readFileSync(path)).digest('hex');
writeFileSync(resolve(pack,'payload.sha256'),manifest.map(name=>hash(resolve(pack,name))+'  '+name).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);
console.log(JSON.stringify({path:resolve(root,'release.tar.gz'),sha256:hash(resolve(root,'release.tar.gz')),files:files.length}));
