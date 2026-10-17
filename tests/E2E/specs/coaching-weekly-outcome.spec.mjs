import {test,expect} from '@playwright/test';
const math='33333333-3333-4333-8333-333333333331',turkish='33333333-3333-4333-8333-333333333332';
const lessons=[{id:math,name:'Matematik',examCode:'LGS',gradeNumber:8},{id:turkish,name:'Türkçe',examCode:'LGS',gradeNumber:8}];
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return`${e({alg:'HS256'})}.${e({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'Student',platform_product:'coaching',given_name:'Test',family_name:'Öğrenci',exp:Math.floor(Date.now()/1000)+900})}.fixture`;};
async function fixture(context){
  const writes=[];let conflict=false,missing=false,generated=null;
  const old={id:'old-plan',version:7,title:'Mevcut çalışma planım',status:'Active',tasks:[{id:'old-work',title:'Önceki çalışmam',plannedDate:'2026-10-16',plannedMinutes:30,isCompleted:true,isPinned:false,actualMinutes:25,actualQuestions:0}]};
  const draft={version:2,timeZoneId:'Europe/Istanbul',slots:[{id:'one',lessonId:math,lessonName:'Matematik',examCode:'LGS',day:'Monday',startMinute:1080,durationMinutes:30},{id:'two',lessonId:turkish,lessonName:'Türkçe',examCode:'LGS',day:'Tuesday',startMinute:1110,durationMinutes:30}]};
  function preview(request){
    const extended=request.endDate>='2027-06-07',complete=extended||request.reuseFreedSlots;
    const task=(lessonId,date,start,reusedSlot=false)=>({date,startMinute:start,lessonId,lessonName:lessons.find(x=>x.id===lessonId).name,minutes:30,reusedSlot,outcome:{topicId:lessonId===math?'math-topic':'turkish-topic',topicName:lessonId===math?'Çarpanlar ve Katlar':'Sözcükte Anlam',outcomeId:lessonId===math?'math-outcome':'turkish-outcome',code:lessonId===math?'M.8.1.1.1':'T.TEST.1',name:lessonId===math?'Pozitif tam sayıların çarpanlarını bulur.':'Sözcük anlamını bağlamdan çıkarır (örnek).',estimateId:'estimate',estimateNumber:1,minutes:lessonId===math?90:30}});
    const tasks=[task(math,'2027-05-24',1080),task(turkish,'2027-05-25',1110),task(math,'2027-05-31',1080)];
    if(complete)tasks.push(task(math,extended?'2027-06-07':'2027-06-01',extended?1080:1110,!extended));
    return{request,timeZoneId:'Europe/Istanbul',fingerprint:'a'.repeat(64),canCreate:complete&&!missing,protectedTaskCount:0,issues:missing?[{lessonId:turkish,lessonName:'Türkçe',message:'Onaylı kazanım çalışma süresi eksik.'}]:[],schedule:{tasks,freeMinutes:complete?0:30,recommendedEndDate:complete?tasks.at(-1).date:'2027-06-07',lessons:[{lessonId:math,lessonName:'Matematik',weeklyMinutes:30,outcomeCount:3,requiredMinutes:90,scheduledMinutes:complete?90:60,remainingMinutes:complete?0:30,finishDate:complete?tasks.at(-1).date:'2027-06-07',extraWeeklyMinutes:complete?0:30},{lessonId:turkish,lessonName:'Türkçe',weeklyMinutes:30,outcomeCount:1,requiredMinutes:30,scheduledMinutes:30,remainingMinutes:0,finishDate:'2027-05-25',extraWeeklyMinutes:0}]}};
  }
  await context.route('**/api/**',async route=>{
    const req=route.request(),path=new URL(req.url()).pathname;const ok=data=>route.fulfill({json:{success:true,data}});
    if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),expiresInMinutes:15,tokenType:'Bearer'}});
    if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
    if(path.endsWith('/weekly-draft')){expect(req.method()).toBe('GET');return ok({draft,lessons});}
    if(path.endsWith('/weekly-outcome-plans/options'))return ok({startDate:'2027-05-24',endDate:'2027-06-01',weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[{name:'LGS',date:'2027-06-20'}]});
    if(path.endsWith('/weekly-outcome-plans/preview'))return ok(preview(req.postDataJSON()));
    if(path.endsWith('/weekly-outcome-plans/drafts')){
      const body=req.postDataJSON();writes.push({path,body});if(conflict)return route.fulfill({status:409,json:{message:'Haftalık taslak veya kazanım süreleri değişti. Yeniden önizleyin.'}});
      expect(body.fingerprint).toBe('a'.repeat(64));expect(preview(body.preview).canCreate).toBe(true);
      generated={id:'generated',version:0,title:body.title,status:'Draft',weeklySource:{draftVersion:2,learningContextVersion:0,...body.preview,timeZoneId:'Europe/Istanbul'},tasks:preview(body.preview).schedule.tasks.map((x,i)=>({id:'new-'+i,title:x.outcome.topicName,plannedDate:x.date,plannedMinutes:x.minutes,plannedStartMinute:x.startMinute,lessonName:x.lessonName,topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,outcomes:[{outcomeId:'o',code:x.outcome.code,name:x.outcome.name,estimateNumber:1,allocatedMinutes:30,origin:'Catalog'}]}))};return ok({id:generated.id,version:0});
    }
    if(path.endsWith('/plans/generated'))return ok(generated);
    if(path.endsWith('/plans/old-plan'))return ok(old);
    if(path.endsWith('/plans'))return ok({items:[old,...(generated?[generated]:[])],totalCount:generated?2:1,pageNumber:1,pageSize:20});
    if(req.method()!=='GET')writes.push({path});return ok([]);
  });return{writes,old,setMissing:value=>missing=value,setConflict:value=>conflict=value};
}
async function open(page){await page.goto('/coaching-portal/weekly-plan');await page.getByTestId('weekly-preview-link').click();await expect(page.getByRole('heading',{name:'Çalışma planını önizle',exact:true})).toBeVisible();await expect(page.getByLabel('Hedef bitiş tarihi',{exact:true})).toHaveValue('2027-06-01');await page.getByRole('button',{name:'Önizleme oluştur',exact:true}).click();await expect(page.getByRole('heading',{name:'Dersler ne zaman bitecek?',exact:true})).toBeVisible();}
async function create(page){await page.getByRole('checkbox',{name:'Tarihleri, ders bitişlerini ve süreleri kontrol ettim.',exact:true}).check();await page.getByRole('button',{name:'Çalışma planı taslağını oluştur',exact:true}).click();await expect(page.getByRole('heading',{name:'Haftalık derslerimden çalışma planım',exact:true})).toBeVisible();}

