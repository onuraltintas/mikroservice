import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CoachingCurriculumManagerService, chunk } from './coaching-curriculum-manager.service';
import { examLabel, label, CONFIDENCE_LABELS, sourceLabel } from './coaching-curriculum-labels';

describe('CoachingCurriculumManagerService (KM7 contract)', () => {
  function setup() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { api: TestBed.inject(CoachingCurriculumManagerService), http: TestBed.inject(HttpTestingController) };
  }
  it('posts bulk status, topic patches and link decisions to the contract routes', () => {
    const { api, http } = setup();
    api.bulkStatus({ kind: 'lessons', ids: ['l1'], active: true, includeDescendants: true, reason: 'KM3 yayını' }).subscribe();
    expect(http.expectOne(r => r.method === 'POST' && r.url.endsWith('/coaching-admin/curriculum-management/bulk/status')).request.body.includeDescendants).toBe(true);
    api.patchTopics([{ id: 't', version: 'v', estimatedMinutes: 90, yieldWeight: 4 }], 'Süre düzeltmesi').subscribe();
    expect(http.expectOne(r => r.method === 'PATCH' && r.url.endsWith('/curriculum-management/topics/bulk')).request.body.items.length).toBe(1);
    api.reviewMappings([{ topicId: 't', outcomeId: 'o' }], 'reject', 'Yanlış eşleşme').subscribe();
    expect(http.expectOne(r => r.url.endsWith('/coaching-admin/outcomes/mappings/bulk')).request.body.decision).toBe('reject');
    api.deleteMapping({ topicId: 't', outcomeId: 'o' }, 'Yanlış eşleşme').subscribe();
    expect(http.expectOne(r => r.method === 'DELETE' && r.url.endsWith('/outcomes/mappings/t/o')).request.body).toEqual({ reason: 'Yanlış eşleşme' });
    http.verify();
  });
  it('omits empty quality filters and reads the pending count from totalCount', () => {
    const { api, http } = setup(); let count = 0;
    api.quality({ examCode: 'AYT', lessonId: '', issue: 'noMinutes', page: 1, pageSize: 50 }).subscribe();
    const q = http.expectOne(r => r.url.endsWith('/curriculum-management/quality'));
    expect(q.request.params.has('lessonId')).toBe(false); expect(q.request.params.get('issue')).toBe('noMinutes');
    api.pendingCount().subscribe(x => count = x);
    http.expectOne(r => r.url.endsWith('/outcomes/mappings') && r.params.get('status') === 'pending').flush({ data: { items: [], totalCount: 206 } });
    expect(count).toBe(206);
  });
  it('splits large selections into request-sized chunks', () => {
    expect(chunk(Array.from({ length: 1201 }, (_, i) => i), 500).map(x => x.length)).toEqual([500, 500, 201]);
  });
  it('maps codes to Turkish labels and never shows a raw code', () => {
    expect(label(CONFIDENCE_LABELS, 'Medium')).toBe('Orta');
    expect(sourceLabel('coaching-catalog-km3')).toContain('Koçluk kataloğu');
    expect(sourceLabel('some-new-import')).toBe('Diğer kaynak');
    expect(examLabel('OABT_TURKCE', [{ code: 'OABT_TURKCE', name: 'ÖABT · Türkçe Öğretmenliği' }])).toBe('ÖABT · Türkçe Öğretmenliği');
    expect(examLabel('YOKDIL')).toBe('YÖKDİL');
  });
});
