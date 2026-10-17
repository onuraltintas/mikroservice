import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync,symlinkSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),name='coaching-calendar-move-live-20261017',release=resolve(root,'artifacts',name),revision='2acdd750';
const git=(...args)=>execFileSync('git',args,{cwd:root,maxBuffer:32*1024*1024});
assert.ok(!existsSync(resolve(release,'source')));mkdirSync(release,{recursive:true});
const changed=(base,...paths)=>git('diff','--name-only',base,revision,'--',...paths).toString().trim().split('\n').filter(x=>x&&!x.endsWith('.spec.ts'));
const shared=['clients/admin-panel/src/app/core/services/coaching-study-planning.service.ts',...['student-study-plans.component.ts','student-study-plans.component.html','student-study-plans.component.scss','study-plan-calendar.ts','study-plan-calendar.component.ts','study-plan-calendar.component.html','study-plan-calendar.component.scss','weekly-calendar.ts'].map(n=>'clients/admin-panel/src/app/features/coaching-portal/pages/'+n)];
const provenance={revision:git('rev-parse',revision).toString().trim(),services:['coaching-service','admin-panel','staff-portal'],migration:'20261017140000_ManualTimedStudyTasks',sources:[]};
for(const [kind,previous,base,files] of [
 ['source','coaching-weekly-outcomes-live-20261017-r2/source','5411fc01',changed('5411fc01','services/coaching-service')],
 ['admin-source','coaching-yearly-calendar-live-20261017/admin-source','1e7c488f',changed('1e7c488f','clients/admin-panel/src')],
 ['staff-source','coaching-outcomes-live-20261016-r2/staff-source','374f79e2',[...shared,...changed('374f79e2','clients/admin-panel/projects/staff-portal/src/app/features/coaching')]]
]){
 cpSync(resolve(root,'artifacts',previous),resolve(release,kind),{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
 for(const file of files){
  // Staff shared plan code has an intentional older live-only merge; replace only its complete dependency closure.
  if(kind==='staff-source'&&shared.includes(file)){const target=resolve(release,kind,file);mkdirSync(resolve(target,'..'),{recursive:true});writeFileSync(target,git('show',revision+':'+file));}
  else execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/'+kind,'-'],{cwd:root,input:git('diff','--binary',base,revision,'--',file)});
 }
 provenance.sources.push({kind,previous,base,files});
 if(kind!=='source'){
  const app=resolve(release,kind,'clients/admin-panel');
  const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
  assert.equal(sha(resolve(app,'package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
  symlinkSync(resolve(root,'clients/admin-panel/node_modules'),resolve(app,'node_modules'),'junction');
 }
}
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify(provenance,null,2));
console.log(JSON.stringify({release,files:provenance.sources.map(x=>[x.kind,x.files.length])}));
