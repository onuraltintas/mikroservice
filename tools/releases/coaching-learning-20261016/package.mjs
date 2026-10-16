import { cpSync, mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { resolve, relative } from 'node:path';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';
const variant=process.argv[2]??''; assert.ok(/^(|r[2-9])$/.test(variant));
const root=process.cwd(),release=resolve(root,'artifacts/coaching-learning-live-20261016'),pack=resolve(release,variant?'package-'+variant:'package');
assert.ok(!existsSync(pack),'Do not overwrite an existing reviewed package.'); mkdirSync(pack,{recursive:true});
const sha=file=>createHash('sha256').update(readFileSync(file)).digest('hex');
assert.equal(sha(resolve(release,'publish/identity/EduPlatform.Shared.Contracts.dll')),sha(resolve(release,'publish/coaching/EduPlatform.Shared.Contracts.dll')));
for(const [kind,assemblies] of [['identity',['Identity.API','Identity.Application','Identity.Infrastructure','EduPlatform.Shared.Contracts']],['coaching',['Coaching.API','Coaching.Application','Coaching.Infrastructure','Coaching.Domain','EduPlatform.Shared.Contracts']]]) {
  const target=resolve(pack,'payload',kind);mkdirSync(target,{recursive:true});
  for(const assembly of assemblies)for(const extension of ['dll','pdb'])cpSync(resolve(release,'publish',kind,assembly+'.'+extension),resolve(target,assembly+'.'+extension));
}
cpSync(resolve(release,'publish/verification'),resolve(pack,'payload/verification'),{recursive:true});
cpSync(resolve(release,'admin-source/clients/admin-panel/dist/admin-panel'),resolve(pack,'payload/admin'),{recursive:true});
cpSync(resolve(release,'staff-source/clients/admin-panel/dist/staff-portal/browser'),resolve(pack,'payload/staff'),{recursive:true});
for(const file of ['deploy.sh','identity-service.Dockerfile','coaching-service.Dockerfile','admin-panel.Dockerfile','staff-portal.Dockerfile'])cpSync(resolve(root,'tools/releases/coaching-learning-20261016',file),resolve(pack,file));
writeFileSync(resolve(pack,'source-revision.txt'),execFileSync('git',['rev-parse','8757ca75'],{encoding:'utf8'}));
writeFileSync(resolve(pack,'source-provenance.json'),JSON.stringify({revision:'8757ca75',liveDocumentsAccountedFor:768,backendIsolatedFromLivePdb:true,excluded:'unreleased course management controller and working tree changes',adminBaseline:'coaching-daily-flow-fixes-20261016-r2',staffBaseline:'institution-benchmark-20261016',tests:{backend:760,frontend:81,browser:4}},null,2));
function files(dir){return readdirSync(dir).flatMap(n=>statSync(resolve(dir,n)).isDirectory()?files(resolve(dir,n)):[resolve(dir,n)]);}
assert.ok(files(pack).every(f=>!relative(pack,f).split(/[\\/]/).some(n=>n==='.env'||n.startsWith('.env.'))));
writeFileSync(resolve(pack,'payload.sha256'),files(pack).map(f=>sha(f)+'  '+relative(pack,f).replaceAll('\\','/')).join('\n')+'\n');
const archive=resolve(release,variant?'release-'+variant+'.tar.gz':'release.tar.gz');execFileSync('tar',['-czf',archive,'-C',pack,'.']);
console.log(JSON.stringify({files:files(pack).length,archive,sha256:sha(archive)}));
