import {test,expect} from '@playwright/test';
// The teacher sections are tabs on wide screens and a "Öğretmen bölümleri" select on phones.
async function openSection(page,name){if(test.info().project.name==='mobile')await page.getByRole('combobox',{name:'Öğretmen bölümleri',exact:true}).selectOption({label:name});else await page.getByRole('tab',{name,exact:true}).click();}
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return `${e({alg:'HS256'})}.${e({sub:'student',role:'Student',platform_product:'coaching',exp:Math.floor(Date.now()/1000)+3600})}.fixture`;};
test('restore keeps completed work and outcome IDs and archives the exact active plan',async({page,context})=>{
 const task={id:'kept',title:'Korunan çalışma',plannedDate:'2026-10-19',plannedStartMinute:1080,plannedMinutes:30,isCompleted:true,actualMinutes:17,actualQuestions:0,completedAt:'2026-10-17T12:00:00Z',outcomes:[{outcomeId:'o',code:'TEST.1',name:'Korunan kazanım',allocatedMinutes:30,estimateNumber:2}]};
 let plan={id:'annual',version:11,title:'Korunan yıllık plan',status:'Archived',tasks:[task]},active={id:'test',version:2,status:'Active',title:'Test planı',tasks:[]},writes=[];
 await context.route('**/api/**',async route=>{const path=new URL(route.request().url()).pathname,ok=data=>route.fulfill({json:{success:true,data}});
  if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),expiresInMinutes:60,tokenType:'Bearer'}});
  if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
  if(path.endsWith('/plans/annual/restore')){const body=route.request().postDataJSON();writes.push(body);expect(body).toEqual({expectedVersion:11,expectedActiveRevisionId:'test',expectedActiveRevisionVersion:2});active={...active,status:'Archived'};plan={...plan,status:'Active',version:12};return ok(plan);}
  if(path.endsWith('/plans/annual'))return ok(plan);
  if(path.endsWith('/plans'))return ok({items:new URL(route.request().url()).searchParams.get('status')==='Active'?[active]:[plan,active],totalCount:2,pageNumber:1,pageSize:20});return ok([]);
 });
 await page.goto('http://127.0.0.1:4317/coaching-portal/plan?plan=annual');const restore=page.getByRole('button',{name:'Planı yeniden aktif et',exact:true});await expect(restore).toBeDisabled();
 await page.getByRole('checkbox',{name:'Mevcut aktif planın arşivlenmesini ve bu planın yeniden etkinleştirilmesini onaylıyorum.',exact:true}).check();await restore.click();await expect(page.getByRole('status').filter({hasText:'Plan yeniden etkinleştirildi'})).toBeVisible();expect(writes).toHaveLength(1);expect(plan.tasks[0]).toEqual(task);expect(active.status).toBe('Archived');
 // T4: Planım's Liste tab shows task rows; the completion and outcome details are in the task sheet.
 await page.getByRole('tab',{name:'Liste',exact:true}).click();await page.getByRole('button',{name:'Korunan çalışma, ayrıntıları aç',exact:true}).click();const sheet=page.getByRole('dialog',{name:'Korunan çalışma',exact:true});await expect(sheet.getByText('Tamamlandı · 17 dk',{exact:false})).toBeVisible();await expect(sheet.getByText('TEST.1',{exact:false})).toBeVisible();
});
test('assigned teacher edits school details without changing grade or consent',async({page,context})=>{
 const encode=x=>Buffer.from(JSON.stringify(x)).toString('base64url'),accessToken=`${encode({alg:'HS256'})}.${encode({sub:'teacher',role:'Teacher',platform_product:'coaching',exp:Math.floor(Date.now()/1000)+3600})}.fixture`;
 let profile={version:3,educationStatus:'Student',gradeLevel:8,schoolName:'Eski okul',schoolCity:null,schoolDistrict:null,schoolType:null,classBranch:null,birthDate:null,bio:null,shareProgressWithTeachers:true},writes=[];
 await context.route('**/api/**',async route=>{const path=new URL(route.request().url()).pathname,bare=data=>route.fulfill({json:data});
  if(path.endsWith('/auth/refresh-token'))return bare({accessToken,expiresInMinutes:60,tokenType:'Bearer'});
  if(path.endsWith('/auth/staff-session/products'))return bare([{product:'coaching',roles:['Teacher']}]);
  if(path.endsWith('/teachers/me/students'))return bare({items:[{userId:'student',fullName:'Test Öğrenci',firstName:'Test',lastName:'Öğrenci',gradeLevel:8,assignmentStartDate:'2026-09-01'}],totalCount:1,pageNumber:1,pageSize:25});
  if(path.endsWith('/students/student/profile')){if(route.request().method()==='PUT'){const body=route.request().postDataJSON();writes.push(body);expect(body.expectedVersion).toBe(3);expect(body).not.toHaveProperty('gradeLevel');expect(body).not.toHaveProperty('shareProgressWithTeachers');profile={...profile,...body,version:4};}return bare(profile);}
  if(path.endsWith('/progress'))return bare({totalAssignments:0,submittedAssignments:0,totalExams:0,totalGoals:0,totalSessions:0});
  if(path.endsWith('/history'))return bare({items:[],totalCount:0,pageNumber:1,pageSize:10});return bare({success:true,data:[]});
 });
 await page.goto('http://127.0.0.1:4318/?product=coaching');await openSection(page,'Öğrenciler ve raporlar');await page.getByRole('button',{name:'Test Öğrenci öğrenci raporunu aç',exact:true}).click();await page.getByRole('button',{name:'Öğrenci profili',exact:true}).click();
 const panel=page.getByRole('region',{name:'Öğrencinin Koçluk profili',exact:true});await expect(panel).toContainText('Sınıf: 8');await panel.getByRole('button',{name:'Profili düzenle',exact:true}).click();await panel.getByLabel('Okul adı',{exact:true}).fill('Yeni okul');await panel.getByRole('button',{name:'Profili kaydet',exact:true}).click();await expect(panel.getByRole('status').filter({hasText:'güncellendi'})).toBeVisible();await expect(panel).toContainText('Yeni okul');expect(writes).toHaveLength(1);expect(profile.gradeLevel).toBe(8);expect(profile.shareProgressWithTeachers).toBe(true);
});
