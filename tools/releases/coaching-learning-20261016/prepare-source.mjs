import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, dirname } from 'node:path';
const root = process.cwd(), release = resolve(root, 'artifacts/coaching-learning-live-20261016');
const source = resolve(release, 'source');
const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 128*1024*1024 });
const documents = JSON.parse(readFileSync(resolve(release, 'live-sources.json')));
const task = new Set(git('diff','--name-only','61955eba..8757ca75','--','services','shared').toString().trim().split('\n'));
const candidates = ['.tmp/admin-edit-access-source-20261015','.tmp/admin-user-service-source-20261015',
  '.tmp/admin-user-dual-source-20261015','.tmp/coaching-release-ac014834','tools/releases/google-direct-20261013/base'];
function match(bytes, hash) {
  const text = bytes.toString('utf8').replace(/^\uFEFF/, '').replaceAll('\r\n','\n');
  return [bytes,Buffer.from(text),Buffer.from(text.replaceAll('\n','\r\n'))].some(b =>
    [b,Buffer.concat([Buffer.from([239,187,191]),b])].some(v => createHash(hash.length === 40 ? 'sha1':'sha256').update(v).digest('hex') === hash));
}
const rows=[]; const found=new Map();
for (const doc of documents) {
  const key=doc.path;
  if (found.has(key)) { if (found.get(key).hash !== doc.hash) rows.push({...doc,status:'different-live-shared-source'}); continue; }
  let bytes, origin;
  for (const dir of [root, ...candidates.map(x=>resolve(root,x))]) {
    const file=resolve(dir,key); if(existsSync(file) && match(readFileSync(file),doc.hash)) { bytes=readFileSync(file);origin=dir;break; }
  }
  if (!bytes) {
    const commits=git('log','-80','--format=%H','--',key).toString().trim().split('\n');
    for (const commit of commits) { try { const data=git('show',`${commit}:${key}`); if(match(data,doc.hash)){bytes=data;origin=commit;break;} } catch {} }
  }
  let baseMatches=false;
  if(task.has(key)){try{baseMatches=match(git('show',`61955eba:${key}`),doc.hash);}catch{}}
  found.set(key,{hash:doc.hash,bytes,origin});
  rows.push({...doc,taskChange:task.has(key),baseMatches,status:bytes?'found':'MISSING',origin});
}
writeFileSync(resolve(release,'source-comparison.json'), JSON.stringify(rows,null,2));
console.log(JSON.stringify({documents:documents.length,missing:rows.filter(x=>x.status!=='found'),changedBaseMismatch:rows.filter(x=>x.taskChange&&!x.baseMatches).map(x=>x.path)}));
if (process.argv.includes('--apply')) {
  if(rows.some(x=>x.status!=='found'))throw new Error('Every live source must be accounted for.');
  if(existsSync(source))throw new Error('Isolated source already exists.');
  mkdirSync(source,{recursive:true});
  const archive=resolve(release,'source-head.tar');
  writeFileSync(archive,git('archive','8757ca75','services/identity-service','services/coaching-service','shared','clients/admin-panel'));
  execFileSync('tar',['-xf',archive,'-C',source]);
  for(const [path,data] of found) {
    if(task.has(path)&&rows.find(x=>x.path===path).baseMatches)continue;
    mkdirSync(dirname(resolve(source,path)),{recursive:true});writeFileSync(resolve(source,path),data.bytes);
    if(task.has(path)) {
      const patch=git('diff','61955eba','8757ca75','--',path);
      execFileSync('git',['apply','--directory=artifacts/coaching-learning-live-20261016/source','--recount','-'],{cwd:root,input:patch});
    }
  }
  console.log('Isolated backend sources prepared.');
}
