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
});
