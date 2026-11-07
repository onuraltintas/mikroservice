import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { OAuthService } from 'angular-oauth2-oidc';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, hasManagementRole, NO_ROLE_FOR_PRODUCT_CODE } from './auth.service';

describe('AuthService browser session security', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    const oauthEvents = new Subject<{ type: string }>();

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: OAuthService,
          useValue: {
            events: oauthEvents,
            configure: vi.fn(),
            setStorage: vi.fn(),
            hasValidAccessToken: vi.fn(() => false),
            getAccessToken: vi.fn(() => ''),
            getIdentityClaims: vi.fn(() => null),
            initLoginFlow: vi.fn(),
            logOut: vi.fn()
          }
        },
        { provide: SocialAuthService, useValue: { signOut: vi.fn().mockResolvedValue(undefined) } },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } }
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('keeps login tokens out of localStorage and sends cookie credentials', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(
      { error: 'no session' },
      { status: 401, statusText: 'Unauthorized' });
    const loginPromise = service.loginWithPassword(
      'admin@example.test',
      'Correct-Password-1!',
      false);

        const request = http.expectOne(req => req.url.endsWith('/auth/coaching/login'));
    expect(request.request.withCredentials).toBe(true);
    request.flush({
      accessToken: createToken('user-a'),
      tokenType: 'Bearer',
      expiresInMinutes: 15
    });
    await loginPromise;

    expect(service.getToken()).not.toBe('');
    expect(localStorage.getItem('access_token')).toBeNull();
    expect(localStorage.getItem('refresh_token')).toBeNull();
    expect(localStorage.getItem('expires_at')).toBeNull();
  });

  it('uses the coaching-scoped Google login endpoint', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(
      { error: 'no session' },
      { status: 401, statusText: 'Unauthorized' });

    const loginPromise = service.loginWithGoogle('google-id-token');
    const request = http.expectOne(req => req.url.endsWith('/auth/coaching/google-login'));
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.body).toEqual({ idToken: 'google-id-token', legalAcceptances: [], utm: null });
    request.flush({
      accessToken: createToken('coaching-user'),
      tokenType: 'Bearer',
      expiresInMinutes: 15
    });

    const result = await loginPromise;
    expect(result.authenticated).toBe(true);
  });

  it('restores an access token from the HttpOnly refresh cookie on startup', async () => {
    const service = TestBed.inject(AuthService);

    const request = http.expectOne(req => req.url.endsWith('/auth/refresh-token'));
    expect(request.request.withCredentials).toBe(true);
    request.flush({
      accessToken: createToken('restored-user'),
      tokenType: 'Bearer',
      expiresInMinutes: 15
    });

    await service.waitForAuth();
    expect(service.isAuthenticated()).toBe(true);
    expect(service.userProfile()?.id).toBe('restored-user');
  });

  it('keeps an MFA challenge outside authenticated session state', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(
      { error: 'no session' },
      { status: 401, statusText: 'Unauthorized' });

    const loginPromise = service.loginWithPassword('admin@example.test', 'password', true);
    http.expectOne(req => req.url.endsWith('/auth/coaching/login')).flush({
      requiresMfa: true,
      mfaEnrollmentRequired: true,
      mfaChallengeToken: 'challenge'
    });

    const result = await loginPromise;
    expect(result.requiresMfa).toBe(true);
    expect(result.mfaEnrollmentRequired).toBe(true);
    expect(result.mfaChallengeToken).toBe('challenge');
    expect(service.isAuthenticated()).toBe(false);
  });

  it('links a Google account using the authenticated cookie session', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(
      { error: 'no session' },
      { status: 401, statusText: 'Unauthorized' });

    const linkPromise = service.linkGoogleAccount('google-id-token');
    const request = http.expectOne(req => req.url.endsWith('/auth/google-link'));
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.body).toEqual({ idToken: 'google-id-token' });
    request.flush({});

    await linkPromise;
  });

  it('completes MFA verification using the HttpOnly refresh cookie', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(
      { error: 'no session' },
      { status: 401, statusText: 'Unauthorized' });

    const verifyPromise = service.verifyMfa('challenge', '123456');
    const request = http.expectOne(req => req.url.endsWith('/auth/mfa/verify'));
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.body).toEqual({
      challengeToken: 'challenge',
      code: '123456',
      recoveryCode: null
    });
    request.flush({
      accessToken: createToken('mfa-admin'),
      tokenType: 'Bearer',
      expiresInMinutes: 15
    });

    await verifyPromise;
    expect(service.isAuthenticated()).toBe(true);
    expect(service.userProfile()?.id).toBe('mfa-admin');
  });

  // PR #111 second review H1: a resend names the Coaching product so the e-mail and link are the Coaching ones.
  it('asks for a Coaching verification e-mail when resending', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(null, { status: 401, statusText: 'Unauthorized' });
    const pending = service.resendVerificationEmail('veli@example.test');
    const request = http.expectOne('/api/auth/resend-verification-email');
    expect(request.request.body).toEqual({ email: 'veli@example.test', product: 'coaching' });
    request.flush({});
    await pending;
  });

  // Paket F F4 (live 07.11.2026): a Koçluk token with roles: [] opened the /dashboard management shell.
  it('ends a role-less session after sign-in instead of opening the management shell', async () => {
    const service = TestBed.inject(AuthService);
    const router = TestBed.inject(Router) as unknown as { navigate: ReturnType<typeof vi.fn> };
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(null, { status: 401, statusText: 'Unauthorized' });

    const pending = service.loginWithPassword('student@example.test', 'password', false);
    const outcome = pending.then(() => null, (error: unknown) => error);
    http.expectOne(req => req.url.endsWith('/auth/coaching/login')).flush({
      accessToken: createToken('role-less', [], 'coaching'),
      tokenType: 'Bearer',
      expiresInMinutes: 15
    });
    await vi.waitFor(() => http.expectOne(req => req.url.endsWith('/auth/revoke-token')).flush({}));

    expect(await outcome).toMatchObject({ error: { code: NO_ROLE_FOR_PRODUCT_CODE } });
    expect(service.isAuthenticated()).toBe(false);
    expect(router.navigate).not.toHaveBeenCalledWith(['/dashboard']);
  });

  it('lands a Koçluk student in the portal and staff in the management shell', async () => {
    const service = TestBed.inject(AuthService);
    const router = TestBed.inject(Router) as unknown as { navigate: ReturnType<typeof vi.fn> };
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(null, { status: 401, statusText: 'Unauthorized' });

    const student = service.loginWithPassword('student@example.test', 'password', false);
    http.expectOne(req => req.url.endsWith('/auth/coaching/login')).flush({
      accessToken: createToken('student', ['Student'], 'coaching'), tokenType: 'Bearer', expiresInMinutes: 15 });
    await student;
    expect(router.navigate).toHaveBeenLastCalledWith(['/coaching-portal']);

    const staff = service.loginWithPassword('editor@example.test', 'password', false);
    http.expectOne(req => req.url.endsWith('/auth/coaching/login')).flush({
      accessToken: createToken('editor', ['Editor'], 'coaching'), tokenType: 'Bearer', expiresInMinutes: 15 });
    await staff;
    expect(router.navigate).toHaveBeenLastCalledWith(['/dashboard']);
  });

  // Third review N2: the confirmation names Coaching so the welcome e-mail is the Coaching one.
  it('confirms the e-mail address for Coaching', async () => {
    const service = TestBed.inject(AuthService);
    http.expectOne(req => req.url.endsWith('/auth/refresh-token')).flush(null, { status: 401, statusText: 'Unauthorized' });
    const pending = service.confirmEmail('user-id', 'token');
    const request = http.expectOne('/api/auth/confirm-email');
    expect(request.request.body).toEqual({ userId: 'user-id', token: 'token', product: 'coaching' });
    request.flush({});
    await pending;
  });
});

function createToken(userId: string, roles: string[] = ['SystemAdmin'], product?: string): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    sub: userId,
    email: `${userId}@example.test`,
    role: roles,
    permission: [],
    ...(product ? { platform_product: product } : {})
  }));
  return `${header}.${payload}.signature`;
}

describe('hasManagementRole', () => {
  it('accepts any role other than the Koçluk portal roles', () => {
    for (const role of ['SystemAdmin', 'Editor', 'InstitutionAdmin', 'InstitutionOwner', 'ContentManager']) {
      expect(hasManagementRole({ roles: [role] })).toBe(true);
    }
    expect(hasManagementRole({ roles: ['Student', 'InstitutionAdmin'] })).toBe(true);
  });

  it('rejects portal-only and role-less sessions', () => {
    expect(hasManagementRole({ roles: [] })).toBe(false);
    expect(hasManagementRole({ roles: ['Student'] })).toBe(false);
    expect(hasManagementRole({ roles: ['Teacher', 'Parent'] })).toBe(false);
    expect(hasManagementRole(null)).toBe(false);
  });
});
