import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingCurriculumComponent } from './coaching-curriculum.component';

describe('Coaching curriculum administration', () => {
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
});
