import {test,expect} from '@playwright/test';
import {studentShell} from '../support/coaching-student-shell.mjs';
// Paket H4: the yearly preview is the plan wizard's Özet step. The old preview address (and the weekly calendar's link to it) open that
// step; the plan is created from the checked preview and published in one go.
const math='33333333-3333-4333-8333-333333333331',turkish='33333333-3333-4333-8333-333333333332';
const lessons=[{id:math,name:'Matematik',examCode:'LGS',gradeNumber:8},{id:turkish,name:'Türkçe',examCode:'LGS',gradeNumber:8}];
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return`${e({alg:'HS256'})}.${e({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'Student',platform_product:'coaching',given_name:'Test',family_name:'Öğrenci',exp:Math.floor(Date.now()/1000)+900})}.fixture`;};
const context={id:'context',version:4,academicYearStart:2026,purpose:'Lgs',targetExamYear:2027,primaryScoreType:null,foreignLanguage:null,curriculumVersionId:'cur',startingStatusReviewed:true,topicStates:[],
  coursePreferences:[{lessonId:math,priority:3,selfReportedDifficulty:null},{lessonId:turkish,priority:3,selfReportedDifficulty:4}]};
const tempos=[{tempo:'Relaxed',finishBy:'2027-06-19',neededWeeklyMinutes:60,capacityWeeklyMinutes:120,fits:true},{tempo:'Balanced',finishBy:'2027-05-20',neededWeeklyMinutes:90,capacityWeeklyMinutes:120,fits:true},
  {tempo:'Intense',finishBy:'2027-04-15',neededWeeklyMinutes:150,capacityWeeklyMinutes:120,fits:false}];
