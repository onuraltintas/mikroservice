import {cpSync,existsSync,mkdirSync,readFileSync,writeFileSync,symlinkSync,readdirSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=process.cwd(),release=resolve(root,'artifacts/coaching-yks-20261019-r2');
const resume=process.argv[2]==='--resume';
assert.ok(!existsSync(join(release,'source-provenance.json')),'An existing reviewed release must not be overwritten.');
assert.ok(resume||!existsSync(join(release,'backend-source')),'Use explicit resume after a partial preparation.');
mkdirSync(release,{recursive:true});
const filter=path=>!/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(path);
if(!resume){
 cpSync(resolve(root,'artifacts/coaching-clean-start-20261018/backend-source'),join(release,'backend-source'),{recursive:true,filter});
 cpSync(resolve(root,'artifacts/coaching-study-wizard-live-20261017/admin-source/clients/admin-panel'),join(release,'admin-source'),{recursive:true,filter});
 cpSync(resolve(root,'artifacts/coaching-teacher-wizard-live-20261018/frontend-source'),join(release,'staff-source'),{recursive:true,filter});
}
const backend=[
 'Coaching.Domain/Entities/StudyCatalogYksScope.cs','Coaching.Domain/Entities/StudyCatalogOutcome.cs','Coaching.Domain/Entities/StudentLearningContext.cs',
 'Coaching.Infrastructure/Catalogs/OutcomeImportPackage.cs','Coaching.Infrastructure/Catalogs/CoachingOutcomeCatalogService.cs',
 'Coaching.Infrastructure/Catalogs/CoachingYksCatalogImportService.cs','Coaching.Infrastructure/Catalogs/CoachingCurriculumManagementService.cs',
 'Coaching.Infrastructure/Data/Configurations/StudyOutcomeConfigurations.cs','Coaching.Infrastructure/Data/Configurations/StudyCatalogYksScopeConfiguration.cs',
 'Coaching.Infrastructure/StudyPlanning/YksCatalogEligibility.cs','Coaching.Infrastructure/StudyPlanning/CoachingLearningSetupService.cs',
 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs','Coaching.Infrastructure/StudyPlanning/CoachingAutomaticStudyPlanPreviewService.cs',
 'Coaching.API/Controllers/StudyOutcomeCatalogController.cs',
 'Coaching.Infrastructure/Data/Migrations/CoachingDbContextModelSnapshot.cs',
 ...readdirSync(resolve(root,'services/coaching-service/Coaching.Infrastructure/Data/Migrations')).filter(n=>/Yks(CatalogScopes|GeographyOutcomeExclusions)/.test(n)).map(n=>'Coaching.Infrastructure/Data/Migrations/'+n)
];
const frontend=['src/app/features/coaching-portal/pages/student-learning-setup.component.html',
 'src/app/core/services/coaching-study-planning.service.ts',
 'src/app/features/dashboard/pages/coaching-curriculum-manager.component.ts','src/app/features/dashboard/pages/coaching-curriculum-manager.component.html',
 'src/app/features/dashboard/pages/coaching-outcomes.component.ts'];
const sha=file=>createHash('sha256').update(readFileSync(file)).digest('hex');const changes=[];
for(const file of backend){const target=join(release,'backend-source/services/coaching-service',file),baseline=resolve(root,'artifacts/coaching-clean-start-20261018/backend-source/services/coaching-service',file),source=resolve(root,'services/coaching-service',file);mkdirSync(resolve(target,'..'),{recursive:true});if(resume&&existsSync(target))assert.ok(sha(target)===sha(source)||existsSync(baseline)&&sha(target)===sha(baseline),'Unknown prepared backend modification: '+file);changes.push({surface:'backend',file,before:existsSync(baseline)?sha(baseline):null,after:sha(source)});cpSync(source,target);}
for(const surface of ['admin-source','staff-source']){
 const baselineRoot=resolve(root,surface==='admin-source'?'artifacts/coaching-study-wizard-live-20261017/admin-source/clients/admin-panel':'artifacts/coaching-teacher-wizard-live-20261018/frontend-source');
 for(const file of frontend){const target=join(release,surface,file),baseline=join(baselineRoot,file),source=resolve(root,'clients/admin-panel',file);mkdirSync(resolve(target,'..'),{recursive:true});if(resume&&existsSync(target))assert.ok(sha(target)===sha(source)||existsSync(baseline)&&sha(target)===sha(baseline),'Unknown prepared frontend modification: '+file);changes.push({surface,file,before:existsSync(baseline)?sha(baseline):null,after:sha(source)});cpSync(source,target);}
 if(!existsSync(join(release,surface,'node_modules')))symlinkSync(resolve(root,'clients/admin-panel/node_modules'),join(release,surface,'node_modules'),'junction');
}
writeFileSync(join(release,'source-provenance.json'),JSON.stringify({baselineBackend:'coaching-clean-start-20261018',baselineAdmin:'coaching-study-wizard-live-20261017',baselineStaff:'coaching-teacher-wizard-live-20261018',changes},null,2));
writeFileSync(join(release,'expected-base-dlls.sha256'),['Coaching.Domain','Coaching.Application','Coaching.Infrastructure','Coaching.API'].map(n=>sha(resolve(root,'artifacts/coaching-clean-start-20261018/publish',n+'.dll'))+'  '+n+'.dll').join('\n')+'\n');
console.log(JSON.stringify({release,backendFiles:backend.length,frontendFiles:frontend.length}));
