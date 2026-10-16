import { execFileSync } from 'node:child_process';
import { cpSync, readFileSync, writeFileSync, mkdirSync, existsSync, unlinkSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
const root=process.cwd(), release=resolve(root,'artifacts/coaching-learning-live-20261016');
const files=execFileSync('git',['diff','--name-only','61955eba','8757ca75','--','clients/admin-panel'],{encoding:'utf8'}).trim().split('\n');
for(const [kind,base] of [['admin','artifacts/coaching-daily-flow-fixes-20261016/source'],['staff','.tmp/institution-benchmark-20261016/staff']]) {
  const destination=resolve(release,kind+'-source');
  if(existsSync(destination))throw new Error('Existing source preserved: '+destination);
  cpSync(resolve(root,base,'clients'),resolve(destination,'clients'),{recursive:true,filter:path=>!/[\\/](node_modules|dist|\.angular|\.git)([\\/]|$)/.test(path)});
  for(const path of files) {
    if(kind==='admin'&&path.includes('/projects/'))continue;
    if(kind==='staff'&&!path.includes('/projects/staff-portal/')&&!path.includes('/shared/'))continue;
    const target=resolve(destination,path); mkdirSync(dirname(target),{recursive:true});
    writeFileSync(target,execFileSync('git',['show','8757ca75:'+path],{maxBuffer:10*1024*1024}));
  }
}
const excluded=resolve(release,'source/services/identity-service/Identity.API/Controllers/CourseManagementAuthController.cs');
if(existsSync(excluded))unlinkSync(excluded);
console.log('UI sources based on the exact latest live admin/staff source snapshots; only task files overlaid. Future course auth controller excluded.');