async function fixture(page,{active=false,routine=null}={}){
  const writes=[],previews=[],proposals=[];let missing=false,conflict=null,generated=null,draftVersion=2;
  const old={id:'old-plan',version:7,title:'Mevcut çalışma planım',status:'Active',tasks:[{id:'old-work',title:'Önceki çalışmam',plannedDate:'2026-10-16',plannedMinutes:30,isCompleted:true,isPinned:false,actualMinutes:25,actualQuestions:0}]};
  let slots=[{id:'one',lessonId:math,lessonName:'Matematik',examCode:'LGS',day:'Monday',startMinute:1080,durationMinutes:60}];
  const proposed={id:'p1',lessonId:turkish,lessonName:'Türkçe',examCode:'LGS',day:'Tuesday',startMinute:1080,durationMinutes:60};
  function preview(request){
    const task=(lessonId,date,kind='Learn')=>({date,startMinute:1080,lessonId,lessonName:lessons.find(x=>x.id===lessonId).name,minutes:60,reusedSlot:false,kind,reason:null,
      outcome:{topicId:lessonId===math?'math-topic':'turkish-topic',topicName:lessonId===math?'Çarpanlar ve Katlar':'Sözcükte Anlam',outcomeId:'o',code:'M.8.1.1.1',name:'Çarpanları bulur.',estimateId:'e',estimateNumber:1,minutes:60}});
    const focus=(request.focusTopicIds??[]).length>0;
    return{request,timeZoneId:'Europe/Istanbul',fingerprint:'a'.repeat(64),canCreate:!missing,protectedTaskCount:0,issues:missing?[{lessonId:turkish,lessonName:'Türkçe',message:'Onaylı kazanım çalışma süresi eksik.'}]:[],
      leftOutTopics:request.prioritizeWhenShort?[]:[{lessonId:turkish,lessonName:'Türkçe',topicId:'paragraf',topicName:'Paragraf',minutes:120}],
      schedule:{tasks:[task(math,'2026-11-16'),task(turkish,'2026-11-17'),task(math,'2026-11-23','Spaced')],freeMinutes:0,recommendedEndDate:'2027-05-01',
        lessons:[{lessonId:math,lessonName:'Matematik',weeklyMinutes:60,outcomeCount:3,requiredMinutes:600,scheduledMinutes:600,remainingMinutes:0,finishDate:'2027-03-01',extraWeeklyMinutes:0},
          {lessonId:turkish,lessonName:'Türkçe',weeklyMinutes:60,outcomeCount:3,requiredMinutes:900,scheduledMinutes:focus||request.prioritizeWhenShort?900:780,remainingMinutes:focus||request.prioritizeWhenShort?0:120,
            finishDate:focus||request.prioritizeWhenShort?'2027-05-01':null,extraWeeklyMinutes:30}]}};
  }
  await page.context().route('**/api/**',async route=>{
    const req=route.request(),url=new URL(req.url()),path=url.pathname;const ok=data=>route.fulfill({json:{success:true,data}});
    if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),expiresInMinutes:15,tokenType:'Bearer'}});
    const shell=studentShell(route,path);if(shell)return shell;
    if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
    if(path.endsWith('/study-planning/setup')){if(req.method()==='PUT')writes.push({path,body:req.postDataJSON()});}
    if(path.endsWith('/study-planning/setup'))return ok({context,nextStep:'Preview',isReady:true,missingFields:[],warnings:[]});
    if(path.endsWith('/setup/catalog'))return ok({curriculum:null,lessons:[],missingDurationCount:0,warnings:[],topics:{totalCount:2,pageNumber:1,pageSize:50,items:[
      {id:'paragraf',lessonId:turkish,name:'Paragraf',lessonName:'Türkçe',unitName:'Okuma',gradeNumber:8,examCode:'LGS',estimatedMinutes:120},
      {id:'fiil',lessonId:turkish,name:'Fiilimsiler',lessonName:'Türkçe',unitName:'Dil bilgisi',gradeNumber:8,examCode:'LGS',estimatedMinutes:60}]}});
    if(path.endsWith('/routine'))return routine?ok(routine):route.fulfill({status:404,json:{success:false,code:'StudyPlanning.RoutineMissing'}});
    if(path.endsWith('/availability'))return ok({version:3,timeZoneId:'Europe/Istanbul',windows:[{day:'Monday',startMinute:1080,endMinute:1200},{day:'Tuesday',startMinute:1080,endMinute:1200}]});
    if(path.endsWith('/weekly-draft/tempo-options'))return ok({planEnd:'2027-06-19',options:tempos});
    if(path.endsWith('/weekly-draft/proposal')){const body=req.postDataJSON();proposals.push(body);
      const fill=body.mode==='Fill';return ok({mode:body.mode,examFocus:null,slots:[proposed],lessons:[{lessonId:math,lessonName:'Matematik',examCode:'LGS',existingMinutes:fill?60:0,proposedMinutes:0,neededMinutes:60,noTime:false},
        {lessonId:turkish,lessonName:'Türkçe',examCode:'LGS',existingMinutes:0,proposedMinutes:60,neededMinutes:90,noTime:false}],freeMinutes:120,neededMinutes:150,shortageMinutes:30,coveredMinutes:120,
        maxDayMinutes:480,weeksUntilEnd:30,endDate:'2027-06-19',middleSchool:true,draftVersion:draftVersion,finishBy:body.finishBy??null});}
    if(path.endsWith('/weekly-draft')){
      if(req.method()==='PUT'){const body=req.postDataJSON();writes.push({path,body});draftVersion++;slots=body.slots.map(x=>({...x,lessonName:lessons.find(l=>l.id===x.lessonId).name,examCode:'LGS'}));
        return ok({version:draftVersion,timeZoneId:'Europe/Istanbul',slots});}
      return ok({draft:{version:draftVersion,timeZoneId:'Europe/Istanbul',slots},lessons,maxDayMinutes:480});}
    if(path.endsWith('/weekly-outcome-plans/options'))return ok({startDate:'2026-11-09',endDate:'2027-06-19',weeklyDraftVersion:draftVersion,timeZoneId:'Europe/Istanbul',exams:[{name:'LGS',date:'2027-06-20'}]});
    if(path.endsWith('/weekly-outcome-plans/preview')){const body=req.postDataJSON();previews.push(body);return ok(preview(body));}
    if(path.endsWith('/weekly-outcome-plans/drafts')){
      const body=req.postDataJSON();writes.push({path,body});if(conflict)return route.fulfill({status:409,json:{message:conflict}});
      expect(body.fingerprint).toBe('a'.repeat(64));
      generated={id:'generated',version:0,title:body.title,status:'Draft',weeklySource:{draftVersion,learningContextVersion:4,...body.preview,timeZoneId:'Europe/Istanbul'},
        tasks:preview(body.preview).schedule.tasks.map((x,i)=>({id:'new-'+i,title:x.outcome.topicName,plannedDate:x.date,plannedMinutes:x.minutes,plannedStartMinute:x.startMinute,lessonName:x.lessonName,topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,outcomes:[]}))};
      return ok({id:generated.id,version:0});
    }
    if(path.endsWith('/plans/generated/publish')){const body=req.postDataJSON();writes.push({path,body});generated={...generated,status:'Active',version:1};return ok(generated);}
    if(path.endsWith('/plans/generated'))return ok(generated);
    if(path.endsWith('/plans/old-plan'))return ok(old);
    if(path.endsWith('/plans')){const items=[...(active?[old]:[]),...(generated?[generated]:[])].filter(x=>!url.searchParams.get('status')||x.status===url.searchParams.get('status'));
      return ok({items,totalCount:items.length,pageNumber:1,pageSize:20});}
    if(req.method()!=='GET')writes.push({path});return ok([]);
  });
  return{writes,previews,proposals,setMissing:v=>missing=v,setConflict:v=>conflict=v};
}
const heading=(page,name)=>page.getByRole('heading',{name,exact:true});

