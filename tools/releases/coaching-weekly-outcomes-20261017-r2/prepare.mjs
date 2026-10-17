import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),name='coaching-weekly-outcomes-live-20261017-r2';
const release=resolve(root,'artifacts',name),previous=resolve(root,'artifacts/coaching-weekly-outcomes-live-20261017');
const revision=process.argv[2],base='119fc711';assert.match(revision??'',/^[a-f0-9]{8,40}$/);
const git=(...args)=>execFileSync('git',args,{cwd:root,maxBuffer:32*1024*1024});
assert.ok(!existsSync(resolve(release,'source')),'Preserve prepared releases.');mkdirSync(release,{recursive:true});
assert.equal(JSON.parse(readFileSync(resolve(previous,'source-provenance.json'))).revision,git('rev-parse',base).toString().trim());
const files=git('diff','--name-only',base,revision,'--','services/coaching-service','clients/admin-panel/src').toString().trim().split('\n').filter(x=>!x.endsWith('.spec.ts'));
assert.ok(files.length===1);assert.ok(files.every(x=>x.startsWith('services/coaching-service/')||x.startsWith('clients/admin-panel/src/')));
for(const [kind,prefix] of [['source','services/'],['admin-source','clients/']]){
  const dest=resolve(release,kind);
  cpSync(resolve(previous,kind),dest,{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
  for(const file of files.filter(x=>x.startsWith(prefix)))execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/'+kind,'-'],{cwd:root,input:git('diff','--binary',base,revision,'--',file)});
}
const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const assemblies=['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'];
const baselineAssemblies=Object.fromEntries(assemblies.map(n=>{const hash=sha(resolve(previous,'publish/coaching',n+'.dll'));assert.equal(hash,sha(resolve(previous,'package/payload/coaching',n+'.dll')));return[n,hash];}));
assert.equal(sha(resolve(previous,'admin-source/clients/admin-panel/package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify({revision:git('rev-parse',revision).toString().trim(),baseRevision:git('rev-parse',base).toString().trim(),previousRelease:'coaching-weekly-outcomes-20261017',services:['coaching-service','admin-panel'],files,baselineAssemblies},null,2));
console.log(JSON.stringify({release,files:files.length,revision}));
