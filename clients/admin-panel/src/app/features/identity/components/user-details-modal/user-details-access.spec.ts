import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { UserDetailsModalComponent } from './user-details-modal';
import { IdentityService } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';

describe('UserDetailsModalComponent access management', () => {
  let fixture: ComponentFixture<UserDetailsModalComponent>;
  let component: UserDetailsModalComponent;
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
        { provide: ToasterService, useValue: toaster }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(UserDetailsModalComponent);
    component = fixture.componentInstance;
    component.userId = 'user-1';
    fixture.detectChanges();
  });

  it('loads active sessions with the user details', () => {
    expect(identity.getUserSessions).toHaveBeenCalledWith('user-1');
    expect(component.sessions().length).toBe(1);
  });

  it('revokes a selected session and refreshes the local list', async () => {
    await component.revokeSession(component.sessions()[0]);

    expect(identity.revokeUserSession).toHaveBeenCalledWith('user-1', 'session-1');
    expect(component.sessions()).toEqual([]);
  });

  it('resets MFA and clears all sessions after confirmation', async () => {
    await component.resetMfa();

    expect(identity.resetUserMfa).toHaveBeenCalledWith('user-1');
    expect(component.sessions()).toEqual([]);
  });
});
