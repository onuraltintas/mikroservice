import {readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import {resolve} from 'node:path';
const root=resolve('artifacts/coaching-decision-work-live-20261020');
const files=JSON.parse(readFileSync(resolve(root,'backend-files.json'),'utf8'));
mkdirSync(resolve(root,'baseline/Coaching'),{recursive:true});
for(const project of ['Coaching.API','Coaching.Application','Coaching.Infrastructure','Coaching.Domain','EduPlatform.Shared.Contracts'])
 for(const extension of ['dll','pdb'])copyFileSync('artifacts/coaching-weekly-followup-live-20261020/publish/coaching/'+project+'.'+extension,resolve(root,'baseline/Coaching',project+'.'+extension));
const folder='tools/releases/coaching-decision-work-20261020/SourceCheck';mkdirSync(folder,{recursive:true});
copyFileSync('tools/releases/coaching-weekly-followup-20261020/SourceCheck/SourceCheck.csproj',folder+'/SourceCheck.csproj');
let source=readFileSync('tools/releases/coaching-weekly-followup-20261020/SourceCheck/Program.cs','utf8');
source=source.replace(/var allowed = new HashSet<string>\(StringComparer.Ordinal\) \{[\s\S]*?\};/,'var allowed = new HashSet<string>(StringComparer.Ordinal) {\n'+files.map(x=>' '+JSON.stringify(x)).join(',\n')+'\n};');
source=source.replace('(\"Notification\", \"Notification\", \"services/notification-service\"), ','');
source=source.replace('relative == "services/notification-service/Notification.Application/Consumers/WeeklyReviewNotificationConsumers.cs"','allowed.Contains(relative)');
writeFileSync(folder+'/Program.cs',source);
