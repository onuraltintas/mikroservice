import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StaffAuthService } from './staff-auth.service';

describe('StaffAuthService product-scoped sessions', () => {
  let http: HttpTestingController;
  let service: StaffAuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StaffAuthService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(StaffAuthService);
  });

  afterEach(() => http.verify());

  it('logs into the selected product and loads only server-authorized staff products', async () => {
    const login = service.login('coaching', 'teacher@example.com', 'correct horse battery staple');
    const loginRequest = http.expectOne('/api/auth/coaching/login');
    expect(loginRequest.request.withCredentials).toBe(true);
    expect(loginRequest.request.body).toEqual({
      email: 'teacher@example.com',
      password: 'correct horse battery staple',
      rememberMe: true
    });
    loginRequest.flush({ accessToken: 'coaching-access', expiresInMinutes: 15, tokenType: 'Bearer' });

    const productsRequest = http.expectOne('/api/auth/staff-session/products');
    productsRequest.flush([
      { product: 'coaching', roles: ['Teacher'] },
      { product: 'speed-reading', roles: ['InstitutionAdmin'] }
    ]);
    await login;

    expect(service.activeProduct()).toBe('coaching');
    expect(service.products()).toEqual([
      { product: 'coaching', roles: ['Teacher'] },
      { product: 'speed-reading', roles: ['InstitutionAdmin'] }
    ]);
  });

  it('rotates into the selected product session without persisting access tokens', async () => {
    const login = service.login('coaching', 'teacher@example.com', 'password');
    http.expectOne('/api/auth/coaching/login').flush({
      accessToken: 'coaching-access', expiresInMinutes: 15, tokenType: 'Bearer'
    });
    http.expectOne('/api/auth/staff-session/products').flush([
      { product: 'coaching', roles: ['Teacher'] },
      { product: 'speed-reading', roles: ['Teacher'] }
    ]);
    await login;

    const switching = service.switchProduct('speed-reading');
    const switchRequest = http.expectOne('/api/auth/staff-session/switch/speed-reading');
    expect(switchRequest.request.method).toBe('POST');
    expect(switchRequest.request.withCredentials).toBe(true);
    switchRequest.flush({ accessToken: 'speed-reading-access', expiresInMinutes: 15, tokenType: 'Bearer' });
    await switching;

    expect(service.activeProduct()).toBe('speed-reading');
    expect(service.getAccessToken()).toBe('speed-reading-access');
  });
});
