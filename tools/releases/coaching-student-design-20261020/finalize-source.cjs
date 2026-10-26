const fs=require('fs'),path=require('path');
const base='clients/admin-panel/',backup='artifacts/coaching-student-design-20261020/before/';
for(const n of ['coaching-institution-subscription','coaching-teacher-assignment-detail','coaching-teacher-student-profile','coaching-teacher-goals','coaching-teacher-assignments']){
 const f=base+'projects/staff-portal/src/app/features/coaching/'+n+'.component.ts';fs.copyFileSync(backup+f,f);
}
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(x=>x.isDirectory()?walk(path.join(d,x.name)):[path.join(d,x.name)]);}
const files=[...walk(base+'src/app/features/coaching-portal'),...walk(base+'projects/staff-portal/src/app/features/coaching'),base+'shared/coaching-profile.component.ts'].filter(f=>f.endsWith('.ts')&&!f.endsWith('.spec.ts')&&!f.endsWith('coaching-feedback.ts'));
for(const f of files){let s=fs.readFileSync(f,'utf8');if(!s.includes('inject(ToasterService)'))continue;
 const rel=path.relative(path.dirname(f),base+'src/app/features/coaching-portal/coaching-feedback').replaceAll('\\','/');
 s=s.replace(/^import \{ ToasterService \}[^\n]*\n/m,'').replaceAll('inject(ToasterService)','coachingToaster()');
 if(s.includes('import { coachingFeedback }'))s=s.replace('import { coachingFeedback }','import { coachingFeedback, coachingToaster }');else s=`import { coachingToaster } from '${rel.startsWith('.')?rel:'./'+rel}';\n`+s;
 fs.writeFileSync(f,s);
}
const styles=base+'src/styles.scss';let s=fs.readFileSync(styles,'utf8');
if(s.includes('coaching-student-tokens'))throw Error('Unexpected baseline theme imports');
s=s.replace("@use '@angular/material' as mat;","@use '@angular/material' as mat;\n@use './app/features/coaching-portal/layout/coaching-student-tokens';\n@use './app/features/coaching-portal/layout/coaching-student-surfaces';");fs.writeFileSync(styles,s);
const weekly=base+'src/app/features/coaching-portal/pages/student-weekly-calendar.component.ts';s=fs.readFileSync(weekly,'utf8').replace('coachingFeedback(undefined, this.error)','coachingFeedback(this.message, this.error)');fs.writeFileSync(weekly,s);
console.log('Student theme and shared async confirmations applied to existing live frontend sources only. No backend or catalogue changes.');
