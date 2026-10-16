import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(), name='coaching-curriculum-live-20261016';
const release=resolve(root,'artifacts',name), previous=resolve(root,'artifacts/coaching-outcomes-live-20261016-r2');
const revision='ddc089ef', base='374f79e2';
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
const expected=['8ac948c364d83159f23a5d9dfb97e2c8b121c02110a743c48cc663f3d298d933','c047534019706f9af8cf63695b28bfb948d399733e89b27c0cd74ae1436f4623','1e194ff7264a00fd4fe8eeaf699f43b13fbf67036ddd7de5d6b5267241373b0f','aab49286623710a439dd51a7fd77f17b5146af57469248b535c4f9e175dd14d6'];
const assemblies=['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'];
assemblies.forEach((n,i)=>assert.equal(sha(resolve(previous,'publish/coaching',n+'.dll')),expected[i]));
assert.equal(sha(resolve(previous,'admin-source/clients/admin-panel/package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
const provenance={revision:git('rev-parse',revision).toString().trim(),baseRevision:git('rev-parse',base).toString().trim(),previousRelease:'coaching-outcomes-20261016',services:['coaching-service','admin-panel'],files,baselineAssemblies:Object.fromEntries(assemblies.map((n,i)=>[n,expected[i]]))};
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify(provenance,null,2));
console.log(JSON.stringify({release,files:files.length,revision:provenance.revision}));
