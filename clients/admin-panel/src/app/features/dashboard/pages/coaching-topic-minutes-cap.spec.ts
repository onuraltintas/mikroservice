import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CoachingCurriculumManagerComponent } from './coaching-curriculum-manager.component';
import { CoachingOutcomesComponent } from './coaching-outcomes.component';
import { KM7A_REAL, KM7A_STUBS } from './coaching-km7a-embeds.testing';
import { MAX_DAY_MINUTES, MAX_TOPIC_MINUTES } from '../../../shared/coaching-study-limits';

// User decision (01.11.2026, "sınırı yükselt"): a topic or an outcome may need up to 4320 minutes (72 saat). A study
// task still fits in one day (1440), so the planner places a large topic on several days.
describe('Konu süresi sınırı 4320 dakika', () => {
  it('names the limits once', () => {
    expect(MAX_TOPIC_MINUTES).toBe(4320); expect(MAX_DAY_MINUTES).toBe(1440);
  });

  function manager() {
    TestBed.configureTestingModule({ imports: [CoachingCurriculumManagerComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    TestBed.overrideComponent(CoachingCurriculumManagerComponent, { remove: { imports: KM7A_REAL }, add: { imports: KM7A_STUBS } });
    const fixture = TestBed.createComponent(CoachingCurriculumManagerComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/curriculum-management/children')).flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } });
    http.expectOne(r => r.url.endsWith('/coaching/catalog/exams')).flush({ data: [] });
    http.expectOne(r => r.url.endsWith('/outcomes/mappings')).flush({ data: { items: [], totalCount: 0 } });
    return { fixture, http, c: fixture.componentInstance };
  }
  const topic = { kind: 'topics', id: 'topic', name: 'Analiz', path: 'ÖABT / İlköğretim Matematik / Analiz', parentId: 'unit', parentKind: 'units', isActive: true,
    fingerprint: 'current', version: 'v1', estimatedMinutes: 1440, yieldWeight: 5, canDelete: false, blockers: [], children: 0, outcomes: [], estimates: [] };

  it('Müfredat Yönetimi inline edit accepts 4320 minutes and refuses 4321', () => {
    const { fixture, c } = manager();
    c.editMinutes(topic as never, 4320); expect(c.inlineInvalid()).toBe(false);
    c.editMinutes(topic as never, 4321); expect(c.inlineInvalid()).toBe(true);
    c.inlineReason = 'KM3 süreleri'; c.saveInline();
    expect(c.error()).toContain('4320');
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[name="rowMinutes"]') as HTMLInputElement | null;
    if (input) expect(input.max).toBe('4320');
  });

  it('the topic form sends up to 4320 minutes and refuses more without a request', () => {
    const { fixture, http, c } = manager(); c.select({ ...topic } as never);
    http.expectOne(r => r.url.endsWith('/topics/topic')).flush({ data: topic }); fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('input[name="minutes"]') as HTMLInputElement).max).toBe('4320');
    c.form.estimatedMinutes = 4321; c.form.reason = 'KM3 süreleri'; c.save();
    http.expectNone(r => r.method === 'PUT'); expect(c.error()).toContain('4320');
    c.form.estimatedMinutes = 3960; c.save();
    expect(http.expectOne(r => r.method === 'PUT').request.body.estimatedMinutes).toBe(3960); http.verify();
  });

  it('an outcome duration version may total 4320 minutes but not more', () => {
    const { fixture, http, c } = manager();
    const outcome = { ...topic, kind: 'outcomes', id: 'outcome', mappings: [{ topicId: 'topic', path: 'Analiz', isApproved: true, status: 'approved', evidence: 'İncelendi' }], estimates: [] };
    c.select(outcome as never); http.expectOne(r => r.url.endsWith('/outcomes/outcome')).flush({ data: outcome }); fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('input[name="learning"]') as HTMLInputElement).max).toBe('4320');
    c.form.reason = 'KM3 süreleri'; c.durationEvidence = 'ÖSYM payı'; c.learningMinutes = 2600; c.practiceMinutes = 1300; c.checkMinutes = 421;
    c.saveDuration(); http.expectNone(r => r.url.endsWith('/estimates')); expect(c.error()).toContain('1–4320');
    c.checkMinutes = 420; c.saveDuration();
    const req = http.expectOne(r => r.url.endsWith('/outcomes/outcome/estimates'));
    expect(req.request.body.learningMinutes + req.request.body.practiceMinutes + req.request.body.checkMinutes).toBe(4320);
  });

  it('the outcome review screen allows a 4320-minute estimate', () => {
    TestBed.configureTestingModule({ imports: [CoachingOutcomesComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const f = TestBed.createComponent(CoachingOutcomesComponent); f.detectChanges(); const http = TestBed.inject(HttpTestingController); const c = f.componentInstance;
    http.expectOne('/api/coaching-admin/outcomes').flush({ data: [] });
    c.outcomeId = 'o'; c.reason = 'KM3 süreleri'; c.durationEvidence = 'ÖSYM payı';
    c.learningMinutes = 4000; c.practiceMinutes = 321; c.checkMinutes = 0; c.saveEstimate();
    http.expectNone('/api/coaching-admin/outcomes/o/estimates'); expect(c.error()).toBe('Toplam süre 1–4320 dakika olmalıdır.');
    c.practiceMinutes = 320; c.saveEstimate();
    expect(http.expectOne('/api/coaching-admin/outcomes/o/estimates').request.body.learningMinutes).toBe(4000);
  });
});
