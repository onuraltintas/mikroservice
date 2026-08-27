import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthResponse } from '../../core/models/user.model';
import { SettingsService } from './settings.service';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;

  beforeEach(() => {
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
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
  });

  afterEach(() => {
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');
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
});
