import { mkdirSync, copyFileSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const tag='coaching-weekly-followup-20261020', root=resolve('artifacts/coaching-weekly-followup-live-20261020'), pack=resolve(root,'package');
if(existsSync(pack))throw Error('Sealed release already exists.');
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex'), manifest=[];
function copy(from,name){const dest=resolve(pack,name);mkdirSync(dirname(dest),{recursive:true});copyFileSync(from,dest);manifest.push(name);}
const pages='src/app/features/coaching-portal/pages/', staff='projects/staff-portal/src/app/';
const files=[
 'shared/coaching-notification-target.ts','src/app/core/services/coaching-weekly-review.service.ts',
 ...['weekly-review.component.ts','weekly-review.component.html','weekly-review.component.scss','practice-progress.component.ts','practice-progress.component.html',
 'coaching-portal-progress.component.ts','coaching-portal-progress.component.html','coaching-notifications.component.html',
 'coaching-portal-home.component.ts','coaching-portal-home.component.html','student-weekly-home.component.ts'].map(n=>pages+n),
 ...['app.component.ts','app.component.html','notifications/staff-notification-center.component.ts','notifications/staff-notification-center.component.html',
 'features/coaching/coaching-teacher-workspace.component.ts','features/coaching/coaching-teacher-workspace.component.html',
 'features/coaching/coaching-teacher-tracking.component.ts','features/coaching/coaching-teacher-tracking.component.html',
 'features/coaching/coaching-teacher-tracking.service.ts'].map(n=>staff+n)
];
for(const file of files)copy(resolve('clients/admin-panel',file),'overlay/'+file);
if(hash(resolve(root,'publish/coaching/EduPlatform.Shared.Contracts.dll'))!==hash(resolve(root,'publish/notification/EduPlatform.Shared.Contracts.dll')))throw Error('Shared contract builds differ.');
for(const [service,prefix,modules] of [['coaching','Coaching',['API','Application','Infrastructure','Domain']],['notification','Notification',['API','Application']]]) {
 for(const module of [...modules.map(n=>prefix+'.'+n),'EduPlatform.Shared.Contracts'])for(const ext of ['dll','pdb'])copy(resolve(root,'publish',service,module+'.'+ext),'payload/'+service+'/'+module+'.'+ext);
 const dockerfile=service+'-service.Dockerfile';
 const content='ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY payload/'+service+'/ /app/\nLABEL org.opencontainers.image.revision="${SOURCE_REVISION}" org.opencontainers.image.release="'+tag+'"\n';
 writeFileSync(resolve(pack,dockerfile),content);manifest.push(dockerfile);
}
copy(resolve(root,'verification/Verification.dll'),'Verification.dll');
for(const prefix of ['Coaching','Notification'])copy(resolve(root,'baseline',prefix,'modules.sha256'),'baseline-'+prefix+'.sha256');
copy('tools/releases/'+tag+'/deploy.sh','deploy.sh');
copy('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs','build-portals.cjs');
for(const name of ['history.sh','public-check.sh']){writeFileSync(resolve(pack,name),readFileSync('tools/releases/coaching-tracking-20261020/'+name,'utf8').replaceAll('coaching-tracking-20261020',tag));manifest.push(name);}
for(const name of manifest)if(/\.(sh|cjs|ts|html|scss)$/.test(name))writeFileSync(resolve(pack,name),readFileSync(resolve(pack,name),'utf8').replaceAll('\r\n','\n'));
writeFileSync(resolve(pack,'payload.sha256'),manifest.map(n=>hash(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);
writeFileSync(resolve(root,'archive.sha256'),hash(resolve(root,'release.tar.gz'))+'\n');
console.log(JSON.stringify({archive:resolve(root,'release.tar.gz'),sha256:hash(resolve(root,'release.tar.gz')),frontendFiles:files.length}));
