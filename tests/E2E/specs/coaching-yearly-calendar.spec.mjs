import {test,expect} from '@playwright/test';
test.beforeEach(async({page})=>{await page.clock.setFixedTime(new Date('2026-10-17T12:00:00Z'));});
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return`${e({alg:'HS256'})}.${e({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'Student',platform_product:'coaching',exp:Math.floor(Date.now()/1000)+900})}.fixture`;};
async function fixture(context,status='Draft'){
  const task=(id,date,start,minutes,lesson='Matematik')=>({id,title:'Konu '+id,plannedDate:date,plannedStartMinute:start,plannedMinutes:minutes,weeklyLessonId:lesson,lessonName:lesson,topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,actualQuestions:null,completedAt:null,outcomes:[{outcomeId:id,code:'TEST.'+id,name:'Örnek kazanım '+id,estimateId:'estimate',estimateNumber:2,allocatedMinutes:minutes,origin:'Catalog'}]});
  let annual={id:'annual',version:4,title:'Yıllık kazanım planım',status,weeklySource:{draftVersion:1,learningContextVersion:2,startDate:'2027-01-01',endDate:'2027-06-01',reuseFreedSlots:false,fingerprint:'saved',timeZoneId:'Europe/Istanbul'},tasks:[task('a','2027-01-04',1080,15),task('b','2027-01-04',1095,45),task('overlap','2027-01-04',1110,30,'Türkçe'),task('c','2027-01-06',1200,30,'Türkçe'),task('d','2027-03-08',570,30),task('last','2027-06-01',1140,30)]};
  const old={id:'old',version:7,title:'Eski çalışma planım',status:'Active',tasks:[{id:'old-work',title:'Tamamlanan eski çalışma',plannedDate:'2026-10-16',plannedMinutes:30,topicId:null,isPinned:false,isCompleted:true,actualMinutes:25,actualQuestions:0,completedAt:'2026-10-16T10:00:00Z'}]},writes=[];
  await context.route('**/api/**',async route=>{
    const req=route.request(),path=new URL(req.url()).pathname,ok=data=>route.fulfill({json:{success:true,data}});
    if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),expiresInMinutes:15,tokenType:'Bearer'}});
    if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
    if(path.endsWith('/plans/annual'))return ok(annual);
    if(path.endsWith('/plans/old'))return ok(old);
    if(path.endsWith('/plans'))return ok({items:[annual,old],totalCount:2,pageNumber:1,pageSize:20});
    if(/\/tasks\/[^/]+\/(completion|schedule)$/.test(path)){
      const body=req.postDataJSON(),id=path.split('/').at(-2);writes.push({path,body});expect(body.expectedVersion).toBe(annual.version);
      annual={...annual,version:annual.version+1,tasks:annual.tasks.map(t=>t.id!==id?t:path.endsWith('/completion')?{...t,isCompleted:true,actualMinutes:body.actualMinutes,actualQuestions:body.actualQuestions,completedAt:'2027-01-04T17:00:00Z'}:{...t,plannedDate:body.plannedDate})};return ok(annual);
    }
    if(req.method()!=='GET'){writes.push({path});return route.fulfill({status:500,json:{message:'Unexpected calendar write'}});}return ok([]);
  });return{writes,old,getAnnual:()=>annual};
}
const calendar=page=>page.getByRole('region',{name:'Kayıtlı çalışma planı takvimi',exact:true});
async function open(page){await page.goto('/coaching-portal/study-plans?plan=annual');await expect(calendar(page)).toBeVisible();}

