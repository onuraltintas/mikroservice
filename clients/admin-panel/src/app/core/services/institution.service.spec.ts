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
});
