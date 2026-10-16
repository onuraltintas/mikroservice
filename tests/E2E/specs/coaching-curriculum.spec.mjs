import { test,expect } from '@playwright/test';

const token=()=>{
  const encode=x=>Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({alg:'HS256'})}.${encode({sub:'b8a09753-f3ea-44d8-a444-cf49577b4a01',role:'SystemAdmin',platform_product:'coaching',permission:['Permissions.Coaching.View','Permissions.Coaching.ContentManage'],given_name:'Test',family_name:'Yönetici',exp:Math.floor(Date.now()/1000)+900})}.fixture`;
};
const node=(kind,id,name,parentId,parentKind,extras={})=>({kind,id,name,parentId,parentKind,isActive:true,path:name,fingerprint:`version-${id}`,canDelete:false,blockers:['Bağlı kayıtlar var'],estimates:[],mappings:[],usedEstimateIds:[],...extras});
async function catalog(context){
  const nodes=[node('exams','exam','Liselere Geçiş Sistemi',null,null,{code:'LGS'}),node('lessons','lesson','Matematik','exam','exams',{gradeNumber:8,examCode:'LGS'}),
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
    if(path.endsWith('/coaching/catalog/exams'))return respond(nodes.filter(n=>n.kind==='exams'));
    if(path.endsWith('/coaching-admin/curricula'))return respond([{id:'curriculum',name:'Ders Kataloğu',versionLabel:'2026',isPublished:true}]);
    if(path.endsWith('/curriculum-management/children')){
      const kind=url.searchParams.get('kind'),parent=url.searchParams.get('parentId'),all=url.searchParams.get('all')==='true',search=url.searchParams.get('search')||'';
      const level=url.searchParams.get('level');
      const items=nodes.filter(x=>x.kind===kind&&(all||x.parentId===parent)&&x.name.includes(search)&&(level!=='root'||x.parentKind!=='topics')&&(level!=='sub'||x.parentKind==='topics')&&(level!=='leaf'||!nodes.some(c=>c.kind==='topics'&&c.parentId===x.id))).map(document);
      return respond({items,totalCount:items.length,pageNumber:1,pageSize:25});
    }
    const prefix='/api/coaching-admin/curriculum-management/';
    if(path.startsWith(prefix)){
      const [kind,id,action,estimateId,estimateAction]=path.slice(prefix.length).split('/');
      let n=nodes.find(x=>x.id===id&&x.kind===kind);
      if(method==='GET')return respond(document(n));
      writes.push({kind,id,action,body});
      if(method==='POST'&&!id){const owner=nodes.find(x=>x.id===body.parentId);n=node(kind,'created-'+writes.length,body.name,body.parentId||null,owner?.kind||null,{...body,isActive:!['lessons','units','topics'].includes(kind),canDelete:true,blockers:[]});nodes.push(n);return respond(document(n));}
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

async function tab(page,name){await page.getByRole('tab',{name,exact:true}).click();}
async function create(page,label,name,parent){
  await page.getByRole('button',{name:label+' ekle',exact:true}).click();
  if(parent)await page.getByLabel('Bağlanacağı üst kayıt',{exact:true}).selectOption(parent);
  await page.getByLabel('Ad',{exact:true}).fill(name);
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('İçerik yönetimi testi için oluştur');
}
async function save(page){await page.getByRole('button',{name:'Kaydı oluştur',exact:true}).click();await expect(page.getByText('Kayıt kaydedildi.',{exact:true})).toBeVisible();}

test('exam tabs support dynamic exam creation, edit, lesson attachment and automatic activation',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');
  await expect(page.getByRole('tablist')).toBeVisible();await expect(page.getByText('Ders grupları',{exact:true})).toHaveCount(0);
  await create(page,'Sınav türü','Akademik Personel Sınavı');await page.getByLabel('Sınav kodu',{exact:true}).fill('ALES');await save(page);
  const exam=f.nodes.find(x=>x.code==='ALES');
  await page.getByLabel('Ad',{exact:true}).fill('Akademik Personel ve Lisansüstü Eğitimi Giriş Sınavı');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Sınavın açık adını düzenle');
  await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();await expect(page.getByText('Kayıt kaydedildi.',{exact:true})).toBeVisible();
  await tab(page,'Dersler');await page.getByLabel('Sınav türü ile filtrele',{exact:true}).selectOption(exam.id);
  await create(page,'Ders','Matematik',exam.id);await page.getByLabel('Sınıf',{exact:true}).selectOption('12');await page.getByLabel('Ders programı',{exact:true}).selectOption('curriculum');await save(page);
  const lesson=f.nodes.find(x=>x.kind==='lessons'&&x.parentId===exam.id);expect(lesson.examCode).toBe('ALES');expect(lesson.isActive).toBe(true);expect(lesson.curriculumVersionId).toBe('curriculum');
  await page.screenshot({path:`../../artifacts/coaching-dynamic/lessons-${test.info().project.name}.png`,fullPage:true});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
});

test('subtopic edit move and safe deletion keep old usage visible',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');await tab(page,'Alt konular');
  await page.getByRole('button',{name:'EBOB ve EKOK düzenle',exact:true}).click();await page.getByLabel('Konu çalışma süresi (dk)',{exact:true}).fill('75');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Konu çalışma süresini güncelle');await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();await expect(page.getByText('Kayıt kaydedildi.',{exact:true})).toBeVisible();
  expect(f.nodes.find(x=>x.id==='child').estimatedMinutes).toBe(75);
  await page.getByText('Güvenli silme',{exact:true}).click();await expect(page.getByText('1 çalışma kaydı',{exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Kalıcı sil',exact:true})).toBeDisabled();
  await page.getByText('Başka konuma taşı',{exact:true}).click();await page.getByRole('button',{name:'Hedefleri ara',exact:true}).click();await page.getByLabel('Hedef konum',{exact:true}).selectOption('target-unit');
  await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Konuyu yeni üniteye taşı');await page.getByLabel('Hedefi ve geçmiş planların korunacağını kontrol ettim.',{exact:true}).check();
  await page.getByRole('button',{name:'Seçilen konuma taşı',exact:true}).click();await expect(page.getByRole('status').filter({hasText:'Taşıma tamamlandı.'})).toBeVisible();expect(f.nodes.find(x=>x.id==='child').isActive).toBe(false);
});

test('outcome durations show simple inputs with collapsible history and preserved used duration',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');await tab(page,'Kazanımlar');
  await page.getByRole('button',{name:'Pozitif tam sayıların çarpanlarını bulur. düzenle',exact:true}).click();
  await expect(page.getByText('Sürüm 1 · 60 dk',{exact:true})).not.toBeVisible();
  await page.getByLabel('Öğrenme (dk)',{exact:true}).fill('25');await page.getByLabel('Alıştırma (dk)',{exact:true}).fill('40');await page.getByLabel('Kısa kontrol (dk)',{exact:true}).fill('10');
  await page.getByLabel('Süre dayanağı',{exact:true}).fill('Öğretmen gözlemine göre çalışma süresi');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('İncelenen süreyi yayınla');
  await page.getByLabel('Yeni süre sürümünü inceleyip yayınlamayı onaylıyorum.',{exact:true}).check();await page.getByRole('button',{name:'Yeni süre sürümünü kaydet ve yayınla',exact:true}).click();
  await expect(page.getByText('Yeni süre sürümü yayınlandı.',{exact:true})).toBeVisible();await page.getByText('Önceki süreler ve değişiklik geçmişi',{exact:true}).click();await expect(page.getByText('Sürüm 2 · 75 dk',{exact:true})).toBeVisible();
  const used=page.locator('.duration-history article').filter({hasText:'Sürüm 1 · 60 dk'});await used.getByText('Bu süre sürümü için işlemler',{exact:true}).click();await expect(used.getByText('Bu sürüm öğrenci planında kullanılmış; kalıcı silinemez.',{exact:true})).toBeVisible();
  expect(f.nodes.find(x=>x.id==='outcome').estimates.find(x=>x.id==='estimate-1').learningMinutes).toBe(20);
});

test('each tab can create a unit topic subtopic and sourced outcome',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');await tab(page,'Üniteler');await create(page,'Ünite','Yeni ünite','lesson');await save(page);
  const unit=f.nodes.find(x=>x.name==='Yeni ünite');await tab(page,'Konular');await create(page,'Konu','Yeni konu',unit.id);await page.getByLabel('Konu çalışma süresi (dk)',{exact:true}).fill('45');await save(page);
  const topic=f.nodes.find(x=>x.name==='Yeni konu');await tab(page,'Alt konular');await create(page,'Alt konu','Yeni alt konu',topic.id);await save(page);
  const child=f.nodes.find(x=>x.name==='Yeni alt konu');await tab(page,'Kazanımlar');await page.getByRole('button',{name:'Kazanım ekle',exact:true}).click();await page.getByLabel('Bağlanacağı üst kayıt',{exact:true}).selectOption(child.id);
  await page.getByLabel('Kazanım metni',{exact:true}).fill('Yeni kazanım açıklaması');await page.getByLabel('Kazanım kodu',{exact:true}).fill('M.8.1.1.9');await page.getByLabel('Kaynak program',{exact:true}).selectOption('edition');await page.getByLabel('Programdaki konu grubu',{exact:true}).selectOption('official');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Program dayanaklı kazanım ekle');await save(page);
  expect(f.nodes.find(x=>x.name==='Yeni kazanım açıklaması').parentId).toBe(child.id);
});

test('tab navigation and concurrency preserve unsaved fields until explicitly discarded',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');await tab(page,'Alt konular');await page.getByRole('button',{name:'EBOB ve EKOK düzenle',exact:true}).click();
  await page.getByLabel('Ad',{exact:true}).fill('Düzenlenen alt konu');await tab(page,'Sınav türleri');await expect(page.getByText('Kaydedilmemiş değişiklikler var. Sekme değiştirilsin mi?',{exact:true})).toBeVisible();await page.getByRole('button',{name:'Düzenlemeye devam et',exact:true}).click();
  await expect(page.getByLabel('Ad',{exact:true})).toHaveValue('Düzenlenen alt konu');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Çakışan düzenlemeyi dene');f.setConflict(true);await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Kayıt değişmiş');await expect(page.getByLabel('Ad',{exact:true})).toHaveValue('Düzenlenen alt konu');
  await tab(page,'Sınav türleri');await page.getByRole('button',{name:'Değişiklikleri bırak ve sekmeye geç',exact:true}).click();await expect(page.getByRole('tab',{name:'Sınav türleri',exact:true})).toHaveAttribute('aria-selected','true');
  await page.getByRole('tab',{name:'Sınav türleri',exact:true}).press('ArrowRight');await expect(page.getByRole('tab',{name:'Dersler',exact:true})).toHaveAttribute('aria-selected','true');
});


test('source program and its topic budget can be created and edited in separate tabs',async({context,page})=>{
  const f=await catalog(context);await page.goto('/dashboard/coaching/curriculum');await tab(page,'Kaynak programlar');
  await create(page,'Kaynak program','Matematik');await page.getByLabel('Program kodu',{exact:true}).fill('mat-2026');await page.getByLabel('Sınav türü',{exact:true}).selectOption('LGS');
  await page.getByLabel('Sınıf',{exact:true}).fill('8');await page.getByLabel('Kaynak belge bağlantısı',{exact:true}).fill('https://meb.gov.tr/program');await page.getByLabel('Belge SHA256 özeti',{exact:true}).fill('a'.repeat(64));await page.getByLabel('Kapsam dayanağı bağlantısı',{exact:true}).fill('https://meb.gov.tr/kapsam');await save(page);
  const program=f.nodes.find(x=>x.kind==='programs');await tab(page,'Program konu grupları');await create(page,'Program konu grubu','Sayılar ve İşlemler',program.id);await page.getByLabel('Program konu kodu',{exact:true}).fill('SAY');await page.getByLabel('Kazanım sayısı',{exact:true}).fill('5');await page.getByLabel('Programdaki ders saati',{exact:true}).fill('12');await save(page);
  await page.getByLabel('Programdaki ders saati',{exact:true}).fill('15');await page.getByLabel('İşlem gerekçesi',{exact:true}).fill('Belgeye göre konu bütçesini düzelt');await page.getByRole('button',{name:'Değişiklikleri kaydet',exact:true}).click();await expect(page.getByText('Kayıt kaydedildi.',{exact:true})).toBeVisible();
  expect(f.nodes.find(x=>x.kind==='programTopics').lessonPeriods).toBe(15);
});
