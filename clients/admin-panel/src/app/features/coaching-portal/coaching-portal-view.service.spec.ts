import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { CoachingPortalViewService } from './coaching-portal-view.service';

describe('CoachingPortalViewService', () => {
  it('allows a dual-role user to switch views without granting a missing role', () => {
    const profile = signal<any>({ roles: ['Student', 'Teacher'] });
    TestBed.configureTestingModule({ providers: [
      CoachingPortalViewService,
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const view = TestBed.inject(CoachingPortalViewService);
    expect(view.current()).toBe('Teacher');
    view.select('Student');
    expect(view.current()).toBe('Student');
    profile.set({ roles: ['Teacher'] });
    expect(view.current()).toBe('Teacher');
    view.select('Student');
    expect(view.current()).toBe('Teacher');
  });
});
