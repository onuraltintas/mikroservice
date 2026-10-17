import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,basename} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),name='coaching-followup-live-20261017',out=resolve('artifacts',name),revision='1c68ffc1';const git=(...a)=>execFileSync('git',a,{maxBuffer:32*1024*1024});
const completing=process.argv.includes('--complete');assert.ok(completing||!existsSync(resolve(out,'source')));mkdirSync(out,{recursive:true});
if(!completing)cpSync(resolve('artifacts/coaching-restore-profile-live-20261017/source'),resolve(out,'source'),{recursive:true,filter:p=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(p)&&!basename(p).startsWith('.env')});
if(!existsSync(resolve(out,'source/services/notification-service/Notification.API/Program.cs'))){git('archive','a12f470a','--output='+resolve(out,'notification-base.tar'),'services/notification-service');execFileSync('tar',['-xf',resolve(out,'notification-base.tar'),'-C',resolve(out,'source')]);}
const provenance={revision:git('rev-parse',revision).toString().trim(),services:['notification-service','coaching-service','staff-portal'],migration:null,files:[]};
for(const [base,scope] of [['7609d374','services/coaching-service'],['7609d374','shared/EduPlatform.Shared.Contracts'],['a12f470a','services/notification-service']]){
 const files=git('diff','--name-only',base,revision,'--',scope).toString().trim().split('\n').filter(Boolean);
 for(const file of files)if(!completing||scope==='services/notification-service')execFileSync('git',['apply','--recount','--directory=artifacts/'+name+'/source','-'],{input:git('diff','--binary',base,revision,'--',file)});
 provenance.files.push({scope,base,files});
}
const staff=resolve('artifacts/coaching-followup-check-20261017/staff-source');
const ui=JSON.parse(readFileSync('artifacts/coaching-followup-check-20261017/source-provenance.json'));
const sha=b=>createHash('sha256').update(b).digest('hex');
for(const file of ui.files)assert.equal(readFileSync(resolve(staff,file),'utf8').replaceAll('\r\n','\n'),git('show',revision+':'+file).toString().replaceAll('\r\n','\n'));
assert.ok(readFileSync(resolve(staff,'clients/admin-panel/dist/staff-portal/browser/index.html'),'utf8').includes('<base href="/staff/">'));
provenance.staff={...ui,validatedAgainst:revision};
writeFileSync(resolve(out,'source-provenance.json'),JSON.stringify(provenance,null,2));
console.log(JSON.stringify({out,scopes:provenance.files.map(x=>[x.scope,x.files.length]),staffFiles:ui.files.length}));
