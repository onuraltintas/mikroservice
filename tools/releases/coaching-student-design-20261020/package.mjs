import {readFileSync,writeFileSync,copyFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const tag='coaching-student-design-20261020',root=resolve('artifacts/'+tag),pack=resolve(root,'package');
if(existsSync(pack))throw Error('Package already sealed');
mkdirSync(pack,{recursive:true});
const manifest=[];
function write(name,text){const p=resolve(pack,name);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,text.replaceAll('\r\n','\n'));manifest.push(name);}
function copy(file,name){write(name,readFileSync(file,'utf8'));}
const prefix='clients/admin-panel/';
const overlay=[
 'src/app/features/coaching-portal/coaching-feedback.ts','src/app/features/coaching-portal/coaching-icon.component.ts',
 ...['coaching-student-tokens.scss','coaching-student-surfaces.scss','coaching-portal-layout.component.ts','coaching-portal-layout.component.html','coaching-portal-layout.component.scss'].map(n=>'src/app/features/coaching-portal/layout/'+n),
 'src/app/core/services/toaster.service.ts','src/app/shared/components/confirmation-dialog/confirmation-dialog.component.ts',
 'src/app/features/coaching-portal/pages/coaching-finance.component.ts','src/app/features/coaching-portal/pages/coaching-portal-progress.component.ts',
 'projects/staff-portal/src/app/app.component.ts',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-study-overview.component.ts',
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-study-overview.component.html'
];
for(const f of overlay)copy(prefix+f,'overlay/'+f);
let transform=readFileSync('artifacts/student-design-transform.cjs','utf8').replace("require('../clients/admin-panel/node_modules/typescript')","require('./source/admin-panel/node_modules/typescript')");
write('transform.cjs',transform);
let feedback=readFileSync('artifacts/student-design-feedback.cjs','utf8').split('// Shared global student styles')[0].replace("require('../clients/admin-panel/node_modules/typescript')","require('./source/admin-panel/node_modules/typescript')");
write('feedback.cjs',feedback);
copy('tools/releases/'+tag+'/finalize-source.cjs','finalize-source.cjs');
copy('tools/releases/'+tag+'/deploy.sh','deploy.sh');
copy('tools/releases/coaching-practice-adaptation-20261020/build-portals.cjs','build-portals.cjs');
write('history.sh',readFileSync('tools/releases/coaching-tracking-20261020/history.sh','utf8').replaceAll('coaching-tracking-20261020',tag));
write('public-check.sh',readFileSync('tools/releases/coaching-tracking-20261020/public-check.sh','utf8').replaceAll('coaching-tracking-20261020',tag));
const hash=f=>createHash('sha256').update(readFileSync(f)).digest('hex');
writeFileSync(resolve(pack,'package.sha256'),manifest.map(n=>hash(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);
writeFileSync(resolve(root,'archive.sha256'),hash(resolve(root,'release.tar.gz'))+'\n');
console.log(JSON.stringify({archive:resolve(root,'release.tar.gz'),sha256:hash(resolve(root,'release.tar.gz')),overlayFiles:overlay.length}));
