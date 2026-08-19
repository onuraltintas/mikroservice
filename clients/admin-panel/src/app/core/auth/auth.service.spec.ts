import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { OAuthService } from 'angular-oauth2-oidc';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from './auth.service';

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
    const loginPromise = service.loginWithPassword(
      'admin@example.test',
      'Correct-Password-1!',
      false);

    const request = http.expectOne(req => req.url.endsWith('/auth/login'));
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
});

function createToken(userId: string): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    sub: userId,
    email: `${userId}@example.test`,
    role: ['SystemAdmin'],
    permission: []
  }));
  return `${header}.${payload}.signature`;
}
