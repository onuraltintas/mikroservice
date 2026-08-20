import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { IdentityService } from './identity.service';

describe('IdentityService user access management', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [IdentityService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('lists active sessions without exposing refresh token values', () => {
    const service = TestBed.inject(IdentityService);
    service.getUserSessions('user-1').subscribe();

    const request = http.expectOne('/api/users/user-1/sessions');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'session-1', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2026-01-02T00:00:00Z' }]);
  });

  it('supports single-session, all-session and MFA reset actions', () => {
    const service = TestBed.inject(IdentityService);

    service.revokeUserSession('user-1', 'session-1').subscribe();
    expect(http.expectOne('/api/users/user-1/sessions/session-1').request.method).toBe('DELETE');

    service.revokeAllUserSessions('user-1').subscribe();
    expect(http.expectOne('/api/users/user-1/sessions').request.method).toBe('DELETE');

    service.resetUserMfa('user-1').subscribe();
    expect(http.expectOne('/api/users/user-1/mfa/reset').request.method).toBe('POST');
  });
});
