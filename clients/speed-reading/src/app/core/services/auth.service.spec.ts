import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthResponse } from '../../core/models/user.model';
import { SettingsService } from './settings.service';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: { navigate: jasmine.createSpy('navigate') }
        },
        {
          provide: SettingsService,
          useValue: { loadSettings: jasmine.createSpy('loadSettings').and.returnValue(of(void 0)) }
        }
      ]
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');
    http.verify();
  });

  it('recognizes SystemAdmin as administrative access', () => {
    const systemAdmin = {
      id: 'system-admin',
      token: '',
      refreshToken: '',
      email: 'admin@example.com',
      firstName: 'System',
      lastName: 'Admin',
      roles: ['SystemAdmin']
    } as AuthResponse;

    (service as any).currentUserSubject.next(systemAdmin);

    expect(service.hasAdminAccess()).toBeTrue();
  });

  it('does not treat an editor as a full administrative user', () => {
    const editor = {
      id: 'editor',
      token: '',
      refreshToken: '',
      email: 'editor@example.com',
      firstName: 'Content',
      lastName: 'Editor',
      roles: ['Editor']
    } as AuthResponse;

    (service as any).currentUserSubject.next(editor);

    expect(service.hasAdminAccess()).toBeFalse();
  });

  it('uses the canonical auth endpoint for login and sends the session cookie', () => {
    service.login({ email: 'admin@example.com', password: 'Password1!' }).subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({
      accessToken: '',
      roles: []
    });
  });

  it('refreshes through the HttpOnly cookie without a client-side refresh token', () => {
    service.refreshToken().subscribe();

    const request = http.expectOne('/api/auth/refresh-token');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({});
    request.flush({
      accessToken: '',
      roles: []
    });
  });

  it('revokes the session through the backend revoke endpoint on logout', () => {
    const user = {
      id: 'user',
      token: '',
      refreshToken: '',
      email: 'user@example.com',
      firstName: 'Test',
      lastName: 'User',
      roles: ['Student']
    } as AuthResponse;
    (service as any).currentUserSubject.next(user);

    service.logout();

    const request = http.expectOne('/api/auth/revoke-token');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({});
  });

  it('does not persist the access token in browser storage after login', () => {
    const accessToken = 'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJ1c2VyIiwicm9sZSI6IlN5c3RlbUFkbWluIiwiZXhwIjo0MTAyNDQ0ODAwfQ.';
    service.login({ email: 'admin@example.com', password: 'Password1!' }).subscribe();

    const request = http.expectOne('/api/auth/login');
    request.flush({
      accessToken,
      roles: ['SystemAdmin']
    });

    expect(localStorage.getItem('token')).toBeNull();
    expect(JSON.parse(localStorage.getItem('currentUser') || '{}').token).toBeUndefined();
  });

  it('restores the session from the HttpOnly cookie during application initialization', () => {
    const accessToken = 'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJ1c2VyIiwicm9sZSI6IlN5c3RlbUFkbWluIiwiZXhwIjo0MTAyNDQ0ODAwfQ.';
    service.initializeSession().subscribe(user => expect(user?.roles).toEqual(['SystemAdmin']));

    const request = http.expectOne('/api/auth/refresh-token');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({
      accessToken,
      roles: ['SystemAdmin']
    });

    expect(service.isAuthenticated).toBeTrue();
    expect(localStorage.getItem('token')).toBeNull();
    expect(JSON.parse(localStorage.getItem('currentUser') || '{}').token).toBeUndefined();
  });

  it('starts authenticated MFA setup with the current password', () => {
    service.startAuthenticatedMfaSetup('CurrentPassword1!').subscribe(response => {
      expect(response.setupToken).toBe('setup-token');
      expect(response.challengeToken).toBe('challenge-token');
    });

    const request = http.expectOne('/api/auth/mfa/setup-authenticated');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ currentPassword: 'CurrentPassword1!' });
    request.flush({
      secret: 'BASE32SECRET',
      otpAuthUri: 'otpauth://totp/EduPlatform:admin@example.com',
      setupToken: 'setup-token',
      challengeToken: 'challenge-token'
    });
  });

  it('enables MFA and promotes the returned access token to the active session', () => {
    const currentUser = {
      id: 'system-admin',
      token: 'old-token',
      refreshToken: '',
      email: 'admin@example.com',
      firstName: 'System',
      lastName: 'Admin',
      roles: ['SystemAdmin']
    } as AuthResponse;
    (service as any).currentUserSubject.next(currentUser);
    (service as any).accessToken = 'old-token';

    service.enableMfa('challenge-token', 'setup-token', '123456').subscribe(recoveryCodes => {
      expect(recoveryCodes).toEqual(['RECOVERY-ONE']);
      expect(service.token).toBeTruthy();
    });

    const request = http.expectOne('/api/auth/mfa/enable');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({
      challengeToken: 'challenge-token',
      setupToken: 'setup-token',
      code: '123456'
    });
    request.flush({
      accessToken: 'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJzeXN0ZW0tYWRtaW4iLCJyb2xlIjoiU3lzdGVtQWRtaW4iLCJleHAiOjQxMDI0NDQ4MDB9.',
      tokenType: 'Bearer',
      expiresInMinutes: 15,
      recoveryCodes: ['RECOVERY-ONE']
    });
  });
});
