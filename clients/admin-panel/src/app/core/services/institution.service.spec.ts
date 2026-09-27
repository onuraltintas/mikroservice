import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InstitutionService } from './institution.service';

describe('InstitutionService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [InstitutionService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('adds an idempotency key to institution creation requests', () => {
    TestBed.inject(InstitutionService).create({ name: 'Test School', type: 1 }).subscribe();

    const request = http.expectOne('/api/institutions');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    request.flush({ institutionId: 'institution-1' });
  });

  it('keeps institution administrator reads and writes product-scoped', () => {
    const service = TestBed.inject(InstitutionService);

    service.assignAdmin('institution-1', 'user-1', 2, 2).subscribe();
    const assignRequest = http.expectOne('/api/institutions/institution-1/admins');
    expect(assignRequest.request.body).toEqual({ userId: 'user-1', role: 2, product: 2 });
    assignRequest.flush(null);

    service.getAdmins('institution-1', 2).subscribe();
    const readRequest = http.expectOne('/api/institutions/institution-1/admins?product=2');
    expect(readRequest.request.method).toBe('GET');
    readRequest.flush([]);

    service.setAdminActive('institution-1', 'user-1', false, 2).subscribe();
    const activeRequest = http.expectOne('/api/institutions/institution-1/admins/user-1/active');
    expect(activeRequest.request.body).toEqual({ isActive: false, product: 2 });
    activeRequest.flush(null);
  });
});
