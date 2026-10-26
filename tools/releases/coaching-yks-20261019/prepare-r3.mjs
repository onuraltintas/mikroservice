import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd(),previous=resolve(root,'artifacts/coaching-yks-20261019-r2'),release=resolve(root,'artifacts/coaching-yks-20261019-r3');
assert.ok(!existsSync(release));mkdirSync(release,{recursive:true});
const filter=p=>!/[\\/](bin|obj|node_modules|\.angular|\.git)([\\/]|$)/.test(p)&&!/[\\/]Data[\\/]Migrations[\\/]\d+_Yks/.test(p);
cpSync(join(previous,'backend-source'),join(release,'backend-source'),{recursive:true,filter});
const migrationRoot='services/coaching-service/Coaching.Infrastructure/Data/Migrations';
cpSync(resolve(root,'artifacts/coaching-clean-start-20261018/backend-source',migrationRoot,'CoachingDbContextModelSnapshot.cs'),join(release,'backend-source',migrationRoot,'CoachingDbContextModelSnapshot.cs'));
const weekly='services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs';
cpSync(resolve(root,weekly),join(release,'backend-source',weekly));
for(const surface of ['admin','staff']){
 const output=join(release,surface+'-tree/clients/admin-panel/dist',surface==='admin'?'admin-panel':'staff-portal/browser');
 mkdirSync(output,{recursive:true});cpSync(join(previous,'package/payload',surface),output,{recursive:true});
}
cpSync(join(previous,'expected-base-dlls.sha256'),join(release,'expected-base-dlls.sha256'));
const manifest=JSON.parse(readFileSync(join(previous,'source-provenance.json'),'utf8'));
manifest.frontendArtifactsFrom={release:'coaching-yks-20261019-r2',archiveSha256:'b129eaff5ad4dfe7969bd5cc237f0972c3949a9740ef9735b1f397b9e0076a3a'};
manifest.migrationBaseline='Exact live snapshot; unrelated assignment instruction attachment model is excluded.';
writeFileSync(join(release,'source-provenance.initial.json'),JSON.stringify(manifest,null,2));
console.log('Fresh R3 sources prepared; live snapshot retained and unrelated draft metadata excluded.');
