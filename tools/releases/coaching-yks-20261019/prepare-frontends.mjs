import {cpSync,existsSync,mkdirSync,symlinkSync,readFileSync,writeFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-yks-20261019-r2');
const filter=path=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(path);
for(const surface of ['admin','staff']){
 const tree=join(release,surface+'-tree/clients');assert.ok(!existsSync(tree));mkdirSync(tree,{recursive:true});
 cpSync(join(release,surface+'-source'),join(tree,'admin-panel'),{recursive:true,filter});
 const shared=surface==='admin'?'artifacts/coaching-study-wizard-live-20261017/admin-source/clients/shared':'artifacts/coaching-teacher-wizard-live-20261018/shared';
 cpSync(resolve(root,shared),join(tree,'shared'),{recursive:true,filter});
 symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(tree,'admin-panel/node_modules'),'junction');
}
const manifest=JSON.parse(readFileSync(join(release,'source-provenance.json'),'utf8'));
manifest.frontendProjectRoots={admin:'admin-tree/clients/admin-panel',staff:'staff-tree/clients/admin-panel'};
manifest.externalSharedBaselines={admin:'coaching-study-wizard-live-20261017/admin-source/clients/shared',staff:'coaching-teacher-wizard-live-20261018/shared'};
writeFileSync(join(release,'source-provenance.json'),JSON.stringify(manifest,null,2));
console.log('Portal source trees retain their original shared-file layout and versions.');
