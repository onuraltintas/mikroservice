import {cpSync,copyFileSync,mkdirSync,readFileSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
const root=resolve('artifacts/coaching-decision-work-live-20261020'),source=resolve(root,'source');
if(existsSync(source))throw Error('Isolated source already exists.');
const base=resolve('artifacts/coaching-weekly-followup-live-20261020/source');
const filter=path=>!/[\\/](bin|obj|node_modules|dist|\.angular)([\\/]|$)/.test(path)&&!/[\\/]\.env(?:\.|$)/.test(path);
for(const path of ['shared','services/coaching-service'])cpSync(resolve(base,path),resolve(source,path),{recursive:true,filter});
const prefix='services/coaching-service/';
const files=[
 'Coaching.API/Controllers/WeeklyReviewController.cs',
 'Coaching.Application/StudyPlanning/WeeklyReviewModels.cs','Coaching.Application/StudyPlanning/WeeklyDecisionWorkModels.cs',
 'Coaching.Domain/Entities/WeeklyReview.cs',
 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyReviewService.cs','Coaching.Infrastructure/StudyPlanning/CoachingWeeklyDecisionWork.cs',
 'Coaching.Infrastructure/Data/Configurations/WeeklyReviewConfigurations.cs',
 'Coaching.Infrastructure/Data/Migrations/20261020143601_WeeklyDecisionWorkLinks.cs',
 'Coaching.Infrastructure/Data/Migrations/20261020143601_WeeklyDecisionWorkLinks.Designer.cs',
 'Coaching.Infrastructure/Data/Migrations/CoachingDbContextModelSnapshot.cs',
 'Coaching.Infrastructure/Privacy/CoachingStudyPlanErasureMessages.cs',
 'Coaching.Infrastructure/Privacy/CoachingErasureAssessmentService.cs','Coaching.Infrastructure/Privacy/CoachingErasureExecutionService.cs'
].map(x=>prefix+x);
for(const path of files){mkdirSync(dirname(resolve(source,path)),{recursive:true});copyFileSync(path,resolve(source,path));}
writeFileSync(resolve(root,'backend-files.json'),JSON.stringify(files,null,2));
console.log('Isolated live Coaching source prepared with '+files.length+' decision-work files; shared sources unchanged.');