test('yearly calendar focuses occupied hours, keeps overlapping lessons visible and opens grouped outcomes without writes',async({context,page})=>{
  const f=await fixture(context);await open(page);const grid=calendar(page);
  const selectedView=grid.getByRole('group',{name:'Yıllık takvim görünümü',exact:true}).locator('[aria-pressed="true"]'),otherView=grid.getByRole('button',{name:test.info().project.name==='mobile'?'Hafta':'Gün',exact:true});
  expect(await selectedView.evaluate(el=>getComputedStyle(el).backgroundColor)).not.toBe(await otherView.evaluate(el=>getComputedStyle(el).backgroundColor));
  await expect.poll(()=>grid.getByTestId('plan-time-scroller').evaluate(el=>{
    const target=Math.min(1050,el.scrollHeight-el.clientHeight);return Math.abs(el.scrollTop-target);
  })).toBeLessThanOrEqual(1);
  const math=grid.locator('.timed-block').filter({hasText:'Matematik'}).first(),turkish=grid.locator('.day-track[data-date="2027-01-04"] .timed-block').filter({hasText:'Türkçe'});
  const hour18=await grid.locator('.time-axis span').nth(18).boundingBox();expect(Math.abs((await math.boundingBox()).y-hour18.y)).toBeLessThanOrEqual(2);
  await expect(math).toBeInViewport();
  const a=await math.boundingBox(),b=await turkish.boundingBox();expect(a.x+a.width<=b.x+1||b.x+b.width<=a.x+1).toBe(true);
  expect(await math.evaluate(el=>getComputedStyle(el).backgroundColor)).not.toBe(await turkish.evaluate(el=>getComputedStyle(el).backgroundColor));
  await math.click();await expect(page.getByRole('heading',{name:'Seçilen çalışmalar',exact:true})).toBeVisible();await expect(page.locator('article.task')).toHaveCount(2);await expect(page.getByText('süre sürümü 2',{exact:false}).first()).toBeVisible();
  await page.getByRole('group',{name:'Plan görünümü',exact:true}).getByRole('button',{name:'Liste',exact:true}).click();await expect(page.locator('article.task')).toHaveCount(6);
  expect(f.writes).toHaveLength(0);expect(f.getAnnual().version).toBe(4);
  await page.reload();await expect(calendar(page)).toBeVisible();await calendar(page).getByRole('heading').click();await page.screenshot({path:`../../artifacts/yearly-calendar-${test.info().project.name}.png`,fullPage:false});
});
test('month and day navigate real occupied dates and never repeat the weekly template into empty months',async({context,page})=>{
  const f=await fixture(context);await open(page);const grid=calendar(page);await grid.getByRole('button',{name:'Ay',exact:true}).click();await expect(grid.locator('.month-block')).toHaveCount(3);
  expect(await grid.locator('.month-date').first().evaluate(el=>parseFloat(getComputedStyle(el).minHeight))).toBeLessThan(30);
  await grid.getByLabel('Tarihe git',{exact:true}).fill('2027-03-08');await expect(grid.locator('.month-block')).toHaveCount(1);await grid.getByRole('button',{name:'Sonraki dönem',exact:true}).click();await expect(grid.locator('.month-block')).toHaveCount(0);await expect(grid.getByRole('status')).toContainText('Bu aralıkta çalışma yok');
  await grid.getByLabel('Tarihe git',{exact:true}).fill('2027-03-08');await grid.getByRole('button',{name:'8 Mart 2027 Pazartesi gününü aç',exact:true}).click();await expect(grid.getByRole('button',{name:'Gün',exact:true})).toHaveAttribute('aria-pressed','true');
  await expect.poll(()=>grid.getByTestId('plan-time-scroller').evaluate(el=>Math.abs(el.scrollTop-540))).toBeLessThanOrEqual(10);await expect(grid.locator('.timed-block')).toHaveCount(1);
  await grid.getByRole('button',{name:'Sonraki dolu gün',exact:true}).click();await expect(grid.getByLabel('Tarihe git',{exact:true})).toHaveValue('2027-06-01');
  await grid.getByRole('button',{name:'Ay',exact:true}).click();await grid.getByRole('heading').click();await page.screenshot({path:`../../artifacts/yearly-calendar-month-${test.info().project.name}.png`,fullPage:false});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(f.writes).toHaveLength(0);
});
test('legacy completed work remains available in list and untimed calendar with no invented hour',async({context,page})=>{
  const f=await fixture(context);await open(page);await page.getByRole('button',{name:'Eski çalışma planım Aktif',exact:true}).click();await expect(page.getByText('Tamamlandı · 25 dk',{exact:false})).toBeVisible();
  await page.getByRole('group',{name:'Plan görünümü',exact:true}).getByRole('button',{name:'Takvim',exact:true}).click();const grid=calendar(page);await expect(grid.getByText('Saati belirtilmemiş',{exact:true})).toBeVisible();await expect(grid.getByTestId('plan-time-scroller')).toHaveCount(0);
  await grid.locator('.untimed-block').click();await expect(page.locator('article.task')).toContainText('Kaydedilen soru: 0');expect(f.old.tasks[0].actualMinutes).toBe(25);expect(f.old.version).toBe(7);expect(f.writes).toHaveLength(0);
});
test('a first lesson on Sunday stays horizontally visible in a narrow week',async({context,page})=>{
  const f=await fixture(context),tasks=f.getAnnual().tasks;f.getAnnual().tasks=[...tasks.slice(0,2).map(t=>({...t,plannedDate:'2027-01-10'})),{...tasks[0],id:'done',plannedDate:'2027-01-04',plannedStartMinute:480,isCompleted:true,actualMinutes:25,actualQuestions:0,completedAt:'2026-10-16T10:00:00Z'}];await open(page);
  await expect(calendar(page).locator('.day-track[data-date="2027-01-10"] .timed-block').first()).toBeInViewport({ratio:.95});expect(f.writes).toHaveLength(0);
});
test('calendar details reuse completion and rescheduling endpoints and preserve other unsaved inputs',async({context,page})=>{
  const f=await fixture(context,'Active');await open(page);await calendar(page).locator('.timed-block').filter({hasText:'Matematik'}).first().click();const first=page.locator('article.task').filter({has:page.getByRole('heading',{name:'Konu a',exact:true})}),second=page.locator('article.task').filter({has:page.getByRole('heading',{name:'Konu b',exact:true})});
  await second.getByLabel('Gerçek süre (dk)',{exact:true}).fill('32');await second.getByLabel('Gerçek soru (isteğe bağlı)',{exact:true}).fill('2');await first.getByLabel('Gerçek süre (dk)',{exact:true}).fill('17');await first.getByLabel('Gerçek soru (isteğe bağlı)',{exact:true}).fill('0');await first.getByRole('button',{name:'Tamamla',exact:true}).click();
  await expect(first).toContainText('Tamamlandı · 17 dk');await expect(second.getByLabel('Gerçek süre (dk)',{exact:true})).toHaveValue('32');await expect(second.getByLabel('Gerçek soru (isteğe bağlı)',{exact:true})).toHaveValue('2');
  await second.getByLabel('Yeni tarih',{exact:true}).fill('2027-01-05');await second.getByRole('button',{name:'Tarihi değiştir',exact:true}).click();await expect(second).toContainText('05.01.2027');
  expect(f.writes).toHaveLength(2);expect(f.writes[0].body).toEqual({expectedVersion:4,actualMinutes:17,actualQuestions:0});expect(f.writes[1].body).toEqual({expectedVersion:5,plannedDate:'2027-01-05'});expect(f.old.version).toBe(7);expect(f.old.tasks[0].actualMinutes).toBe(25);
});
