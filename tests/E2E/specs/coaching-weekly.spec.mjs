import {test,expect} from '@playwright/test';
const math='33333333-3333-4333-8333-333333333331',turkish='33333333-3333-4333-8333-333333333332';
const lessons=[{id:math,name:'Matematik',examCode:'LGS',gradeNumber:8},{id:turkish,name:'Türkçe',examCode:'LGS',gradeNumber:8}];
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return `${e({alg:'HS256'})}.${e({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'Student',platform_product:'coaching',given_name:'Test',family_name:'Öğrenci',exp:Math.floor(Date.now()/1000)+900})}.fixture`;};
async function fixture(context){
  let draft=null,conflict=false;const writes=[];const active={id:'old-plan',version:7,title:'Mevcut çalışma planım',status:'Active',tasks:[{id:'work',plannedDate:'2026-10-16',title:'Önceki çalışma kaydım',plannedMinutes:30,topicId:null,isCompleted:true,isPinned:false,actualMinutes:25,actualQuestions:0,completedAt:'2026-10-16T18:00:00Z'}]};
  await context.route('**/api/**',async route=>{
    const req=route.request(),path=new URL(req.url()).pathname;const ok=data=>route.fulfill({json:{success:true,data}});
    if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),expiresInMinutes:15,tokenType:'Bearer'}});
    if(path.endsWith('/profiles/me'))return ok({student:{educationStatus:'Student',gradeLevel:8,isActive:true},memberships:[]});
    if(path.endsWith('/weekly-draft')){
      if(req.method()==='GET')return ok({draft,lessons});
      const body=req.postDataJSON();writes.push({path,body});if(conflict)return route.fulfill({status:409,json:{message:'Taslak başka bir sekmede değişti.'}});
      expect(body.expectedVersion).toBe(draft?.version??null);draft={version:(draft?.version??-1)+1,timeZoneId:body.timeZoneId,slots:body.slots.map(s=>({...s,lessonName:lessons.find(l=>l.id===s.lessonId).name,examCode:'LGS'}))};return ok(draft);
    }
    if(path.endsWith('/plans/old-plan'))return ok(active);
    if(path.endsWith('/plans'))return ok({items:[active],totalCount:1,pageNumber:1,pageSize:20});
    if(req.method()!=='GET')writes.push({path});return ok([]);
  });
  return{writes,getDraft:()=>draft,setConflict:value=>conflict=value,active};
}
async function open(page){await page.goto('/coaching-portal/weekly-plan');await expect(page.getByRole('heading',{name:'Haftalık programım',exact:true})).toBeVisible();await page.getByRole('button',{name:'Hafta',exact:true}).click();}
async function add(page,lesson=math,start='18:00',duration='60'){
  await page.getByRole('button',{name:'+ Ders ekle',exact:true}).click();const dialog=page.getByRole('dialog');await dialog.getByLabel('Ders',{exact:true}).selectOption(lesson);await dialog.getByLabel('Gün',{exact:true}).selectOption('Monday');await dialog.getByLabel('Başlangıç',{exact:true}).fill(start);await dialog.getByLabel('Süre',{exact:true}).selectOption({label:duration==='30'?'30 dakika':'1 saat (60 dakika)'});await dialog.getByRole('button',{name:'Dersi ekle',exact:true}).click();await expect(dialog).toHaveCount(0);
}
const card=(page,name)=>page.locator('.slot-card').filter({hasText:name}).first();
async function resize(page,name,edge,delta){
  const handle=card(page,name).getByRole('button',{name:name+(edge==='bottom'?' bitişini ayarla':' başlangıcını ayarla'),exact:true});await handle.scrollIntoViewIfNeeded();const b=await handle.boundingBox();
  if(test.info().project.name==='mobile'){
    const session=await page.context().newCDPSession(page),point={x:b.x+b.width/2,y:b.y+b.height/2};
    await session.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[point]});
    for(let step=1;step<=8;step++)await session.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{...point,y:point.y+delta*step/8}]});
    await session.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await session.detach();return;
  }
  await page.mouse.move(b.x+b.width/2,b.y+b.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2+delta,{steps:8});await page.mouse.up();
}

