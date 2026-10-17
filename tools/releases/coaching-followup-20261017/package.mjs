import {cpSync,mkdirSync,readFileSync,writeFileSync,readdirSync,statSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,relative} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const out=resolve('artifacts/coaching-followup-live-20261017'),pack=resolve(out,'package');assert.ok(!existsSync(pack));mkdirSync(pack,{recursive:true});
const sha=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
assert.equal(sha(resolve(out,'publish/coaching/EduPlatform.Shared.Contracts.dll')),sha(resolve(out,'publish/notification/EduPlatform.Shared.Contracts.dll')));
for(const [folder,prefix] of [['coaching','Coaching'],['notification','Notification']]){
 mkdirSync(resolve(pack,'payload',folder),{recursive:true});
 for(const name of [prefix+'.API',prefix+'.Application',prefix+'.Domain',prefix+'.Infrastructure','EduPlatform.Shared.Contracts'])for(const ext of ['dll','pdb'])cpSync(resolve(out,'publish',folder,name+'.'+ext),resolve(pack,'payload',folder,name+'.'+ext));
}
cpSync(resolve('artifacts/coaching-followup-check-20261017/staff-source/clients/admin-panel/dist/staff-portal/browser'),resolve(pack,'payload/staff'),{recursive:true});
for(const [service,payload] of [['coaching-service','coaching'],['notification-service','notification']])writeFileSync(resolve(pack,service+'.Dockerfile'),`ARG BASE_IMAGE\nFROM \${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY payload/${payload}/ /app/\nLABEL org.opencontainers.image.revision="\${SOURCE_REVISION}" org.opencontainers.image.release="coaching-followup-20261017"\n`);
writeFileSync(resolve(pack,'staff-portal.Dockerfile'),readFileSync('tools/releases/coaching-learning-20261016/staff-portal.Dockerfile','utf8').replaceAll('coaching-learning-20261016','coaching-followup-20261017'));
writeFileSync(resolve(pack,'deploy.sh'),readFileSync('tools/releases/coaching-followup-20261017/deploy.sh','utf8').replaceAll('\r\n','\n'));cpSync(resolve(out,'source-provenance.json'),resolve(pack,'source-provenance.json'));
writeFileSync(resolve(pack,'source-revision.txt'),JSON.parse(readFileSync(resolve(out,'source-provenance.json'))).revision+'\n');
const files=p=>readdirSync(p).flatMap(n=>statSync(resolve(p,n)).isDirectory()?files(resolve(p,n)):[resolve(p,n)]);
assert.ok(files(pack).every(f=>!relative(pack,f).split(/[\\/]/).some(x=>x==='.env'||x.startsWith('.env.'))));
writeFileSync(resolve(pack,'payload.sha256'),files(pack).map(f=>sha(f)+'  '+relative(pack,f).replaceAll('\\','/')).join('\n')+'\n');
const archive=resolve(out,'release.tar.gz');execFileSync('tar',['-czf',archive,'-C',pack,'.']);writeFileSync(resolve(out,'archive.sha256'),sha(archive)+'\n');console.log(JSON.stringify({archive,sha256:sha(archive),files:files(pack).length}));