test('weekly draft previews course finish dates and requires choosing extension before creating a separate plan',async({context,page})=>{
  const f=await fixture(context);await open(page);await expect(page.getByText('07.06.2027',{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Çalışma planı taslağını oluştur',exact:true})).toBeDisabled();expect(f.writes).toHaveLength(0);
  await page.getByRole('heading',{name:'Nasıl devam etmek istersin?',exact:true}).evaluate(el=>el.scrollIntoView({block:'start'}));await page.screenshot({path:`../../artifacts/weekly-outcome-decisions-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('button',{name:'07.06.2027 tarihine uzat ve önizle',exact:true}).click();await expect(page.getByLabel('Hedef bitiş tarihi',{exact:true})).toHaveValue('2027-06-07');await expect(page.getByRole('heading',{name:'Nasıl devam etmek istersin?',exact:true})).toHaveCount(0);
  await page.getByRole('heading',{name:'Dersler ne zaman bitecek?',exact:true}).evaluate(el=>el.scrollIntoView({block:'start'}));await page.screenshot({path:`../../artifacts/weekly-outcome-preview-${test.info().project.name}.png`,fullPage:false});
  await create(page);await expect(page.getByText('18:00–18:30',{exact:false}).first()).toBeVisible();expect(f.old.version).toBe(7);expect(f.writes).toHaveLength(1);expect(f.writes[0].body.preview.endDate).toBe('2027-06-07');await expect(page.locator('input[name="planTitle"]')).toHaveCount(0);
});
test('early-finished lesson hours can be reused without extending the deadline',async({context,page})=>{
  const f=await fixture(context);await open(page);await page.getByRole('button',{name:'Erken biten derslerin saatlerini kullan',exact:true}).click();await expect(page.getByLabel('Hedef bitiş tarihi',{exact:true})).toHaveValue('2027-06-01');await expect(page.getByText('Boşalan ders saatinden',{exact:true})).toBeVisible();await create(page);expect(f.writes[0].body.preview.reuseFreedSlots).toBe(true);expect(f.writes[0].body.preview.endDate).toBe('2027-06-01');
});
test('missing curriculum blocks creating and a save conflict retains the preview',async({context,page})=>{
  const f=await fixture(context);f.setMissing(true);await open(page);await page.getByRole('button',{name:'Erken biten derslerin saatlerini kullan',exact:true}).click();await expect(page.getByText('Onaylı kazanım çalışma süresi eksik.',{exact:false})).toBeVisible();await expect(page.getByRole('checkbox',{name:'Tarihleri, ders bitişlerini ve süreleri kontrol ettim.',exact:true})).toBeDisabled();
  f.setMissing(false);await page.getByRole('button',{name:'Önizleme oluştur',exact:true}).click();await page.getByRole('checkbox',{name:'Tarihleri, ders bitişlerini ve süreleri kontrol ettim.',exact:true}).check();f.setConflict(true);await page.getByRole('button',{name:'Çalışma planı taslağını oluştur',exact:true}).click();await expect(page.getByRole('alert')).toContainText('değişti');await expect(page.getByRole('heading',{name:'Dersler ne zaman bitecek?',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Çalışma planı taslağını oluştur',exact:true})).toBeDisabled();
});
test('choosing more weekly hours returns to saved calendar without changing plans or hours',async({context,page})=>{
  const f=await fixture(context);await open(page);await page.getByRole('link',{name:'Haftalık ders saatimi artır →',exact:true}).click();await expect(page.getByRole('heading',{name:'Haftalık programım',exact:true})).toBeVisible();expect(f.writes).toHaveLength(0);expect(f.old.version).toBe(7);
});
