import { test,expect } from '@playwright/test';

const token=()=>{
  const encode=x=>Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({alg:'HS256'})}.${encode({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'SystemAdmin',platform_product:'coaching',permission:['Permissions.Coaching.View','Permissions.Coaching.ContentManage'],given_name:'Test',family_name:'Yönetici',exp:Math.floor(Date.now()/1000)+900})}.fixture`;
};
const node=(kind,id,name,parentId,parentKind,extras={})=>({kind,id,name,parentId,parentKind,isActive:true,path:name,fingerprint:`version-${id}`,canDelete:false,blockers:['Bağlı kayıtlar var'],estimates:[],mappings:[],usedEstimateIds:[],...extras});
async function catalog(context){
  const nodes=[node('groups','group','Sayısal',null,null),node('lessons','lesson','Matematik','group','groups',{gradeNumber:8,examCode:'LGS'}),
    node('units','unit','Sayılar','lesson','lessons'),node('units','target-unit','Geometri','lesson','lessons'),node('topics','topic','Çarpanlar ve Katlar','unit','units'),
    node('topics','child','EBOB ve EKOK','topic','topics',{estimatedMinutes:60,blockers:['1 çalışma kaydı']}),
    node('outcomes','outcome','Pozitif tam sayıların çarpanlarını bulur.','child','topics',{code:'M.8.1.1.1',officialTopicId:'official',sourcePage:73,displayOrder:1,
      estimates:[{id:'estimate-1',number:1,learningMinutes:20,practiceMinutes:30,checkMinutes:10,confidence:'Low',evidence:'Başlangıç çalışma önerisi; konu bütçesi dayanak alındı.',method:'ReviewedStudy',isPublished:true}],usedEstimateIds:['estimate-1']})];
  const writes=[]; let conflict=false;
  const document=n=>{
    const parent=nodes.find(x=>x.id===n.parentId); const path=parent?document(parent).path+' / '+n.name:n.name;
    return {...n,path,mappings:n.kind==='outcomes'&&parent?[{topicId:parent.id,path:document(parent).path,isApproved:n.id==='outcome',evidence:'Konu kapsamını inceleyin.'}]:[]};
  };
  await context.route('**/api/**',async route=>{
    const req=route.request(),url=new URL(req.url()),path=url.pathname,method=req.method(),body=method==='GET'?null:req.postDataJSON();
    const respond=data=>route.fulfill({json:{success:true,data}});
    if(path.endsWith('/auth/refresh-token'))return route.fulfill({json:{accessToken:token(),tokenType:'Bearer',expiresInMinutes:15}});
    if(path.endsWith('/curriculum-management/children')){
      const kind=url.searchParams.get('kind'),parent=url.searchParams.get('parentId'),all=url.searchParams.get('all')==='true',search=url.searchParams.get('search')||'';
      const items=nodes.filter(x=>x.kind===kind&&(all||x.parentId===parent)&&x.name.includes(search)).map(document);
      return respond({items,totalCount:items.length,pageNumber:1,pageSize:25});
    }
    const prefix='/api/coaching-admin/curriculum-management/';
    if(path.startsWith(prefix)){
      const [kind,id,action,estimateId,estimateAction]=path.slice(prefix.length).split('/');
      let n=nodes.find(x=>x.id===id&&x.kind===kind);
      if(method==='GET')return respond(document(n));
      writes.push({kind,id,action,body});
      if(method==='POST'&&!id){const owner=nodes.find(x=>x.id===body.parentId);n=node(kind,'created-'+writes.length,body.name,body.parentId||null,owner?.kind||null,{...body,canDelete:true,blockers:[]});nodes.push(n);return respond(document(n));}
      if(method==='DELETE'&&!action){nodes.splice(nodes.indexOf(n),1);return respond({deleted:true});}
      if(method==='POST'&&action==='move'){
        n.isActive=false; const copy={...n,id:'moved',isActive:true,parentId:body.destinationId,parentKind:'units',fingerprint:'moved-version'};nodes.push(copy);return respond(document(copy));
      }
      if(action==='estimates'){
        const estimate=n.estimates.find(x=>x.id===estimateId);
        if(method==='DELETE')n.estimates=n.estimates.filter(x=>x.id!==estimateId);
        if(estimateAction==='publication')estimate.isPublished=false;
        return respond(document(n));
      }
      if(method==='PUT'){
        if(conflict)return route.fulfill({status:409,json:{message:'Kayıt değişmiş. Güncel kaydı yeniden açın.'}});
        Object.assign(n,body,{fingerprint:n.fingerprint+'-next'});return respond(document(n));
      }
    }
    if(path.endsWith('/outcomes/outcome/estimates')){
      writes.push({action:'estimate',body}); const n=nodes.find(x=>x.id==='outcome');
      const estimate={id:'estimate-2',number:2,...body,method:'ReviewedStudy',isPublished:false};n.estimates.unshift(estimate);return respond(estimate);
    }
    if(path.endsWith('/estimates/estimate-2/publication')){writes.push({action:'publication',body});nodes.find(x=>x.id==='outcome').estimates[0].isPublished=true;return respond({});}
    if(path.endsWith('/coaching-admin/outcomes'))return respond([{id:'edition',code:'meb-lgs-2026',subjectName:'Matematik',academicYearStart:2026,academicYearEnd:2026}]);
    if(path.endsWith('/coaching-admin/outcomes/edition'))return respond({officialTopics:[{id:'official',name:'Çarpanlar ve Katlar',lessonPeriods:10}]});
    return respond([]);
  });
  return {nodes,writes,setConflict:value=>conflict=value};
}
async function openBranch(page){
  await page.goto('/dashboard/coaching/curriculum');
  for(const name of ['Sayısal','Matematik','Sayılar','Çarpanlar ve Katlar','EBOB ve EKOK'])await page.getByRole('button',{name:name+' alt kayıtları',exact:true}).click();
}

test('hierarchy supports group creation, topic edit, move and guarded deletion',async({context,page})=>{
  const f=await catalog(context);await openBranch(page);
  await page.getByRole('button',{name:'EBOB ve EKOK düzenle',exact:true}).click();
  await page.getByLabel('Konu çalışma süresi (dk)',{exact:true}).fill('75');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Konu çalışma süresini güncelle');
  await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();
  await expect(page.getByRole('status').filter({hasText:'Kayıt kaydedildi.'})).toBeVisible();
  expect(f.writes.at(-1).body.estimatedMinutes).toBe(75);expect(f.writes.at(-1).body.fingerprint).toBe('version-child');
  await page.getByText('Kullanım durumu ve güvenli silme',{exact:true}).click();
  await expect(page.getByText('1 çalışma kaydı',{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Kalıcı sil',exact:true})).toBeDisabled();
  await page.getByText('Başka konuma taşı',{exact:true}).click();
  await page.getByRole('button',{name:'Hedefleri ara',exact:true}).click();
  await page.getByLabel('Hedef konum',{exact:true}).selectOption('target-unit');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Konuyu yeni üniteye taşı');
  await page.getByLabel('Hedefi ve geçmiş planların korunacağını kontrol ettim.',{exact:true}).check();
  await page.getByRole('button',{name:'Seçilen konuma taşı',exact:true}).click();
  await expect(page.getByRole('status').filter({hasText:'Taşıma tamamlandı.'})).toBeVisible();expect(f.nodes.find(x=>x.id==='child').isActive).toBe(false);
  await page.getByRole('button',{name:'Ders grubu ekle',exact:true}).click();
  await page.getByLabel('Ad',{exact:true}).fill('Deneme grubu');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Yeni ders grubu oluştur');
  await page.getByRole('button',{name:'Kaydı oluştur',exact:true}).click();await expect(page.getByRole('heading',{name:'Deneme grubu',exact:true})).toBeVisible();
  await page.getByText('Kullanım durumu ve güvenli silme',{exact:true}).click();await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Kullanılmayan deneme grubunu sil');
  await page.getByLabel('Onaylamak için SİL yazın',{exact:true}).fill('SİL');await page.getByRole('button',{name:'Kalıcı sil',exact:true}).click();
  await expect(page.getByText('Kullanılmayan kayıt silindi.',{exact:true})).toBeVisible();expect(f.nodes.some(x=>x.name==='Deneme grubu')).toBe(false);
});

test('outcome text and duration versions are readable, editable and safely withdrawn',async({context,page})=>{
  const f=await catalog(context);await openBranch(page);
  await page.getByRole('button',{name:'Pozitif tam sayıların çarpanlarını bulur. düzenle',exact:true}).click();
  await page.getByLabel('Kazanım metni',{exact:true}).fill('Pozitif tam sayıların çarpanlarını belirler ve üslü ifadelerle yazar.');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Kazanım açıklamasını düzelt');await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();
  await expect(page.getByText('Kayıt kaydedildi.',{exact:true})).toBeVisible();
  await page.getByLabel('Öğrenme (dk)',{exact:true}).fill('25');await page.getByLabel('Alıştırma (dk)',{exact:true}).fill('40');await page.getByLabel('Kısa kontrol (dk)',{exact:true}).fill('10');
  await page.getByLabel('Süre dayanağı',{exact:true}).fill('Öğretmen tarafından ilk öğrenme için gözlenen başlangıç süresi');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('İncelenen süreyi yayınla');
  await page.getByLabel('Yeni süre sürümünü inceleyip yayınlamayı onaylıyorum.',{exact:true}).check();await page.getByRole('button',{name:'Yeni süre sürümünü kaydet ve yayınla',exact:true}).click();
  await expect(page.getByText('Sürüm 2 · 75 dk',{exact:true})).toBeVisible();expect(f.writes.some(x=>x.action==='publication')).toBe(true);
  const version=page.locator('.duration-history article').filter({hasText:'Sürüm 2 · 75 dk'});await version.getByText('Bu süre sürümü için işlemler',{exact:true}).click();
  await version.getByLabel('Sürüm 2 için işlem yapmayı onaylıyorum.',{exact:true}).check();await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Süre önerisini yayından kaldır');
  await version.getByRole('button',{name:'Bu süreyi yayından kaldır',exact:true}).click();await expect(page.getByText(/Süre sürümü yayından kaldırıldı/)).toBeVisible();
  expect(f.nodes.find(x=>x.id==='outcome').estimates.find(x=>x.id==='estimate-1').isPublished).toBe(true);
  await expect(page.locator('.curriculum-workspace')).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth+1)).toBe(true);
  await page.getByRole('heading',{name:'Kazanım çalışma süresi',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:`../../artifacts/coaching-curriculum/manager-${test.info().project.name}.png`,fullPage:false});
});

test('concurrent edit preserves entered values and asks before discarding them',async({context,page})=>{
  const f=await catalog(context);await openBranch(page);await page.getByRole('button',{name:'EBOB ve EKOK düzenle',exact:true}).click();
  await page.getByLabel('Ad',{exact:true}).fill('Korunacak konu adı');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Konu adını güncelle');f.setConflict(true);
  await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Kayıt değişmiş');
  await expect(page.getByLabel('Ad',{exact:true})).toHaveValue('Korunacak konu adı');await page.getByRole('button',{name:'Matematik düzenle',exact:true}).click();
  await expect(page.getByText(/Kaydedilmemiş değişiklikler var/)).toBeVisible();await page.getByRole('button',{name:'Düzenlemeye devam et',exact:true}).click();
  await expect(page.getByLabel('Ad',{exact:true})).toHaveValue('Korunacak konu adı');
});

test('administrator creates unit, topic, subtopic and a sourced outcome through the real forms',async({context,page})=>{
  const f=await catalog(context);await openBranch(page);await page.getByRole('button',{name:'Matematik düzenle',exact:true}).click();
  for(const [button,name] of [['+ Ünite','Yeni ünite'],['+ Konu','Yeni konu'],['+ Alt konu','Yeni alt konu']]){
    await page.getByRole('button',{name:button,exact:true}).click();await page.getByLabel('Ad',{exact:true}).fill(name);
    await page.getByLabel('İşlem gerekçesi',{exact:true}).fill(name+' kaydı oluştur');await page.getByRole('button',{name:'Kaydı oluştur',exact:true}).click();
    await expect(page.getByRole('heading',{name,exact:true})).toBeVisible();
  }
  await page.getByRole('button',{name:'+ Kazanım',exact:true}).click();await page.getByLabel('Kazanım metni',{exact:true}).fill('Pozitif tam sayıları karşılaştırır.');
  await page.getByLabel('Kazanım kodu',{exact:true}).fill('M.8.1.1.9');await page.getByLabel('Kaynak program',{exact:true}).selectOption('edition');
  await page.getByLabel('Programdaki konu grubu',{exact:true}).selectOption('official');await page.getByLabel('Kaynak sayfa',{exact:true}).fill('73');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Kaynak program dayanağıyla yeni kazanım oluştur');await page.getByRole('button',{name:'Kaydı oluştur',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Pozitif tam sayıları karşılaştırır.',exact:true})).toBeVisible();
  expect(f.writes.at(-1).body.officialTopicId).toBe('official');expect(f.nodes.find(x=>x.name==='Yeni alt konu').parentKind).toBe('topics');
});
