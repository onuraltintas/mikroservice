import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingCatalogService } from './coaching-catalog.service';
import { environment } from '../../../environments/environment';

describe('CoachingCatalogService', () => {
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
