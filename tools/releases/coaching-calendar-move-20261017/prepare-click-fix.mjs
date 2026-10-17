import {cpSync,mkdirSync,readFileSync,writeFileSync,symlinkSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd(),previous='artifacts/coaching-calendar-move-live-20261017',name='coaching-calendar-move-live-20261017-r2',release=resolve('artifacts',name),file='clients/admin-panel/src/app/features/coaching-portal/pages/study-plan-calendar.component.ts';
assert.ok(!existsSync(resolve(release,'admin-source')),'Preserve reviewed release sources.');
mkdirSync(release,{recursive:true});
for(const kind of ['admin-source','staff-source']){
 cpSync(resolve(previous,kind),resolve(release,kind),{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular)([\\/]|$)/.test(p)});
 for(const path of kind==='staff-source'?[file,'clients/admin-panel/angular.json']:[file])execFileSync('git',['apply','--directory=artifacts/'+name+'/'+kind,'-'],{input:execFileSync('git',['diff','--binary','2acdd750','7f1f61de','--',path]),cwd:root});
 symlinkSync(resolve('clients/admin-panel/node_modules'),resolve(release,kind,'clients/admin-panel/node_modules'),'junction');
}
writeFileSync(resolve(release,'source-provenance.json'),JSON.stringify({revision:'7f1f61deccd6ce9b847eed90a650262397db476b',previousRelease:'coaching-calendar-move-20261017',services:['admin-panel','staff-portal'],files:[file,'clients/admin-panel/angular.json'],databaseMigration:false},null,2));
for(const filename of ['ui-server.mjs','playwright.config.mjs'])writeFileSync(resolve(release,filename),readFileSync(resolve(previous,filename),'utf8').replaceAll('coaching-calendar-move-live-20261017','coaching-calendar-move-live-20261017-r2'));
