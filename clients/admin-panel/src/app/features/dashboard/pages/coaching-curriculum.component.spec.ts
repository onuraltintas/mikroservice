import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingCurriculumComponent } from './coaching-curriculum.component';

describe('Coaching curriculum administration', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [CoachingCurriculumComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingCurriculumComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/coaching-admin/curricula').flush({ data: [] });
    http.expectOne(r => r.url === '/api/coaching-admin/catalog/lessons').flush({ data: { items: [], totalCount: 60 } });
    return { fixture, http, c: fixture.componentInstance };
  }
  it('loads version metadata and creates a draft with selected lessons and a reason', () => {
    TestBed.configureTestingModule({ imports: [CoachingCurriculumComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingCurriculumComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/coaching-admin/curricula').flush({ data: [] });
    http.expectOne(r => r.url === '/api/coaching-admin/catalog/lessons').flush({ data: { items: [{ id: 'lesson', name: 'Matematik', curriculumVersionId: null }], totalCount: 1, pageNumber: 1, pageSize: 25 } });
    const c = fixture.componentInstance; c.code = 'test'; c.name = 'Test'; c.versionLabel = 'v1'; c.source = 'Örnek kaynak'; c.reason = 'Test programı oluştur'; c.selected = ['lesson'];
    c.create();
    const request = http.expectOne('/api/coaching-admin/curricula');
    expect(request.request.method).toBe('POST'); expect(request.request.body.lessonIds).toEqual(['lesson']);
    request.flush({ data: { id: 'version', version: 0, isPublished: false } });
    http.expectOne('/api/coaching-admin/curricula').flush({ data: [] });
    http.expectOne(r => r.url === '/api/coaching-admin/catalog/lessons').flush({ data: { items: [], totalCount: 0 } });
    http.verify();
  });
  it('preserves inputs and selections after a create failure', () => {
    const { c, http } = setup(); c.code = 'keep'; c.reason = 'Test gerekçesi'; c.selected = ['lesson'];
    c.create(); c.create();
    http.expectOne('/api/coaching-admin/curricula').flush({ message: 'Program değişti' }, { status: 409, statusText: 'Conflict' });
    expect(c.code).toBe('keep'); expect(c.selected).toEqual(['lesson']); expect(c.error()).toBe('Program değişti'); expect(c.saving()).toBe(false); http.verify();
  });
  it('publishes the exact reviewed version and refreshes after success', () => {
    const { c, http } = setup(); c.reason = 'Yayın gerekçesi';
    c.publish({ id: 'version', version: 3, isPublished: false } as any);
    const request = http.expectOne('/api/coaching-admin/curricula/version/publication');
    expect(request.request.body).toMatchObject({ expectedVersion: 3, isPublished: true }); request.flush({ data: {} });
    http.expectOne('/api/coaching-admin/curricula').flush({ data: [] });
    http.expectOne(r => r.url.endsWith('/catalog/lessons')).flush({ data: { items: [], totalCount: 0 } }); http.verify();
  });
  it('loads paginated lesson choices while retaining choices from other pages', () => {
    const { c, http } = setup(); c.toggle('lesson'); c.search = 'Matematik'; c.loadLessons(2);
    const request = http.expectOne(r => r.url.endsWith('/catalog/lessons'));
    expect(request.request.params.get('pageNumber')).toBe('2'); expect(request.request.params.get('search')).toBe('Matematik');
    request.flush({ data: { items: [], totalCount: 60 } }); expect(c.selected).toEqual(['lesson']); expect(c.page).toBe(2);
    c.toggle('lesson'); expect(c.selected).toEqual([]);
    c.loadLessons(3); http.expectOne(r => r.url.endsWith('/catalog/lessons')).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(c.error()).toContain('Ders seçenekleri'); http.verify();
  });
  it('does not write without a reason or selected lessons and exposes reload failure', () => {
    const { c, http } = setup(); c.create(); c.publish({ id: 'version' } as any); http.expectNone('/api/coaching-admin/curricula');
    c.load(); http.expectOne('/api/coaching-admin/curricula').flush({}, { status: 503, statusText: 'Unavailable' });
    const cancelled = http.match(r => r.url.endsWith('/catalog/lessons')); expect(cancelled).toHaveLength(1);
    expect(c.ready()).toBe(false); expect(c.error()).toContain('yüklenemedi'); http.verify();
  });
});
