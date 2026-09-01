import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            token: 'access-token',
            currentUserValue: { id: 'system-admin' }
          }
        },
        {
          provide: Router,
          useValue: { navigate: jasmine.createSpy('navigate') }
        }
      ]
    });

    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('attaches the access token to authenticated MFA setup requests', () => {
    client.post('/api/auth/mfa/setup-authenticated', { currentPassword: 'secret' }).subscribe();

    const request = http.expectOne('/api/auth/mfa/setup-authenticated');

    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({});
  });

  it('does not attach a stale access token to anonymous login requests', () => {
    client.post('/api/auth/login', { email: 'user@example.com', password: 'secret' }).subscribe();

    const request = http.expectOne('/api/auth/login');

    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });
});
