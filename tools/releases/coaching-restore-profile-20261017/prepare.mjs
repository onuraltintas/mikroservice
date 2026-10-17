import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync,symlinkSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),name='coaching-restore-profile-live-20261017',release=resolve(root,'artifacts',name),revision='7609d374';
const git=(...a)=>execFileSync('git',a,{cwd:root,maxBuffer:32*1024*1024});
assert.ok(!existsSync(resolve(release,'source')));mkdirSync(release,{recursive:true});
const shared=['clients/admin-panel/src/app/core/services/coaching-study-planning.service.ts',...['student-study-plans.component.ts','student-study-plans.component.html'].map(n=>'clients/admin-panel/src/app/features/coaching-portal/pages/'+n)];
const changed=(base,...paths)=>git('diff','--name-only',base,revision,'--',...paths).toString().trim().split('\n').filter(x=>x&&!x.endsWith('.spec.ts'));
const provenance={revision:git('rev-parse',revision).toString().trim(),services:['coaching-service','admin-panel','staff-portal'],migration:null,sources:[]};
for(const [kind,previous,base,files] of [
 ['source','coaching-calendar-move-live-20261017/source','2acdd750',changed('2acdd750','services/coaching-service')],
 ['admin-source','coaching-calendar-move-live-20261017-r2/admin-source','7f1f61de',shared],
 ['staff-source','coaching-calendar-move-live-20261017-r2/staff-source','7f1f61de',[...shared,...changed('7f1f61de','clients/admin-panel/projects/staff-portal/src/app/features/coaching')]]
]){
 cpSync(resolve(root,'artifacts',previous),resolve(release,kind),{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
 for(const file of files)execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/'+kind,'-'],{cwd:root,input:git('diff','--binary',base,revision,'--',file)});
 provenance.sources.push({kind,previous,base,files});
 if(kind!=='source'){
  const app=resolve(release,kind,'clients/admin-panel'),sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
  assert.equal(sha(resolve(app,'package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
  symlinkSync(resolve(root,'clients/admin-panel/node_modules'),resolve(app,'node_modules'),'junction');
 }
}
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify(provenance,null,2));
console.log(JSON.stringify({release,files:provenance.sources.map(x=>[x.kind,x.files.length])}));
