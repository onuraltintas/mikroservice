import {readFileSync,writeFileSync,copyFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const tag='coaching-daily-agenda-20261021',root=resolve('artifacts/coaching-daily-agenda-live-20261021'),pack=resolve(root,'package');
if(existsSync(pack))throw Error('Package already sealed.');
const files=[],hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
function write(path,text){const p=resolve(pack,path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,text.replaceAll('\r\n','\n'));files.push(path);}
function copy(path,name){const p=resolve(pack,name);mkdirSync(dirname(p),{recursive:true});copyFileSync(path,p);files.push(name);}
const frontend=[
 'src/app/core/services/coaching-daily-agenda.service.ts',
 ...['ts','html','scss'].map(ext=>'src/app/features/coaching-portal/pages/student-daily-agenda.component.'+ext),
 ...['ts','html','scss'].map(ext=>'src/app/features/coaching-portal/pages/coaching-portal-home.component.'+ext),
 'src/app/features/coaching-portal/pages/student-weekly-home.component.ts',
 ...['component.ts','component.html','service.ts'].map(ext=>'projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.'+ext),
 'projects/staff-portal/src/app/features/coaching/coaching-teacher-workspace.component.ts'];
for(const path of frontend)write('overlay/'+path,readFileSync('clients/admin-panel/'+path,'utf8'));
let deploy=readFileSync('tools/releases/coaching-calendar-menu-20261021-r2/deploy.sh','utf8').replaceAll('\r\n','\n').replaceAll('coaching-calendar-menu-20261021-r2',tag).replace('base=/var/lib/eduivme/releases/coaching-decision-work-20261020','base=/var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4').replace('cp "$base/final.compose.json"','cp "$base/production.compose.json"').replaceAll('coaching_calendar_menu_probe_20261021','coaching_daily_agenda_probe_20261021').replace('coaching-calendar-menu-builder-20261021','coaching-daily-agenda-builder-20261021').replace('nine scoped portal files','twelve scoped portal files');
deploy=deploy.replace(' docker exec -i postgres pg_restore',' docker exec -i postgres pg_restore');
deploy=deploy.replace(' docker run --rm --memory=1g --cpus=1 --network', ' bash history.sh probe "$drill" drill.rows.before.txt\n docker run --rm --memory=1g --cpus=1 --network');
deploy=deploy.replace(' cat drill-probe.log; stable;', ' bash history.sh probe "$drill" drill.rows.after.txt\n cmp drill.rows.before.txt drill.rows.after.txt\n cat drill-probe.log; stable;');
write('deploy.sh',deploy);
let history=readFileSync('tools/releases/coaching-calendar-menu-20261021-r2/history.sh','utf8').replaceAll('coaching-calendar-menu-20261021-r2',tag).replace(' ELSE \'\' END,table_schema,table_name)', ' WHEN table_schema=\'identity\' AND table_name=\'users\' THEN \'-CASE WHEN "Id" IN (\'\'473bd2d6-e3db-47c4-be56-eeedec0022ed\'\'::uuid,\'\'b184504c-a3fa-4f7d-ad40-71fe418eaa10\'\'::uuid) THEN ARRAY[\'\'LastLoginAt\'\',\'\'UpdatedAt\'\',\'\'Version\'\'] ELSE ARRAY[]::text[] END\'\n ELSE \'\' END,table_schema,table_name)').replace('nullable decision links normalized, only the named test plan version/timestamp tracked separately.','only named test-plan version/timestamp and two test-account login metadata normalized.');
write('history.sh',history);
const base='artifacts/coaching-calendar-menu-live-20261021/package-r2/';
for(const name of ['runtime-admin-panel.cjs','runtime-staff-portal.cjs','build-portals.cjs','public-check.sh'])write(name,readFileSync(base+name,'utf8').replaceAll('coaching-calendar-menu-20261021-r2',tag));
write('.dockerignore','source\n*.before.backup\n*.json\n*.txt\n*.log\n*.sha256\n');
for(const ext of ['dll','pdb'])copy(resolve(root,'publish/coaching/Coaching.API.'+ext),'payload/coaching/Coaching.API.'+ext);
const modules=['Coaching.API','Coaching.Application','Coaching.Infrastructure','Coaching.Domain','EduPlatform.Shared.Contracts'];
write('baseline-coaching.sha256',modules.flatMap(m=>['dll','pdb'].map(e=>hash(resolve(root,'baseline/Coaching',m+'.'+e))+'  /app/'+m+'.'+e)).join('\n')+'\n');
copy(resolve(root,'verification/Verification.dll'),'Verification.dll');
write('coaching-service.Dockerfile','ARG BASE_IMAGE\nFROM ${BASE_IMAGE}\nARG SOURCE_REVISION\nCOPY payload/coaching/ /app/\nLABEL org.opencontainers.image.revision="${SOURCE_REVISION}"\n');
writeFileSync(resolve(pack,'payload.sha256'),files.map(x=>hash(resolve(pack,x))+'  '+x).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'release.tar.gz'),'-C',pack,'.']);
const sha=hash(resolve(root,'release.tar.gz'));
writeFileSync('tools/releases/'+tag+'/start.sh',`set -euo pipefail\numask 077\nrelease=/var/lib/eduivme/releases/${tag}\necho '${sha}  /var/lib/eduivme/releases/${tag}.tar.gz' | sha256sum -c -\ntest ! -e "$release"\nmkdir -m 700 "$release"\ntar -xzf "$release.tar.gz" -C "$release"\ncd "$release"\nbash -n deploy.sh\nbash deploy.sh prepare\nnohup bash deploy.sh build > build-driver.log 2>&1 </dev/null &\necho 'Daily agenda isolated build started.'\n`);
console.log(JSON.stringify({tag,sha256:sha,frontendFiles:frontend.length,backendFiles:2}));
