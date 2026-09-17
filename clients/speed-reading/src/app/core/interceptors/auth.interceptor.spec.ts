import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';
import { SKIP_AUTH_REFRESH } from './http-context.tokens';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let refreshToken: jasmine.Spy;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            token: 'access-token',
            currentUserValue: { id: 'system-admin' },
            refreshToken: refreshToken = jasmine.createSpy('refreshToken')
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

  it('does not start a recursive refresh for a profile hydration request', () => {
    let receivedError: unknown;

    client.get('/api/v1/users/me', {
      context: new HttpContext().set(SKIP_AUTH_REFRESH, true)
    }).subscribe({ error: error => receivedError = error });

    const request = http.expectOne('/api/v1/users/me');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(refreshToken).not.toHaveBeenCalled();
    expect((receivedError as { status?: number })?.status).toBe(401);
  });
});
