import {test,expect} from '@playwright/test';
// On phones the tables stack: each cell's accessible name starts with its column label, so match the value at the end.
const cell=(scope,value)=>scope.getByRole('cell',{name:new RegExp('(^|\\s)'+value.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')+'$')});
// The teacher sections are tabs on wide screens and a "Öğretmen bölümleri" select on phones.
async function openSection(page,name){if(test.info().project.name==='mobile')await page.getByRole('combobox',{name:'Öğretmen bölümleri',exact:true}).selectOption({label:name});else await page.getByRole('tab',{name,exact:true}).click();}
const token=()=>{const e=x=>Buffer.from(JSON.stringify(x)).toString('base64url');return `${e({alg:'HS256'})}.${e({sub:'teacher',role:'Teacher',platform_product:'coaching',exp:Math.floor(Date.now()/1000)+3600})}.fixture`;};
test('teacher follows all waiting submissions, opens an assignment, and returns to follow-up',async({page,context})=>{
 const queries=[],writes=[];const item={assignmentId:'a',studentId:'s',studentName:'Ayşe Test',title:'Matematik tekrar',subject:'Matematik',dueDate:'2027-01-04T12:00:00Z',status:'Assigned',isOverdue:true,overdueMinutes:1440};
 await context.route('**/api/**',async route=>{const request=route.request(),url=new URL(request.url()),path=url.pathname,bare=data=>route.fulfill({json:data});
  if(path.endsWith('/auth/refresh-token'))return bare({accessToken:token(),expiresInMinutes:60,tokenType:'Bearer'});
  if(path.endsWith('/auth/staff-session/products'))return bare([{product:'coaching',roles:['Teacher']}]);
  if(request.method()!=='GET'){writes.push(path);return route.fulfill({status:500});}
  if(path.endsWith('/assignment-follow-up')){queries.push(Object.fromEntries(url.searchParams));const pending=url.searchParams.get('mode')==='pending',search=url.searchParams.get('search');return bare({items:search==='yok'?[]:pending?[item,{...item,assignmentId:'future',title:'Türkçe tekrar',isOverdue:false,overdueMinutes:0}]:[item],totalCount:search==='yok'?0:pending?2:1,pageNumber:1,pageSize:25,pendingCount:2,overdueCount:1});}
  if(path.endsWith('/assignments/a'))return bare({id:'a',title:item.title,status:'Active',subject:'Matematik',source:'Digital',dueDate:item.dueDate,type:'Individual',totalStudents:1,submittedCount:0,assignedStudents:[{studentId:'s',status:'Assigned'}]});
  if(path.endsWith('/teachers/me/students'))return bare({items:[{userId:'s',fullName:'Ayşe Test'}],totalCount:1,pageNumber:1,pageSize:25});
  if(path.includes('/assignments/teacher/'))return bare({items:[],totalCount:0,pageNumber:1,pageSize:25});return bare({items:[],success:true,data:[],totalCount:0});
 });
 await page.goto('http://127.0.0.1:4318/?product=coaching');await openSection(page,'Ödevler');await page.getByRole('button',{name:'Geciken ve teslim edilmeyenler',exact:true}).click();const section=page.getByRole('region',{name:'Ödev toplu takibi',exact:true});await expect(cell(section,'Ayşe Test')).toBeVisible();await expect(cell(section,'04.01.2027 15:00')).toBeVisible();await expect(cell(section,'1 gün')).toBeVisible();
 await section.getByRole('combobox',{name:'Göster',exact:true}).selectOption('pending');await expect(cell(section,'Süresi dolmadı')).toBeVisible();await section.screenshot({path:`../../artifacts/coaching-followup-table-${test.info().project.name}.png`});
 await section.getByRole('button',{name:'Ayşe Test · Matematik tekrar teslimlerini aç',exact:true}).click();await expect(page.getByRole('heading',{name:'Matematik tekrar',exact:true})).toBeVisible();await expect(page.getByText('Henüz teslim edilmedi',{exact:true})).toBeVisible();await page.getByRole('button',{name:'← Ödevlerime dön',exact:true}).click();await expect(section).toBeVisible();
 await section.getByRole('searchbox',{name:'Ödev başlığında ara',exact:true}).fill('yok');await section.getByRole('button',{name:'Ara',exact:true}).click();await expect(section.getByText(/Kayıt bulunamadı/)).toBeVisible();expect(queries.at(-1).search).toBe('yok');expect(writes).toEqual([]);await page.screenshot({path:`../../artifacts/coaching-followup-${test.info().project.name}.png`,fullPage:false});
});
