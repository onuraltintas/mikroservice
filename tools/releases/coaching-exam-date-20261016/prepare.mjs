import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(), name='coaching-exam-date-live-20261016';
const release=resolve(root,'artifacts',name), previous=resolve(root,'artifacts/coaching-curriculum-live-20261016');
const revision='dfbb4335', base='ddc089ef';
const git=(...args)=>execFileSync('git',args,{cwd:root,maxBuffer:32*1024*1024});
assert.ok(!existsSync(resolve(release,'source')),'Preserve the prepared release.');
mkdirSync(release,{recursive:true});
const files=git('diff','--name-only',base,revision,'--','services/coaching-service','clients/admin-panel/src').toString().trim().split('\n').filter(x=>!x.endsWith('.spec.ts'));
for(const [kind,prefix] of [['source','services/'],['admin-source','clients/']]) {
  const dest=resolve(release,kind);
  cpSync(resolve(previous,kind),dest,{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
  for(const file of files.filter(x=>x.startsWith(prefix))) {
    const patch=git('diff','--binary',base,revision,'--',file);
    execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/'+kind,'-'],{cwd:root,input:patch});
  }
}
const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const expected=['ada90457b79e233d53ab511fd3a95af017d32fb0c859304cbc170e1651816b2a','a2765b478c5fd2a169485004102a7e344894a0425662524695e3cc456d389d1e','e2e2657fbdfad7558fb35301edaa3946100f663a1a3fedf58e79c5d19db7b068','59deb0da56d10c0706338828bfbe85011b1972c058ae29f89abfffe79b91ddb1'];
const assemblies=['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'];
assemblies.forEach((n,i)=>assert.equal(sha(resolve(previous,'publish/coaching',n+'.dll')),expected[i]));
assert.equal(sha(resolve(previous,'admin-source/clients/admin-panel/package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
const provenance={revision:git('rev-parse',revision).toString().trim(),baseRevision:git('rev-parse',base).toString().trim(),previousRelease:'coaching-curriculum-20261016',services:['coaching-service','admin-panel'],files,baselineAssemblies:Object.fromEntries(assemblies.map((n,i)=>[n,expected[i]]))};
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify(provenance,null,2));
console.log(JSON.stringify({release,files:files.length,revision:provenance.revision}));
