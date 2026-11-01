import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CoachingCurriculumManagerComponent } from './coaching-curriculum-manager.component';
import { KM7A_REAL, KM7A_STUBS } from './coaching-km7a-embeds.testing';

describe('Coaching curriculum manager', () => {
  const topic = { kind: 'topics', id: 'topic', name: 'Çarpanlar', path: 'Sayısal / Matematik / Sayılar / Çarpanlar', parentId: 'unit', parentKind: 'units', isActive: true,
    fingerprint: 'current', estimatedMinutes: 60, canDelete: false, blockers: ['1 çalışma kaydı'], children: 0, outcomes: [], estimates: [] };
  function setup() {
    TestBed.configureTestingModule({ imports: [CoachingCurriculumManagerComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    TestBed.overrideComponent(CoachingCurriculumManagerComponent, { remove: { imports: KM7A_REAL }, add: { imports: KM7A_STUBS } });
    const fixture = TestBed.createComponent(CoachingCurriculumManagerComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/curriculum-management/children')).flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } });
    // KM7: the filter bar loads the exams and the header shows the pending link count.
    http.expectOne(r => r.url.endsWith('/coaching/catalog/exams')).flush({ data: [] });
    http.expectOne(r => r.url.endsWith('/outcomes/mappings')).flush({ data: { items: [], totalCount: 0 } });
    return { fixture, http, c: fixture.componentInstance };
  }
  it('shows separate catalog tabs with a direct exam create action and no lesson groups', () => {
    const { fixture, http } = setup(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Müfredat Yönetimi');
    expect(fixture.nativeElement.textContent).toContain('Henüz kayıt yok');
    expect(fixture.nativeElement.querySelector('[role="tablist"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Müfredat hiyerarşisi"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Sınav türü');
    expect(fixture.nativeElement.textContent).toContain('Kazanım');
    expect(fixture.nativeElement.querySelectorAll('[role="tab"]').length).toBeGreaterThanOrEqual(7);
    expect(fixture.nativeElement.querySelector('button[aria-label="Sınav türü ekle"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Ders grupları'); http.verify();
  });
  it('shows usage blockers and forbids deletion of a used topic', () => {
    const { fixture, http, c } = setup(); c.select({ ...topic } as never);
    http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: topic }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('1 çalışma kaydı');
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(x => x.textContent?.trim() === 'Kalıcı sil');
    expect(button?.disabled).toBe(true); c.remove(); http.expectNone(r => r.method === 'DELETE'); http.verify();
  });
  // KM1 (01.11.2026): an exam has dated sessions (YDS twice a year, e-YDS many times) instead of one optional date.
  // KM1 (01.11.2026): an exam has dated sessions; KM7 moves their editing into KM7a's <app-exam-sessions-editor>.
  it('embeds the exam sessions editor and sends its sessions as calendar dates', async () => {
    const {fixture,http,c}=setup();c.create('exams');fixture.detectChanges();await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('input[name="examDate"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-exam-sessions-editor')).not.toBeNull();
    c.form.sessions=[{id:null,date:'2027-03-28',label:'2027/1',applicationStart:'2027-02-03',applicationEnd:'2027-02-11'},{id:null,date:'2027-11-21',label:'2027/2',applicationStart:'',applicationEnd:''}];
    c.form.name='Yabancı Dil Bilgisi Seviye Tespit Sınavı';c.form.code='YDS';c.form.reason='YDS oturumları';c.save();
    const req=http.expectOne(r=>r.method==='POST');
    expect(req.request.body.examDate).toBeUndefined();
    expect(req.request.body.sessions).toEqual([{id:null,date:'2027-03-28',label:'2027/1',applicationStart:'2027-02-03',applicationEnd:'2027-02-11'},
      {id:null,date:'2027-11-21',label:'2027/2',applicationStart:null,applicationEnd:null}]);
    req.flush({message:'Örnek çakışma'},{status:409,statusText:'Conflict'});http.verify();
  });
  it('does not save an exam while the sessions editor reports invalid dates', () => {
    const {http,c}=setup();c.create('exams');c.sessionsValid.set(false);
    c.form.name='YDS';c.form.code='YDS';c.form.reason='YDS oturumları';c.save();http.expectNone(r=>r.method==='POST');http.verify();
  });
  it('loads the sessions of an exam, removes one and keeps the others with their ids', () => {
    const {fixture,http,c}=setup();
    const exam={kind:'exams',id:'yds',name:'YDS',code:'YDS',path:'YDS',isActive:false,fingerprint:'v1',canDelete:true,blockers:[],estimates:[],mappings:[],examDate:'2027-03-28',
      sessions:[{id:'s1',date:'2027-03-28',label:'2027/1',applicationStart:'2027-02-03',applicationEnd:'2027-02-11'},{id:'s2',date:'2027-11-21',label:'2027/2',applicationStart:null,applicationEnd:null}]};
    c.select({...exam} as never);http.expectOne(r=>r.url.endsWith('/exams/yds')).flush({data:exam});fixture.detectChanges();
    expect(c.form.sessions.map(x=>x.label)).toEqual(['2027/1','2027/2']);
    expect(fixture.nativeElement.querySelector('app-exam-sessions-editor')).not.toBeNull();
    c.form.sessions=c.form.sessions.slice(1);c.form.reason='Bahar oturumu iptal';c.save();
    const req=http.expectOne(r=>r.method==='PUT');
    expect(req.request.body.sessions).toEqual([{id:'s2',date:'2027-11-21',label:'2027/2',applicationStart:null,applicationEnd:null}]);
    req.flush({message:'Çakışma'},{status:409,statusText:'Conflict'});http.verify();
  });
  it('edits the topic exam weight used when a plan does not fit (roadmap O7)', () => {
    const { fixture, http, c } = setup(); c.select({ ...topic } as never);
    http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: { ...topic, yieldWeight: 4 } }); fixture.detectChanges();
    expect(c.form.yieldWeight).toBe(4);
    expect(fixture.nativeElement.querySelector('select[name="yieldWeight"]')).not.toBeNull();
    c.form.yieldWeight = 5; c.form.reason = 'Sınav ağırlığı'; c.save();
    const req = http.expectOne(r => r.method === 'PUT'); expect(req.request.body.yieldWeight).toBe(5);
    req.flush({ message: 'Çakışma' }, { status: 409, statusText: 'Conflict' }); http.verify();
  });
  it('retains entered values on conflict and sends the version being edited', () => {
    const { fixture, http, c } = setup(); c.select({ ...topic } as never); http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: topic });
    c.form.name = 'Yeni konu adı'; c.form.reason = 'Yazım düzeltmesi'; c.save();
    const req = http.expectOne(r => r.method === 'PUT'); expect(req.request.body.fingerprint).toBe('current');
    req.flush({ message: 'Kayıt değişmiş; yeniden yükleyin.' }, { status: 409, statusText: 'Conflict' }); fixture.detectChanges();
    expect(c.form.name).toBe('Yeni konu adı'); expect(fixture.nativeElement.textContent).toContain('Kayıt değişmiş'); http.verify();
  });
  it('never submits a move without an explicit destination and review confirmation', () => {
    const { http, c } = setup(); c.select({ ...topic } as never); http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: topic });
    c.move(); http.expectNone(r => r.method === 'POST');
    c.destinationId = 'other-unit'; c.form.reason = 'Üniteyi düzelt'; c.move(); http.expectNone(r => r.method === 'POST'); http.verify();
  });
  it('prevents an in-flight detail response from replacing the next selected record', () => {
    const { http, c } = setup(); c.select({ ...topic } as never); const first = http.expectOne(r => r.url.endsWith('/topics/topic'));
    c.select({ ...topic, id: 'other' } as never); http.expectOne(r => r.url.endsWith('/topics/other')).flush({ data: { ...topic, id: 'other', name: 'Üslü ifadeler' } });
    expect(first.cancelled).toBe(true); expect(c.form.name).toBe('Üslü ifadeler'); http.verify();
  });
  it('keeps the duration transaction busy until its publication finishes', () => {
    const { http,c }=setup(); const outcome={...topic,kind:'outcomes',id:'outcome',mappings:[{topicId:'topic',path:'Matematik / Çarpanlar',isApproved:true,evidence:'İncelendi'}],estimates:[]};
    c.select(outcome as never);http.expectOne(r=>r.url.endsWith('/outcomes/outcome')).flush({data:outcome});
    c.form.reason='Süreyi güncelle';c.learningMinutes=20;c.practiceMinutes=30;c.checkMinutes=10;c.durationEvidence='Öğretmen gözlemi';c.publishDuration=true;c.saveDuration();
    http.expectOne(r=>r.url.endsWith('/outcomes/outcome/estimates')).flush({data:{id:'new-estimate'}});
    const publication=http.expectOne(r=>r.url.endsWith('/estimates/new-estimate/publication'));
    expect(c.busy()).toBe(true);publication.flush({data:{}});http.expectOne(r=>r.url.endsWith('/outcomes/outcome')).flush({data:outcome});
    http.expectOne(r=>r.url.endsWith('/curriculum-management/children')).flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:25}});
    expect(c.busy()).toBe(false);http.verify();
  });
  it('asks before navigating away from an unsaved duration and preserves it when saving outcome text', () => {
    const {http,c}=setup();const outcome={...topic,kind:'outcomes',id:'outcome',mappings:[],estimates:[]};
    c.select(outcome as never);http.expectOne(r=>r.url.endsWith('/outcomes/outcome')).flush({data:outcome});c.learningMinutes=15;
    c.select(topic as never);expect(c.pending()?.id).toBe('topic');http.expectNone(r=>r.url.endsWith('/topics/topic'));c.pending.set(null);
    c.form.name='Yeni kazanım metni';c.form.reason='Yazım düzeltmesi';c.save();
    http.expectOne(r=>r.method==='PUT').flush({data:{...outcome,name:'Yeni kazanım metni'}});
    http.expectOne(r=>r.url.endsWith('/curriculum-management/children')).flush({data:{items:[],totalCount:0,pageNumber:1,pageSize:25}});
    expect(c.learningMinutes).toBe(15);expect(c.dirty()).toBe(true);http.verify();
  });
  it('filters the subtopic tab separately and requests a root topic as its parent', () => {
    const {http,c}=setup();c.chooseTab('subtopics');
    http.expectOne(r=>r.url.endsWith('/children')&&r.params.get('level')==='sub').flush({data:{items:[],totalCount:0}});
    const parents=http.expectOne(r=>r.url.endsWith('/children')&&r.params.get('level')==='root');
    parents.flush({data:{items:[topic],totalCount:1}});
    expect(c.parentOptions()[0].id).toBe('topic');expect(c.createLabel()).toBe('Alt konu');http.verify();
  });
  it('keeps unsaved fields when changing tabs until the user explicitly discards them', () => {
    const {http,c}=setup();c.select(topic as never);http.expectOne(r=>r.url.endsWith('/topics/topic')).flush({data:topic});
    c.form.name='Henüz kaydedilmedi';c.chooseTab('units');expect(c.pendingTab).toBe('units');expect(c.tab).toBe('exams');
    expect(c.form.name).toBe('Henüz kaydedilmedi');c.discardTab();
    http.match(r=>r.url.endsWith('/children')).forEach(r=>r.flush({data:{items:[],totalCount:0}}));
    expect(c.tab).toBe('units');expect(c.editing()).toBe(false);http.verify();
  });
  it('creates a lesson under a database exam and makes creation plus activation a single busy operation', () => {
    const {http,c}=setup();const exam={...topic,kind:'exams',id:'exam',code:'KPSS',name:'KPSS'};
    c.create('lessons',exam as never);
    http.expectOne(r=>r.url.endsWith('/coaching/catalog/exams')).flush({data:[exam]});http.expectOne(r=>r.url.endsWith('/coaching-admin/curricula')).flush({data:[]});
    c.form.name='Türkçe';c.form.reason='KPSS için ders ekle';c.save();
    const request=http.expectOne(r=>r.method==='POST');expect(request.request.body.examCode).toBe('KPSS');expect(request.request.body.parentId).toBe('exam');
    const lesson={...topic,kind:'lessons',id:'new',name:'Türkçe',examCode:'KPSS',parentId:'exam',parentKind:'exams',isActive:false};request.flush({data:lesson});
    expect(c.busy()).toBe(true);const activation=http.expectOne(r=>r.url.endsWith('/lessons/new/status'));expect(activation.request.body.isActive).toBe(true);
    activation.flush({data:{...lesson,isActive:true}});
    http.expectOne(r=>r.url.endsWith('/coaching/catalog/exams')).flush({data:[exam]});http.expectOne(r=>r.url.endsWith('/coaching-admin/curricula')).flush({data:[]});
    http.expectOne(r=>r.url.endsWith('/children')).flush({data:{items:[],totalCount:0}});
    expect(c.busy()).toBe(false);expect(c.selected()?.isActive).toBe(true);http.verify();
  });
  it('opens a created passive lesson after activation fails so retry cannot create a duplicate', () => {
    const {http,c}=setup();c.create('lessons');
    http.expectOne(r=>r.url.endsWith('/coaching/catalog/exams')).flush({data:[]});http.expectOne(r=>r.url.endsWith('/coaching-admin/curricula')).flush({data:[]});
    c.form.name='Türkçe';c.form.reason='Ders ekle';c.save();
    http.expectOne(r=>r.method==='POST').flush({data:{...topic,kind:'lessons',id:'new',name:'Türkçe',isActive:false}});
    http.expectOne(r=>r.url.endsWith('/lessons/new/status')).flush({message:'Üst kayıt pasif'},{status:400,statusText:'Bad Request'});
    http.expectOne(r=>r.url.endsWith('/coaching/catalog/exams')).flush({data:[]});http.expectOne(r=>r.url.endsWith('/coaching-admin/curricula')).flush({data:[]});
    expect(c.creating()).toBe(false);expect(c.selected()?.id).toBe('new');expect(c.error()).toContain('Kayıt oluşturuldu');http.verify();
  });
});
