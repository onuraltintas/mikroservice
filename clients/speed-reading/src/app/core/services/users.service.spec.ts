import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [UsersService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(UsersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the identity users contract for filtered admin lists', () => {
    service.getUsers('admin', 'SystemAdmin', true).subscribe();

    const request = http.expectOne(candidate => candidate.url === '/api/v1/users');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('100');
    expect(request.request.params.get('search')).toBe('admin');
    expect(request.request.params.get('role')).toBe('SystemAdmin');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 });
  });

  it('loads the authenticated user profile including MFA status', () => {
    service.getMyProfile().subscribe(profile => expect(profile.mfaEnabled).toBeFalse());

    const request = http.expectOne('/api/v1/users/me');
    expect(request.request.method).toBe('GET');
    request.flush({
      userId: 'user-1',
      email: 'admin@example.com',
      firstName: 'System',
      lastName: 'Admin',
      roles: ['SystemAdmin'],
      isActive: true,
      emailConfirmed: true,
      mfaEnabled: false
    });
  });

  it('sends admin user creation using the identity provisioning contract', () => {
    service.createUser({
      email: 'new-student@example.com',
      firstName: 'New',
      lastName: 'Student',
      role: 'Student',
      phoneNumber: '+905551112233'
    }, true).subscribe();

    const request = http.expectOne('/api/v1/users');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-Skip-Forbidden-Redirect')).toBe('true');
    expect(request.request.body).toEqual({
      email: 'new-student@example.com',
      firstName: 'New',
      lastName: 'Student',
      phoneNumber: '+905551112233',
      role: 'Student'
    });
    request.flush({ userId: 'user-1' });
  });

  it('loads a server-paged user list with the requested filters', () => {
    let result: any;

    service.getUsersPage(2, 25, 'student', 'Student', true).subscribe(value => result = value);

    const request = http.expectOne(candidate => candidate.url === '/api/v1/users');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('student');
    expect(request.request.params.get('role')).toBe('Student');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({
      items: [{ id: 'user-1', firstName: 'Test', lastName: 'Student', email: 'student@example.com', roles: ['Student'], isActive: true, emailConfirmed: true }],
      totalCount: 26,
      pageNumber: 2,
      pageSize: 25,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false
    });

    expect(result.totalCount).toBe(26);
    expect(result.items[0].id).toBe('user-1');
  });

  it('sends role assignments using the identity command shape', () => {
    service.assignRole('user-1', { roleName: 'Teacher' }).subscribe();

    const request = http.expectOne('/api/v1/users/user-1/roles');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ roleName: 'Teacher' });
    request.flush(null);
  });

  it('updates role-specific profile data through the identity profile endpoint', () => {
    service.updateUserProfile('user-1', {
      firstName: 'Updated',
      lastName: 'Student',
      phoneNumber: '+905551112233',
      institutionId: 'institution-1',
      studentBirthDate: '2010-01-02T00:00:00.000Z',
      studentLearningStyle: 'visual'
    }).subscribe();

    const request = http.expectOne('/api/v1/users/user-1/profile');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.studentBirthDate).toBe('2010-01-02T00:00:00.000Z');
    expect(request.request.body.institutionId).toBe('institution-1');
    request.flush(null);
  });

  it('uses the Identity admin change-password contract', () => {
    service.adminResetPassword('user-1', 'NewPassword!123', true).subscribe();

    const request = http.expectOne('/api/v1/users/user-1/change-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-Skip-Forbidden-Redirect')).toBe('true');
    expect(request.request.body).toEqual({ password: 'NewPassword!123' });
    request.flush(null);
  });

  it('deactivates a user through the Identity soft-delete contract', () => {
    service.deactivateUser('user-1').subscribe();

    const request = http.expectOne('/api/v1/users/user-1?permanent=false');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('exposes email confirmation, session and MFA management operations', () => {
    service.revokeEmailConfirmation('user-1').subscribe();
    service.getSessions('user-1').subscribe();
    service.revokeAllSessions('user-1').subscribe();
    service.resetMfa('user-1').subscribe();

    expect(http.expectOne('/api/v1/users/user-1/revoke-email-confirmation').request.method).toBe('POST');
    const getSessionsRequest = http.expectOne(request =>
      request.url === '/api/v1/users/user-1/sessions' && request.method === 'GET');
    expect(getSessionsRequest.request.headers.get('X-Skip-Forbidden-Redirect')).toBe('true');
    const revokeAllSessionsRequest = http.expectOne(request =>
      request.url === '/api/v1/users/user-1/sessions' && request.method === 'DELETE');
    const resetMfaRequest = http.expectOne('/api/v1/users/user-1/mfa/reset');
    expect(resetMfaRequest.request.method).toBe('POST');
    getSessionsRequest.flush([]);
    revokeAllSessionsRequest.flush(null);
    resetMfaRequest.flush(null);
  });
});
