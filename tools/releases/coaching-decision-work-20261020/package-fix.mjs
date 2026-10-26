import {mkdirSync,copyFileSync,readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root=resolve('artifacts/coaching-decision-work-live-20261020'),pack=resolve(root,'http-validation-fix');mkdirSync(pack);
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex'),files=[];
for(const m of ['Coaching.API','Coaching.Application','Coaching.Infrastructure','Coaching.Domain'])for(const e of ['dll','pdb']){const name=m+'.'+e;copyFileSync(resolve(root,'publish-fixed/coaching',name),resolve(pack,name));files.push(name);}
writeFileSync(resolve(pack,'payload.sha256'),files.map(n=>hash(resolve(pack,n))+'  '+n).join('\n')+'\n');
execFileSync('tar',['-czf',resolve(root,'http-validation-fix.tar.gz'),'-C',pack,'.']);
writeFileSync(resolve(root,'http-validation-fix.sha256'),hash(resolve(root,'http-validation-fix.tar.gz'))+'\n');
console.log(hash(resolve(root,'http-validation-fix.tar.gz')));
