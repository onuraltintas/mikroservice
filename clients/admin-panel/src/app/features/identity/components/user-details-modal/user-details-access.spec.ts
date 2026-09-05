import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { UserDetailsModalComponent } from './user-details-modal';
import { AuthService, UserProfile } from '../../../../core/auth/auth.service';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

describe('UserDetailsModalComponent access management', () => {
  let fixture: ComponentFixture<UserDetailsModalComponent>;
  let component: UserDetailsModalComponent;
  const systemAdminProfile: UserProfile = {
    id: 'admin-1', email: 'admin@example.com', firstName: 'System', lastName: 'Admin',
    username: 'admin', role: 'SystemAdmin', roles: ['SystemAdmin'],
    permissions: ['Permissions.Users.View', 'Permissions.Users.Edit'],
    mfaVerified: true
  };
  const auth = { userProfile: signal<UserProfile | null>(systemAdminProfile) };
  const identity = {
    getUserById: vi.fn(() => of({ userId: 'user-1', email: 'user@example.com', fullName: 'User', role: 'Student', isActive: true, emailConfirmed: true, roles: [], permissions: [] })),
    getUserSessions: vi.fn(() => of([{ id: 'session-1', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2026-01-02T00:00:00Z', isPersistent: true }])),
    revokeUserSession: vi.fn(() => of(void 0)),
    revokeAllUserSessions: vi.fn(() => of(void 0)),
    resetUserMfa: vi.fn(() => of(void 0))
  };
  const toaster = { confirm: vi.fn(async () => true), success: vi.fn(), error: vi.fn() };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserDetailsModalComponent],
      providers: [
        { provide: IdentityService, useValue: identity },
        { provide: ToasterService, useValue: toaster },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(UserDetailsModalComponent);
    component = fixture.componentInstance;
    component.userId = 'user-1';
    vi.clearAllMocks();
    auth.userProfile.set(systemAdminProfile);
  });

  it('loads active sessions with the user details', () => {
    fixture.detectChanges();
    expect(identity.getUserSessions).toHaveBeenCalledWith('user-1');
    expect(component.sessions().length).toBe(1);
  });

  it('revokes a selected session and refreshes the local list', async () => {
    fixture.detectChanges();
    await component.revokeSession(component.sessions()[0]);

    expect(identity.revokeUserSession).toHaveBeenCalledWith('user-1', 'session-1');
    expect(component.sessions()).toEqual([]);
  });

  it('resets MFA and clears all sessions after confirmation', async () => {
    fixture.detectChanges();
    await component.resetMfa();

    expect(identity.resetUserMfa).toHaveBeenCalledWith('user-1');
    expect(component.sessions()).toEqual([]);
  });

  it('does not load or render access management for institution administrators', () => {
    auth.userProfile.set({
      ...systemAdminProfile,
      role: 'InstitutionAdmin',
      roles: ['InstitutionAdmin'],
      permissions: ['Permissions.Users.View']
    });

    fixture.detectChanges();

    expect(identity.getUserSessions).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Tüm Oturumları Sonlandır');
    expect(fixture.nativeElement.textContent).not.toContain("MFA'yı Sıfırla");
  });

  it('does not call protected access endpoints before MFA verification', () => {
    auth.userProfile.set({ ...systemAdminProfile, mfaVerified: false });

    fixture.detectChanges();

    expect(identity.getUserSessions).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('MFA doğrulaması gerekiyor');
  });
});
