import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync,symlinkSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';

const root=process.cwd(),previous=resolve(root,'artifacts/coaching-yks-20261019-r2');
const release=resolve(root,'artifacts/coaching-wizard-coverage-20261019');
assert.ok(!existsSync(release),'Never overwrite a reviewed release.');
const filter=p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p);
const page='src/app/features/coaching-portal/pages/';
const functionText=text=>text.slice(text.indexOf('  createProposal() {'),text.indexOf('  applyProposal() {'));
const sourceFunction=functionText(readFileSync(resolve(root,'clients/admin-panel',page+'student-study-plan-assistant.component.ts'),'utf8'));
const oldFunction=functionText(readFileSync(join(previous,'staff-tree/clients/admin-panel',page+'student-study-plan-assistant.component.ts'),'utf8'));
const sha=text=>createHash('sha256').update(text).digest('hex');
const changes=[];
for(const surface of ['admin','staff']){
 const tree=join(release,surface+'-tree/clients');mkdirSync(tree,{recursive:true});
 cpSync(join(previous,surface+'-tree/clients/admin-panel'),join(tree,'admin-panel'),{recursive:true,filter});
 cpSync(join(previous,surface+'-tree/clients/shared'),join(tree,'shared'),{recursive:true,filter});
 for(const file of ['study-plan-wizard.ts','student-study-plan-assistant.component.ts']){
  const target=join(tree,'admin-panel',page+file),before=readFileSync(target,'utf8');
  let after;
  if(file==='study-plan-wizard.ts')after=readFileSync(resolve(root,'clients/admin-panel',page+file),'utf8');
  else {assert.equal(functionText(before),oldFunction);after=before.replace(oldFunction,sourceFunction);}
  writeFileSync(target,after);changes.push({surface,file:page+file,before:sha(before),after:sha(after)});
 }
 symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(tree,'admin-panel/node_modules'),'junction');
}
writeFileSync(join(release,'source-provenance.json'),JSON.stringify({base:'coaching-yks-20261019-r3 frontend artifacts were built from these R2 trees',changes},null,2));
console.log('Two isolated portal trees prepared; only wizard allocation and coverage message changed.');