test('the old preview address opens the Özet step; the week, each course and the fixes lead to a published plan',async({page})=>{
  const f=await fixture(page,{active:true});
  // The weekly calendar's footer link points at the old address.
  await page.goto('/coaching-portal/plan/preview');
  await expect(page).toHaveURL(/\/coaching-portal\/plan\/wizard\?adim=ozet$/);
  await expect(heading(page,'Özet')).toBeVisible();
  await expect(page.getByTestId('wizard-progress')).toHaveText('Adım 4 / 4');
  // The draft has cards: Fill mode, ending on the first (relaxed) tempo's date.
  expect(f.proposals[0]).toEqual({mode:'Fill',examFocus:null,finishBy:'2027-06-19'});
  await expect(page.getByTestId('proposal-day')).toHaveText([/Pazartesi\s*18:00–19:00 · Matematik/,/Salı\s*18:00–19:00 · Türkçe\s*Yeni/]);
  await expect(page.getByTestId('shortage-notice')).toContainText('haftada yaklaşık 2,5 saat gerekiyor');
  await page.screenshot({path:`../../artifacts/weekly-outcome-week-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('button',{name:'Bu haftayla devam et',exact:true}).click();
  await expect(page.getByTestId('course-finish')).toBeVisible();
  expect(f.writes[0].body.slots.map(x=>x.id)).toEqual(['one','p1']);
  expect(f.previews[0]).toEqual({startDate:'2026-11-09',endDate:'2027-06-19',expectedWeeklyDraftVersion:3,reuseFreedSlots:true,engineVersion:2,prioritizeWhenShort:false});
  const rows=page.getByTestId('course-finish').locator('li');
  await expect(rows.nth(0)).toContainText('Bitiş: 1 Mart');await expect(rows.nth(0)).toContainText('1 aralıklı tekrar');
  await expect(rows.nth(1)).toContainText('yetişmiyor');await expect(rows.nth(1).getByTestId('left-out')).toHaveText('Dışarıda kalan konular: 1 (Paragraf)');
  await page.screenshot({path:`../../artifacts/weekly-outcome-summary-${test.info().project.name}.png`,fullPage:false});
  // "Şu konuları öne al": Türkçe's Paragraf goes first; the plan is recomputed before it can be published.
  await page.getByRole('button',{name:'Konu seç',exact:true}).click();
  await page.getByRole('combobox',{name:'Ders',exact:true}).selectOption({label:'Türkçe'});
  await page.getByRole('searchbox',{name:'Konu ara',exact:true}).fill('para');
  await page.getByRole('button',{name:'Paragraf konusunu öne al',exact:true}).click();
  await expect(page.getByRole('list',{name:'Öne alınacak konular'})).toContainText('Türkçe · Paragraf');
  await expect(page.getByRole('button',{name:'Planımı yayımla',exact:true})).toBeDisabled();
  await page.getByRole('button',{name:'Bu konularla yeniden hesapla',exact:true}).click();
  await expect(rows.nth(1)).toContainText('Bitiş: 1 Mayıs');
  expect(f.previews.at(-1).focusTopicIds).toEqual(['paragraf']);
  await page.getByRole('button',{name:'Planımı yayımla',exact:true}).click();
  const dialog=page.getByRole('dialog');
  await expect(dialog).toContainText('Yayımlarsan bugünden sonraki çalışmaların bu plana göre değişir. Tamamladığın çalışmalar korunur.');
  await dialog.getByRole('button',{name:'Yayımla',exact:true}).click();
  await expect(page).toHaveURL(/\/coaching-portal\/plan\?plan=generated$/);
  const create=f.writes.find(x=>x.path.endsWith('/drafts')),publish=f.writes.find(x=>x.path.endsWith('/publish'));
  expect(create.body.preview.focusTopicIds).toEqual(['paragraf']);expect(publish.body).toEqual({expectedVersion:0});
});

test('a new student goes through every step: hours, courses, tempo and summary',async({page})=>{
  const f=await fixture(page);
  await page.goto('/coaching-portal/plan/wizard');
  await expect(heading(page,'Saatlerin')).toBeVisible();await expect(page.getByTestId('wizard-progress')).toHaveText('Adım 1 / 4');
  await page.screenshot({path:`../../artifacts/weekly-outcome-step1-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('button',{name:'Bu saatlerle devam',exact:true}).click();
  await expect(heading(page,'Derslerin')).toBeVisible();
  await page.screenshot({path:`../../artifacts/weekly-outcome-step2-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('button',{name:'Devam',exact:true}).click();
  await expect(heading(page,'Tempo')).toBeVisible();
  await expect(page.getByTestId('tempo-option').nth(2)).toContainText('Saatlerin buna yetmiyor');
  await page.screenshot({path:`../../artifacts/weekly-outcome-step3-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('radio',{name:/Dengeli/}).check();
  await page.getByRole('button',{name:'Devam',exact:true}).click();
  await expect(heading(page,'Özet')).toBeVisible();
  await expect.poll(()=>f.proposals.at(-1)?.finishBy).toBe('2027-05-20');
  // "Önemli konulara odaklan" after the week is kept.
  await page.getByRole('button',{name:'Bu haftayla devam et',exact:true}).click();
  await page.getByRole('button',{name:'Önemli konulara odaklan',exact:true}).click();
  await expect(page.getByTestId('course-finish').locator('li').nth(1)).toContainText('Bitiş: 1 Mayıs');
  expect(f.previews.at(-1)).toMatchObject({endDate:'2027-05-20',prioritizeWhenShort:true});
  await page.getByRole('button',{name:'Geri',exact:true}).click();
  await expect(heading(page,'Tempo')).toBeVisible();
});

test('missing durations block publishing; an existing draft and changed sources are explained',async({page})=>{
  const f=await fixture(page,{active:true});f.setMissing(true);
  await page.goto('/coaching-portal/plan/preview');
  await page.getByRole('button',{name:'Bu haftayla devam et',exact:true}).click();
  await expect(page.getByTestId('plan-issues')).toContainText('Onaylı kazanım çalışma süresi eksik.');
  await expect(page.getByRole('button',{name:'Planımı yayımla',exact:true})).toBeDisabled();
  f.setMissing(false);f.setConflict('Önce mevcut plan taslağınızı yayımlayın veya arşivleyin.');
  await page.getByRole('button',{name:'Önemli konulara odaklan',exact:true}).click();
  await page.getByRole('button',{name:'Planımı yayımla',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Yayımla',exact:true}).click();
  await expect(page.getByTestId('publish-issue')).toContainText('Bekleyen bir plan taslağın var.');
  await expect(page.getByTestId('publish-issue').getByRole('link',{name:"Planım'ı aç",exact:true})).toHaveAttribute('href','/coaching-portal/plan');
  f.setConflict('Haftalık taslak, kazanımlar, süreler veya mevcut çalışmalar değişti. Yeniden önizleyin.');
  await page.getByRole('button',{name:'Planımı yayımla',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Yayımla',exact:true}).click();
  await expect(page.getByTestId('publish-issue')).toContainText('değişti');
  await expect(page.getByTestId('publish-issue').getByRole('button',{name:'Yeniden hesapla',exact:true})).toBeVisible();
  expect(f.writes.filter(x=>x.path.endsWith('/publish'))).toHaveLength(0);
});

test('a student with an active plan chooses what to change; "Derslerim" opens the courses and then the summary',async({page})=>{
  const f=await fixture(page,{active:true});
  await page.goto('/coaching-portal/plan/wizard');
  await expect(heading(page,'Planında neyi değiştirmek istiyorsun?')).toBeVisible();
  await page.screenshot({path:`../../artifacts/weekly-outcome-edit-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('button',{name:/^Derslerim/}).click();
  await expect(page.getByTestId('wizard-progress')).toHaveText('Adım 1 / 2');
  await page.getByRole('radio',{name:'Yüksek',exact:true}).first().check();
  await page.getByRole('button',{name:'Devam',exact:true}).click();
  await expect(heading(page,'Özet')).toBeVisible();
  const save=f.writes.find(x=>x.path.endsWith('/setup'));
  expect(save.body.courses[0]).toEqual({lessonId:math,priority:1,selfReportedDifficulty:null});expect(save.body.expectedVersion).toBe(4);
});