test('right click adds a lesson, resizing frees the other half hour and both save and reopen',async({context,page})=>{
  const f=await fixture(context);await open(page);await page.getByTestId('cell-Monday-1080').click({button:'right'});await page.getByRole('menuitem',{name:'+ Ders ekle',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Dersi ekle',exact:true}).click();
  await resize(page,'Matematik','bottom',-36);await expect(card(page,'Matematik')).toContainText('30 dk');await add(page,turkish,'18:30','30');
  expect((await card(page,'Matematik').boundingBox()).height).toBeCloseTo((await page.getByTestId('cell-Monday-1080').boundingBox()).height,0);
  await page.getByRole('button',{name:'Taslağı kaydet',exact:true}).click();await expect(page.getByRole('status')).toContainText('Haftalık taslak kaydedildi');expect(f.getDraft().slots.map(s=>s.durationMinutes)).toEqual([30,30]);
  await page.reload();await page.getByRole('button',{name:'Hafta',exact:true}).click();await expect(card(page,'Matematik')).toContainText('18:00–18:30');await expect(card(page,'Türkçe')).toContainText('18:30–19:00');
  await page.locator('main').first().evaluate(el=>el.scrollTo({top:0,behavior:'instant'}));await page.locator('.time-scroller').evaluate(el=>el.scrollTop=17*72);await page.screenshot({path:`../../artifacts/coaching-weekly/calendar-${test.info().project.name}.png`,fullPage:false});
  await page.getByRole('link',{name:'Mevcut planlarım ↗',exact:true}).click();await expect(page.getByRole('heading',{name:'Mevcut çalışma planım',exact:true})).toBeVisible();await expect(page.getByText(/Tamamlandı · 25 dk/)).toBeVisible();expect(f.active.version).toBe(7);expect(f.writes.every(w=>w.path.endsWith('/weekly-draft'))).toBe(true);
});

test('cards move across days and top edge resizing preserves the end time',async({context,page})=>{
  await fixture(context);await open(page);await add(page);
  if(test.info().project.name==='desktop'){
    await card(page,'Matematik').scrollIntoViewIfNeeded();const a=await card(page,'Matematik').boundingBox(),target=await page.getByTestId('cell-Wednesday-1080').boundingBox();
    await page.mouse.move(a.x+a.width/2,a.y+a.height/2);await page.mouse.down();await page.mouse.move(target.x+target.width/2,a.y+a.height/2,{steps:12});await page.mouse.up();
    await expect(page.locator('.day-track[data-day="Wednesday"] .slot-card')).toHaveCount(1);
  }else{
    await card(page,'Matematik').getByRole('button',{name:'Matematik 18:00 60 dakika düzenle',exact:true}).click();await page.getByRole('dialog').getByLabel('Gün',{exact:true}).selectOption('Wednesday');await page.getByRole('button',{name:'Değişikliği uygula',exact:true}).click();
  }
  await resize(page,'Matematik','top',36);await expect(card(page,'Matematik')).toContainText('18:30–19:00');
});

test('overlapping resize is reverted, form remains editable and cancelled drag does not change slots',async({context,page})=>{
  await fixture(context);await open(page);await add(page,math,'18:00','30');await add(page,turkish,'18:30','30');
  await resize(page,'Matematik','bottom',36);await expect(page.getByRole('alert')).toContainText('çakış');await expect(card(page,'Matematik')).toContainText('18:00–18:30');
  await card(page,'Türkçe').getByRole('button',{name:'Türkçe 18:30 30 dakika düzenle',exact:true}).click();await page.getByRole('dialog').getByLabel('Başlangıç',{exact:true}).fill('18:00');await page.getByRole('button',{name:'Değişikliği uygula',exact:true}).click();await expect(page.getByRole('dialog').getByRole('alert')).toContainText('çakış');await page.getByRole('button',{name:'Vazgeç',exact:true}).click();
  const handle=card(page,'Türkçe').getByRole('button',{name:'Türkçe bitişini ayarla',exact:true});await handle.scrollIntoViewIfNeeded();const b=await handle.boundingBox();await page.mouse.move(b.x+b.width/2,b.y+b.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2,b.y+b.height/2+36,{steps:6});await page.keyboard.press('Escape');await page.mouse.up();await expect(card(page,'Türkçe')).toContainText('18:30–19:00');
});

test('month and day share the weekly draft and conflict leaves local edits intact',async({context,page})=>{
  const f=await fixture(context);await open(page);await add(page);await page.getByRole('button',{name:'Ay',exact:true}).click();await expect(page.locator('.month-slot')).not.toHaveCount(0);await expect(page.getByText('60 dk / hafta',{exact:true})).toBeVisible();
  await page.locator('.month-date').first().click();await expect(page.getByRole('button',{name:'Gün',exact:true})).toHaveAttribute('aria-pressed','true');
  f.setConflict(true);await page.getByRole('button',{name:'Taslağı kaydet',exact:true}).click();await expect(page.getByRole('alert')).toContainText('başka bir sekmede');await expect(page.getByRole('button',{name:'Taslağı kaydet',exact:true})).toBeDisabled();expect(f.writes[0].body.slots).toHaveLength(1);await expect(page.getByText('60 dk / hafta',{exact:true})).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
});
