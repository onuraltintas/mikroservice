import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminAuditService } from './admin-audit.service';

describe('AdminAuditService', () => {
  it('queries the selected service with bounded pagination and filters', () => {
    TestBed.configureTestingModule({
      providers: [AdminAuditService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(AdminAuditService);
    const http = TestBed.inject(HttpTestingController);

    service.getPage('coaching', {
      page: 2,
      pageSize: 25,
      search: '/api/assignments',
      statusCode: 403
    }).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/admin-audit/coaching'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('/api/assignments');
    expect(request.request.params.get('statusCode')).toBe('403');
    request.flush({ items: [], totalCount: 0, page: 2, pageSize: 25 });

    service.getPage('speed-reading', { page: 1, pageSize: 25 }).subscribe();
    const speedReadingRequest = http.expectOne(candidate => candidate.url.endsWith('/admin-audit/speed-reading'));
    expect(speedReadingRequest.request.method).toBe('GET');
    speedReadingRequest.flush({ items: [], totalCount: 0, page: 1, pageSize: 25 });
    http.verify();
  });
});
