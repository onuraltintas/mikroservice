import {cpSync,mkdirSync,readFileSync,writeFileSync,symlinkSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-targets-20261019-r2');
assert.ok(!existsSync(release));mkdirSync(release,{recursive:true});
const tree=join(release,'admin-tree/clients');mkdirSync(tree,{recursive:true});
const filter=p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p);
for(const folder of ['admin-panel','shared'])cpSync(resolve(root,'artifacts/coaching-targets-20261019/admin-tree/clients',folder),join(tree,folder),{recursive:true,filter});
const file='src/app/core/services/coaching-portal.service.ts',dest=join(tree,'admin-panel',file);
const before=readFileSync(dest,'utf8');
const old='        category: request.category,',current=readFileSync(resolve(root,'clients/admin-panel',file),'utf8');
const replacement=current.split(/\r?\n/).find(line=>line.includes("1: 'ExamPreparation'"));
assert.equal(before.split(old).length,2);assert.ok(replacement);writeFileSync(dest,before.replace(old,replacement));
for(const test of ['coaching-goal-category.spec.ts','coaching-portal.service.spec.ts'])cpSync(resolve(root,'clients/admin-panel/src/app/core/services',test),join(tree,'admin-panel/src/app/core/services',test));
writeFileSync(join(tree,'admin-panel/tsconfig.goal-category.spec.json'),JSON.stringify({extends:'./tsconfig.json',compilerOptions:{outDir:'./out-tsc/spec',types:['vitest/globals','node']},include:[],files:['src/app/core/services/coaching-goal-category.spec.ts','src/app/core/services/coaching-portal.service.spec.ts']}));
symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(tree,'admin-panel/node_modules'),'junction');
let deploy=readFileSync(resolve(root,'tools/target-catalog/deploy-target-release.sh'),'utf8');
deploy=deploy.replaceAll('coaching-targets-20261019','coaching-targets-20261019-r2')
 .replace('services=(coaching-service admin-panel staff-portal)','services=(admin-panel)')
 .replace('expected="eduivme/$service:coaching-wizard-coverage-20261019"','expected="eduivme/$service:coaching-targets-20261019"')
 .replace('/coaching-wizard-coverage-20261019/production.compose.json','/coaching-targets-20261019/production.compose.json');
deploy=deploy.replace(/jq --arg tag "\$tag" [^\n]+rollback.compose.json > production.compose.json/,`jq --arg tag "$tag" '.services["admin-panel"].image="eduivme/admin-panel:"+$tag' rollback.compose.json > production.compose.json`);
writeFileSync(join(release,'deploy.sh'),deploy.replaceAll('\r\n','\n'));
console.log('Student goal category hotfix isolated; API and staff portal remain on R1.');
