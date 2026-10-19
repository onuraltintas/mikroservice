import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync,symlinkSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-targets-20261019');
assert.ok(!existsSync(join(release,'source-provenance.json')));mkdirSync(release,{recursive:true});
const filter=p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p);
cpSync(resolve(root,'artifacts/coaching-yks-20261019-r3/backend-source'),join(release,'backend-source'),{recursive:true,filter});
const changes=[];
const hash=s=>createHash('sha256').update(s).digest('hex');
function put(tree,file,after){const dest=join(tree,file);mkdirSync(resolve(dest,'..'),{recursive:true});const before=existsSync(dest)?readFileSync(dest):null;writeFileSync(dest,after);changes.push({file,before:before&&hash(before),after:hash(after)});}
const backend=join(release,'backend-source');
for(const file of ['Coaching.Domain/Entities/TargetAdmissions.cs','Coaching.Infrastructure/Data/Configurations/TargetAdmissionsConfiguration.cs','Coaching.Infrastructure/StudyPlanning/TargetAdmissionSearchService.cs','Coaching.API/Controllers/TargetAdmissionsController.cs']){
 const path='services/coaching-service/'+file;put(backend,path,readFileSync(resolve(root,path)));
}
const program='services/coaching-service/Coaching.API/Program.cs';
const live=readFileSync(join(backend,program),'utf8'),current=readFileSync(resolve(root,program),'utf8');
const start=current.indexOf('    options.AddPolicy("target-catalog-search"'),end=current.indexOf('    options.AddPolicy("study-planning-write"',start);
assert.ok(start>0&&end>start);
if(live.includes('"target-catalog-search"')){
 const oldStart=live.indexOf('    options.AddPolicy("target-catalog-search"'),oldEnd=live.indexOf('    options.AddPolicy("study-planning-write"',oldStart);
 assert.equal(live.slice(oldStart,oldEnd).replaceAll('\r\n','\n'),current.slice(start,end).replaceAll('\r\n','\n'));
}else put(backend,program,live.replace('    options.AddPolicy("study-planning-write"',current.slice(start,end)+'    options.AddPolicy("study-planning-write"'));
for(const surface of ['admin','staff']){
 const tree=join(release,surface+'-tree/clients');mkdirSync(tree,{recursive:true});
 for(const folder of ['admin-panel','shared'])cpSync(resolve(root,'artifacts/coaching-wizard-coverage-20261019',surface+'-tree/clients',folder),join(tree,folder),{recursive:true,filter});
 for(const file of ['target-admission-search.component.ts','student-goal-target.component.ts','student-goal-target.component.html']){
  const path='src/app/features/coaching-portal/pages/'+file;put(join(tree,'admin-panel'),path,readFileSync(resolve(root,'clients/admin-panel',path)));
 }
 symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(tree,'admin-panel/node_modules'),'junction');
}
writeFileSync(join(release,'source-provenance.json'),JSON.stringify({backendBase:'coaching-yks-20261019-r3',frontendBase:'coaching-wizard-coverage-20261019',database:'TargetAdmissions2026 already applied; no migrations included or executed',changes},null,2));
console.log('Isolated target catalog API and both portals prepared. No unrelated migrations or attachment code included.');
