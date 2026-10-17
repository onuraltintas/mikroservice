import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),name='coaching-weekly-calendar-live-20261017';
const release=resolve(root,'artifacts',name),previous=resolve(root,'artifacts/coaching-exam-date-live-20261016');
const revision='12951c5a',base='dfbb4335';
const git=(...args)=>execFileSync('git',args,{cwd:root,maxBuffer:32*1024*1024});
assert.ok(!existsSync(resolve(release,'source')),'Preserve prepared releases.');mkdirSync(release,{recursive:true});
assert.equal(JSON.parse(readFileSync(resolve(previous,'source-provenance.json'))).revision,git('rev-parse',base).toString().trim());
const files=git('diff','--name-only',base,revision,'--','services/coaching-service','clients/admin-panel/src').toString().trim().split('\n').filter(x=>!x.endsWith('.spec.ts'));
assert.equal(files.length,20);
for(const [kind,prefix] of [['source','services/'],['admin-source','clients/']]){
  const dest=resolve(release,kind);
  cpSync(resolve(previous,kind),dest,{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
  for(const file of files.filter(x=>x.startsWith(prefix)))execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/'+kind,'-'],{cwd:root,input:git('diff','--binary',base,revision,'--',file)});
}
const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const expected=['30b779db2e4fdde6755b4717474fed0d3268b0686eb02fa94e558f8cf57488a1','4df3ac2393da566bd6b87e29c37e3fd229610b718e7e2912a07df6ee6ebe82c7','fc7118b1019365b0075a3ae4402b2b6b138f3df9f4093ec67b47132944ff38b5','99abf0630c8bacf7f1e6552b0ed74fb5e2e1a602d7f991ea9ccb8995d297bb21'];
const assemblies=['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'];assemblies.forEach((n,i)=>assert.equal(sha(resolve(previous,'publish/coaching',n+'.dll')),expected[i]));
assert.equal(sha(resolve(previous,'admin-source/clients/admin-panel/package-lock.json')),sha(resolve(root,'clients/admin-panel/package-lock.json')));
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify({revision:git('rev-parse',revision).toString().trim(),baseRevision:git('rev-parse',base).toString().trim(),previousRelease:'coaching-exam-date-20261016',services:['coaching-service','admin-panel'],files,baselineAssemblies:Object.fromEntries(assemblies.map((n,i)=>[n,expected[i]]))},null,2));
console.log(JSON.stringify({release,files:files.length,revision}));
