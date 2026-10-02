import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingCatalogService } from './coaching-catalog.service';
import { environment } from '../../../environments/environment';

describe('CoachingCatalogService', () => {
  it('supports detail, create, update and explicit status with the same typed envelope', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(CoachingCatalogService);
    const data = { fingerprint: 'hash', data: { id: 'record', name: 'Math', source: 'admin-manual', sourceId: '1', isActive: false } };
    let received = '';
    service.get('lessons', 'record').subscribe(result => received = result.data.name);
    http.expectOne(`${environment.apiUrl}/coaching-admin/catalog/lessons/record`).flush({ data });
    expect(received).toBe('Math');
    const body = { name: 'Math', reason: 'Yeni kayıt' };
    service.create('lessons', body).subscribe();
    const post = http.expectOne(`${environment.apiUrl}/coaching-admin/catalog/lessons`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(body);
    post.flush({ data });
    service.update('lessons', 'record', { ...body, fingerprint: 'hash' }).subscribe();
    const put = http.expectOne(`${environment.apiUrl}/coaching-admin/catalog/lessons/record`);
    expect(put.request.method).toBe('PUT');
    put.flush({ data });
    service.setActive('lessons', 'record', { fingerprint: 'hash', reason: 'Yayın onayı', isActive: true }).subscribe();
    const patch = http.expectOne(`${environment.apiUrl}/coaching-admin/catalog/lessons/record/status`);
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.body.isActive).toBe(true);
    patch.flush({ data });
    http.verify();
  });
  it('sends permanent deletion as DELETE with fingerprint, reason and record confirmation', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const body = { fingerprint: 'fingerprint', reason: 'Unused duplicate', confirmId: 'record' };
    TestBed.inject(CoachingCatalogService).delete('topics', 'record', body).subscribe();
    const request = http.expectOne(`${environment.apiUrl}/coaching-admin/catalog/topics/record`);
    expect(request.request.method).toBe('DELETE');
    expect(request.request.body).toEqual(body);
    request.flush({ success: true });
    http.verify();
  });
  it('preserves false active filters and unwraps the API envelope', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(CoachingCatalogService);
    let count = -1;
    service.list('schools', { pageNumber: 2, pageSize: 25, isActive: false, search: '', scoreYear: undefined })
      .subscribe(result => count = result.totalCount);
    const request = http.expectOne(request => request.url === `${environment.apiUrl}/coaching-admin/catalog/schools`);
    expect(request.request.params.get('isActive')).toBe('false');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('scoreYear')).toBe(false);
    request.flush({ success: true, data: { items: [], totalCount: 26, pageNumber: 2, pageSize: 25 } });
    expect(count).toBe(26);
    http.verify();
  });
});
