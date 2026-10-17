import {test,expect} from '@playwright/test';
const token=role=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return`${e({alg:'HS256'})}.${e({sub:role==='Teacher'?'teacher':'student',role,platform_product:'coaching',exp:Math.floor(Date.now()/1000)+3600})}.fixture`;};
const calendar=page=>page.getByRole('region',{name:'Kayıtlı çalışma planı takvimi',exact:true});
const task=(id,start,minutes=30,lesson='Matematik')=>({id,title:lesson,plannedDate:'2027-01-04',plannedStartMinute:start,plannedMinutes:minutes,weeklyLessonId:lesson,lessonName:lesson,topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,completedAt:null,outcomes:[{outcomeId:id,code:'TEST.'+id,name:'Korunan kazanım '+id,estimateId:'estimate',estimateNumber:2,allocatedMinutes:minutes,origin:'Catalog'}]});
async function fixture(browser,empty=false){
  let plan=empty?null:{id:'annual',version:4,title:'Takvim taşıma testi',status:'Active',weeklySource:{timeZoneId:'Europe/Istanbul',startDate:'2027-01-01',endDate:'2027-06-01',draftVersion:1,fingerprint:'saved'},tasks:[task('a',1080,15),task('b',1095,45),task('other',1110,30,'Türkçe')]};const writes=[];
  const contexts=[];
  async function context(role){const c=await browser.newContext({viewport:test.info().project.use.viewport,isMobile:test.info().project.use.isMobile,hasTouch:test.info().project.use.hasTouch,timezoneId:'Europe/Istanbul'});contexts.push(c);
    await c.route('**/api/**',async route=>{const req=route.request(),path=new URL(req.url()).pathname,body=req.method()==='GET'?null:req.postDataJSON(),ok=data=>route.fulfill({json:{success:true,data}}),bare=data=>route.fulfill({json:data});
      if(path.endsWith('/auth/refresh-token'))return bare({accessToken:token(role),expiresInMinutes:60,tokenType:'Bearer'});
      if(path.endsWith('/auth/staff-session/products'))return bare([{product:'coaching',roles:[role]}]);
      if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
      if(path.endsWith('/teachers/me/students'))return bare({items:[],totalCount:0,pageNumber:1,pageSize:25});
      if(path.endsWith('/study-overview'))return bare({items:[{studentId:'student',fullName:'Öğrenci Test',hasActivePlan:plan?.status==='Active',hasDraft:plan?.status==='Draft',completedTasks:plan?.tasks.filter(t=>t.isCompleted).length??0,pendingTasks:plan?.tasks.filter(t=>!t.isCompleted).length??0,overdueTasks:0,actualMinutes:plan?.tasks.reduce((s,t)=>s+(t.actualMinutes??0),0)??0}],totalCount:1,pageNumber:1,pageSize:25});
      if(path.endsWith('/plans')&&req.method()==='GET')return ok({items:plan?[plan]:[],totalCount:plan?1:0,pageNumber:1,pageSize:20});
      if(path.endsWith('/plans')&&req.method()==='POST'){writes.push({path,body});plan={id:'annual',version:0,title:body.title,status:'Draft',tasks:body.tasks.map((t,i)=>({...t,id:'new'+i,isCompleted:false,actualMinutes:null,completedAt:null}))};return ok(plan);}
      if(path.endsWith('/plans/annual')&&req.method()==='GET')return ok(plan);
      if(/\/(publish|completion|calendar-move|title)$/.test(path)){
        writes.push({path,body});expect(body.expectedVersion).toBe(plan.version);
        if(path.endsWith('/publish'))plan={...plan,status:'Active',version:plan.version+1};
        else if(path.endsWith('/calendar-move')){const moving=plan.tasks.filter(t=>body.taskIds.includes(t.id)).sort((a,b)=>a.plannedStartMinute-b.plannedStartMinute),delta=body.startMinute-moving[0].plannedStartMinute;plan={...plan,version:plan.version+1,tasks:plan.tasks.map(t=>!body.taskIds.includes(t.id)?t:{...t,plannedDate:body.plannedDate,plannedStartMinute:t.plannedStartMinute+delta})};}
        else {const id=path.split('/').at(-2);plan={...plan,version:plan.version+1,tasks:plan.tasks.map(t=>t.id!==id?t:path.endsWith('/title')?{...t,title:body.title}:{...t,isCompleted:true,actualMinutes:body.actualMinutes,actualQuestions:body.actualQuestions,completedAt:'2027-01-04T17:00:00Z'})};}return ok(plan);
      }
      if(req.method()!=='GET')return route.fulfill({status:500,json:{message:'Unexpected write'}});return ok([]);
    });return c;
  }
  return{context,writes,get:()=>plan,close:async()=>{for(const c of contexts)await c.close();}};
}
async function dragTo(page,card,date,minute){
  const box=await card.boundingBox(),track=await calendar(page).locator(`.day-track[data-date="${date}"]`).boundingBox();
  const offset=5;await page.mouse.move(box.x+box.width/2,box.y+offset);await page.mouse.down();await page.mouse.move(track.x+track.width/2,track.y+minute+offset,{steps:12});await page.mouse.up();
}
test('dragging moves all outcome pieces, rejects overlap, and keeps completed cards locked',async({browser})=>{
  const f=await fixture(browser),context=await f.context('Student'),page=await context.newPage();await page.clock.setFixedTime(new Date('2026-10-17T12:00:00Z'));
  try{
    await page.goto('http://127.0.0.1:4317/coaching-portal/study-plans?plan=annual');await expect(calendar(page)).toBeVisible();await calendar(page).getByRole('button',{name:'Gün',exact:true}).click();
    const card=()=>calendar(page).locator('.timed-block').filter({hasText:'Matematik'});await dragTo(page,card(),'2027-01-04',1200);
    await expect.poll(()=>f.writes.length).toBe(1);expect(f.writes[0].body).toEqual({expectedVersion:4,taskIds:['a','b'],plannedDate:'2027-01-04',startMinute:1200});await expect(card()).toContainText('20:00–21:00');
    expect(f.get().tasks[1].plannedStartMinute).toBe(1215);expect(f.get().tasks[0].outcomes[0].code).toBe('TEST.a');
    await dragTo(page,card(),'2027-01-04',1110);await expect(calendar(page).getByRole('alert')).toContainText('çakış');expect(f.writes).toHaveLength(1);await expect(card()).toContainText('20:00–21:00');
    await calendar(page).getByRole('button',{name:'Gün ve saat değiştir',exact:true}).click();await calendar(page).getByLabel('Yeni gün',{exact:true}).fill('2027-01-05');await calendar(page).getByLabel('Yeni saat',{exact:true}).fill('19:00');await calendar(page).getByRole('button',{name:'Taşımayı kaydet',exact:true}).click();await expect.poll(()=>f.writes.length).toBe(2);
    await calendar(page).getByLabel('Tarihe git',{exact:true}).fill('2027-01-05');await card().click();const first=page.locator('article.task').first();await first.getByLabel('Gerçek süre (dk)',{exact:true}).fill('12');await first.getByLabel('Gerçek soru (isteğe bağlı)',{exact:true}).fill('0');await first.getByRole('button',{name:'Tamamla',exact:true}).click();await expect(first).toContainText('Tamamlandı · 12 dk');
    await expect(card()).toContainText('1/2 tamamlandı');await expect(card()).not.toHaveClass(/moveable/);await page.screenshot({path:`../../artifacts/calendar-move-${test.info().project.name}.png`,fullPage:false});
  }finally{await f.close();}
});
test('teacher creates and corrects a timed plan and sees the students completion in bulk tracking',async({browser})=>{
  const f=await fixture(browser,true),teacher=await f.context('Teacher'),student=await f.context('Student'),page=await teacher.newPage();
  try{
    await page.goto('http://127.0.0.1:4318/?product=coaching');await page.getByTestId('study-tab').click();await page.getByRole('button',{name:'Plan oluştur',exact:true}).click();const editor=page.locator('app-student-study-plans');
    await editor.getByRole('button',{name:'Yeni taslak',exact:true}).click();await editor.getByLabel('Plan başlığı',{exact:true}).fill('Öğretmen çalışma planı');await editor.getByLabel('Çalışma başlığı',{exact:true}).fill('Matematik');await editor.getByLabel('Tarih',{exact:true}).fill('2027-01-04');await editor.getByLabel('Başlangıç saati (isteğe bağlı)',{exact:true}).fill('18:00');await editor.getByRole('button',{name:'Taslağı kaydet',exact:true}).click();await editor.getByRole('checkbox',{name:'Planımı kontrol ettim, yayımlamayı onaylıyorum.',exact:true}).check();await editor.getByRole('button',{name:'Planı yayımla',exact:true}).click();
    await editor.getByLabel('Çalışma başlığını düzelt',{exact:true}).fill('Matematik tekrarı');await editor.getByRole('button',{name:'Başlığı kaydet',exact:true}).click();await expect(editor.getByRole('heading',{name:'Matematik tekrarı',exact:true})).toBeVisible();
    const s=await student.newPage();await s.goto('http://127.0.0.1:4317/coaching-portal/study-plans?plan=annual');await expect(s.getByRole('heading',{name:'Matematik tekrarı',exact:true})).toBeVisible();await s.getByLabel('Gerçek süre (dk)',{exact:true}).fill('12');await s.getByLabel('Gerçek soru (isteğe bağlı)',{exact:true}).fill('0');await s.getByRole('button',{name:'Tamamla',exact:true}).click();await expect(s.getByText('Tamamlandı · 12 dk',{exact:false})).toBeVisible();
    await editor.getByRole('button',{name:'Planı yenile',exact:true}).click();await expect(editor.getByText('Tamamlandı · 12 dk',{exact:false})).toBeVisible();await page.getByRole('button',{name:'Toplu takibe dön',exact:true}).click();await expect(page.getByRole('button',{name:'1 tamamlanan',exact:true})).toBeVisible();expect(f.get().tasks[0].actualQuestions).toBe(0);await page.screenshot({path:`../../artifacts/calendar-teacher-${test.info().project.name}.png`,fullPage:false});
  }finally{await f.close();}
});
