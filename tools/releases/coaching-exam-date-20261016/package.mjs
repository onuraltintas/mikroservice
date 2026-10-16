import {cpSync,mkdirSync,readFileSync,writeFileSync,readdirSync,statSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,relative} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-exam-date-live-20261016'),pack=resolve(release,'package');
assert.ok(!existsSync(pack));mkdirSync(pack,{recursive:true});
const sha=file=>createHash('sha256').update(readFileSync(file)).digest('hex');
mkdirSync(resolve(pack,'payload/coaching'),{recursive:true});
for(const name of ['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'])
  for(const ext of ['dll','pdb'])cpSync(resolve(release,'publish/coaching',name+'.'+ext),resolve(pack,'payload/coaching',name+'.'+ext));
cpSync(resolve(release,'admin-source/clients/admin-panel/dist/admin-panel'),resolve(pack,'payload/admin'),{recursive:true});
for(const service of ['coaching-service','admin-panel'])writeFileSync(resolve(pack,service+'.Dockerfile'),
  readFileSync(resolve(root,'tools/releases/coaching-learning-20261016',service+'.Dockerfile'),'utf8').replaceAll('coaching-learning-20261016','coaching-exam-date-20261016').replaceAll('\r\n','\n'));
writeFileSync(resolve(pack,'deploy.sh'),readFileSync(resolve(root,'tools/releases/coaching-exam-date-20261016/deploy.sh'),'utf8').replaceAll('\r\n','\n'));
cpSync(resolve(release,'source-provenance.json'),resolve(pack,'source-provenance.json'));
writeFileSync(resolve(pack,'source-revision.txt'),execFileSync('git',['rev-parse','dfbb4335'],{encoding:'utf8'}));
function files(dir){return readdirSync(dir).flatMap(n=>statSync(resolve(dir,n)).isDirectory()?files(resolve(dir,n)):[resolve(dir,n)]);}
assert.ok(files(pack).every(f=>!relative(pack,f).split(/[\\/]/).some(n=>n==='.env'||n.startsWith('.env.'))));
writeFileSync(resolve(pack,'payload.sha256'),files(pack).map(f=>sha(f)+'  '+relative(pack,f).replaceAll('\\','/')).join('\n')+'\n');
const archive=resolve(release,'release.tar.gz');execFileSync('tar',['-czf',archive,'-C',pack,'.']);
writeFileSync(resolve(release,'archive.sha256'),sha(archive)+'\n');
console.log(JSON.stringify({archive,sha256:sha(archive),files:files(pack).length}));
