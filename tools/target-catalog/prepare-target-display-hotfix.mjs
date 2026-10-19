import {cpSync,mkdirSync,readFileSync,writeFileSync,symlinkSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-targets-20261019-r3');
assert.ok(!existsSync(release));mkdirSync(release,{recursive:true});
const tree=join(release,'admin-tree/clients');mkdirSync(tree,{recursive:true});
const filter=p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p);
for(const folder of ['admin-panel','shared'])cpSync(resolve(root,'artifacts/coaching-targets-20261019-r2/admin-tree/clients',folder),join(tree,folder),{recursive:true,filter});
const page='src/app/features/coaching-portal/pages/';
for(const file of ['coaching-portal-progress.component.ts','coaching-portal-progress.component.spec.ts'])cpSync(resolve(root,'clients/admin-panel',page+file),join(tree,'admin-panel',page+file));
const specs=[page+'target-admission-search.component.spec.ts',page+'student-goal-target.component.spec.ts',page+'coaching-portal-progress.component.spec.ts','src/app/core/services/coaching-goal-category.spec.ts','src/app/core/services/coaching-portal.service.spec.ts'];
writeFileSync(join(tree,'admin-panel/tsconfig.targets-final.spec.json'),JSON.stringify({extends:'./tsconfig.json',compilerOptions:{outDir:'./out-tsc/spec',types:['vitest/globals','node']},include:[],files:specs}));
symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(tree,'admin-panel/node_modules'),'junction');
let deploy=readFileSync(resolve(root,'artifacts/coaching-targets-20261019-r2/deploy.sh'),'utf8');
deploy=deploy.replaceAll('coaching-targets-20261019-r2','coaching-targets-20261019-r3')
 .replace('expected="eduivme/$service:coaching-targets-20261019"','expected="eduivme/$service:coaching-targets-20261019-r2"')
 .replace('/coaching-targets-20261019/production.compose.json','/coaching-targets-20261019-r2/production.compose.json');
writeFileSync(join(release,'deploy.sh'),deploy);
console.log('Only goal category labels and summary refresh changed from R2.');
