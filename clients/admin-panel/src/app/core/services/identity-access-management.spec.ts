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

  it('updates the role-specific profile through the admin endpoint', () => {
    const service = TestBed.inject(IdentityService);
    const profile = {
      firstName: 'Ada',
      lastName: 'Lovelace',
      phoneNumber: '+905551112233',
      teacherTitle: 'Matematik Öğretmeni',
      teacherExperienceYears: 8,
      teacherSubjects: ['Matematik'],
      institutionId: 'institution-1'
    };

    service.updateUserProfile('user-1', profile).subscribe();

    const request = http.expectOne('/api/users/user-1/profile');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(profile);
    request.flush(null);
  });

  it('uses the server-side bulk user contracts for template, import, export and role assignment', () => {
    const service = TestBed.inject(IdentityService);
    const file = new File(['firstName,lastName,email,phoneNumber,role\nAda,Lovelace,ada@example.com,,Editor\n'], 'users.csv', { type: 'text/csv' });

    service.getBulkUserImportTemplate().subscribe();
    expect(http.expectOne('/api/users/bulk/template').request.method).toBe('GET');

    service.importUsers(file).subscribe();
    const importRequest = http.expectOne('/api/users/bulk/import');
    expect(importRequest.request.method).toBe('POST');
    expect(importRequest.request.body instanceof FormData).toBe(true);

    service.exportUsers('Ada', 'Editor', true).subscribe();
    const exportRequest = http.expectOne(request => request.url === '/api/users/bulk/export');
    expect(exportRequest.request.method).toBe('GET');
    expect(exportRequest.request.params.get('search')).toBe('Ada');
    expect(exportRequest.request.params.get('role')).toBe('Editor');
    expect(exportRequest.request.params.get('isActive')).toBe('true');

    service.assignBulkRole(['user-1'], 'Editor', true).subscribe();
    const roleRequest = http.expectOne('/api/users/bulk/role');
    expect(roleRequest.request.method).toBe('POST');
    expect(roleRequest.request.body).toEqual({ userIds: ['user-1'], roleName: 'Editor', removeExistingRoles: true });

    importRequest.flush({ succeeded: 1, failed: 0, errors: [] });
    exportRequest.flush(new Blob(['id,email\n']));
    roleRequest.flush({ succeeded: 1, failed: 0, errors: [] });
  });
});
