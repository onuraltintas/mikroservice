import { cpSync, mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { resolve, relative } from 'node:path';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';
const variant=process.argv[2]??'r2'; assert.ok(/^r[2-9]$/.test(variant));
const root=process.cwd(), release=resolve(root,'artifacts/coaching-outcomes-live-20261016-r2'), pack=resolve(release,'package-'+variant);
assert.ok(!existsSync(pack)); mkdirSync(pack,{recursive:true});
const sha=file=>createHash('sha256').update(readFileSync(file)).digest('hex');
const target=resolve(pack,'payload/coaching');mkdirSync(target,{recursive:true});
for(const assembly of ['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'])
  for(const extension of ['dll','pdb'])cpSync(resolve(release,'publish/coaching',assembly+'.'+extension),resolve(target,assembly+'.'+extension));
cpSync(resolve(release,'publish/verification'),resolve(pack,'payload/verification'),{recursive:true});
cpSync(resolve(root,'data/coaching-outcomes/lgs-matematik-2026-pilot.json'),resolve(pack,'payload/verification/lgs-matematik-2026-pilot.json'));
cpSync(resolve(release,'admin-source/clients/admin-panel/dist/admin-panel'),resolve(pack,'payload/admin'),{recursive:true});
cpSync(resolve(release,'staff-source/clients/admin-panel/dist/staff-portal/browser'),resolve(pack,'payload/staff'),{recursive:true});
for(const service of ['coaching-service','admin-panel','staff-portal']) {
  let content=readFileSync(resolve(root,'tools/releases/coaching-learning-20261016',service+'.Dockerfile'),'utf8').replaceAll('coaching-learning-20261016','coaching-outcomes-20261016');
  writeFileSync(resolve(pack,service+'.Dockerfile'),content.replaceAll('\r\n','\n'));
}
cpSync(resolve(root,'tools/releases/coaching-outcomes-20261016/deploy.sh'),resolve(pack,'deploy.sh'));
cpSync(resolve(release,'source-provenance.json'),resolve(pack,'source-provenance.json'));
writeFileSync(resolve(pack,'source-revision.txt'),execFileSync('git',['rev-parse','374f79e2'],{encoding:'utf8'}));
function files(dir){return readdirSync(dir).flatMap(n=>statSync(resolve(dir,n)).isDirectory()?files(resolve(dir,n)):[resolve(dir,n)]);}
assert.ok(files(pack).every(f=>!relative(pack,f).split(/[\\/]/).some(n=>n==='.env'||n.startsWith('.env.'))));
writeFileSync(resolve(pack,'payload.sha256'),files(pack).map(f=>sha(f)+'  '+relative(pack,f).replaceAll('\\','/')).join('\n')+'\n');
const archive=resolve(release,'release-'+variant+'.tar.gz');execFileSync('tar',['-czf',archive,'-C',pack,'.']);
console.log(JSON.stringify({files:files(pack).length,archive,sha256:sha(archive)}));
