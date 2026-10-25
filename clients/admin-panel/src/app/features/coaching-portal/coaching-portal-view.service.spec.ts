import { ToasterService } from '../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { CoachingPortalViewService } from './coaching-portal-view.service';

describe('CoachingPortalViewService', () => {
  it('allows a dual-role user to switch views without granting a missing role', () => {
    const profile = signal<any>({ roles: ['Student', 'Teacher'], product: 'coaching' });
    TestBed.configureTestingModule({ providers: [
      CoachingPortalViewService,
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const view = TestBed.inject(CoachingPortalViewService);
    expect(view.current()).toBe('Student');
    view.select('Student');
    expect(view.current()).toBe('Student');
    profile.set({ roles: ['Teacher'], product: 'coaching' });
    expect(view.current()).toBe('Teacher');
    view.select('Student');
    expect(view.current()).toBe('Teacher');
  });

  it('hides coaching views for another product session', () => {
    const profile = signal<any>({ roles: ['Teacher'], product: 'speed-reading' });
    TestBed.configureTestingModule({ providers: [
      CoachingPortalViewService,
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const view = TestBed.inject(CoachingPortalViewService);
    expect(view.current()).toBeNull();
    expect(view.canSelect('Teacher')).toBe(false);
  });

  it('does not expose a Coaching panel for a parent-only account', () => {
    const profile = signal<any>({ roles: ['Parent'], product: 'coaching' });
    TestBed.configureTestingModule({ providers: [
      CoachingPortalViewService,
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const view = TestBed.inject(CoachingPortalViewService);

    expect(view.current()).toBeNull();
  });

  it('keeps a student view for a dual Student and Parent account without a parent switch', () => {
    const profile = signal<any>({ roles: ['Parent', 'Student'], product: 'coaching' });
    TestBed.configureTestingModule({ providers: [
      CoachingPortalViewService,
      { provide: AuthService, useValue: { userProfile: profile } }
    ] });
    const view = TestBed.inject(CoachingPortalViewService);

    expect(view.current()).toBe('Student');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
