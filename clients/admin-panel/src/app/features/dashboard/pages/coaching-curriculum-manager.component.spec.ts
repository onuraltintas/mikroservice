import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CoachingCurriculumManagerComponent } from './coaching-curriculum-manager.component';

describe('Coaching curriculum manager', () => {
  const topic = { kind: 'topics', id: 'topic', name: 'Çarpanlar', path: 'Sayısal / Matematik / Sayılar / Çarpanlar', parentId: 'unit', parentKind: 'units', isActive: true,
    fingerprint: 'current', estimatedMinutes: 60, canDelete: false, blockers: ['1 çalışma kaydı'], children: 0, outcomes: [], estimates: [] };
  function setup() {
    TestBed.configureTestingModule({ imports: [CoachingCurriculumManagerComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(CoachingCurriculumManagerComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/curriculum-management/children')).flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } });
    return { fixture, http, c: fixture.componentInstance };
  }
  it('shows a readable empty hierarchy and an accessible create action', () => {
    const { fixture, http } = setup(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ders ve Müfredat Yönetimi');
    expect(fixture.nativeElement.textContent).toContain('Henüz kayıt yok');
    expect(fixture.nativeElement.querySelector('button[aria-label="Ders grubu ekle"]')).not.toBeNull(); http.verify();
  });
  it('shows usage blockers and forbids deletion of a used topic', () => {
    const { fixture, http, c } = setup(); c.select({ ...topic } as never);
    http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: topic }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('1 çalışma kaydı');
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(x => x.textContent?.trim() === 'Kalıcı sil');
    expect(button?.disabled).toBe(true); c.remove(); http.expectNone(r => r.method === 'DELETE'); http.verify();
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
});
