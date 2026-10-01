import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { hasCoachingPortalRole, hasRequiredCoachingRole, coachingPortalGuard, coachingRoleGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { COACHING_PORTAL_ROUTES } from '../../features/coaching-portal/coaching-portal.routes';
import { vi } from 'vitest';

describe('hasCoachingPortalRole', () => {
  it('allows student and teacher identities but not parent-only identities', () => {
    expect(hasCoachingPortalRole({ roles: ['Student'], product: 'coaching' })).toBe(true);
    expect(hasCoachingPortalRole({ roles: ['Teacher'], product: 'coaching' })).toBe(true);
    expect(hasCoachingPortalRole({ roles: ['Parent'], product: 'coaching' })).toBe(false);
    expect(hasCoachingPortalRole({ roles: ['Parent', 'Student'], product: 'coaching' })).toBe(true);
  });

  it('does not allow management-only identities', () => {
    expect(hasCoachingPortalRole({ roles: ['SystemAdmin'] })).toBe(false);
    expect(hasCoachingPortalRole({ roles: ['InstitutionAdmin'] })).toBe(false);
    expect(hasCoachingPortalRole({ roles: ['Teacher'], product: 'speed-reading' })).toBe(false);
    expect(hasCoachingPortalRole({ roles: ['Teacher'] })).toBe(false);
    expect(hasCoachingPortalRole(null)).toBe(false);
  });
});

describe('hasRequiredCoachingRole', () => {
  it('matches one of the roles explicitly allowed by a child portal route', () => {
    expect(hasRequiredCoachingRole({ roles: ['Teacher'], product: 'coaching' }, ['Teacher'])).toBe(true);
    expect(hasRequiredCoachingRole({ roles: ['Student'], product: 'coaching' }, ['Student', 'Teacher'])).toBe(true);
  });

  it('rejects a role outside the child route allow-list', () => {
    expect(hasRequiredCoachingRole({ roles: ['Student'] }, ['Teacher'])).toBe(false);
    expect(hasRequiredCoachingRole({ roles: ['Teacher'], product: 'speed-reading' }, ['Teacher'])).toBe(false);
    expect(hasRequiredCoachingRole({ roles: ['Teacher'] }, ['Teacher'])).toBe(false);
    expect(hasRequiredCoachingRole(null, ['Teacher'])).toBe(false);
  });
});

describe('coaching portal route role contract', () => {
  it('redirects legacy teacher URLs and removes parent-only portal routes', () => {
    const route = (path: string) => COACHING_PORTAL_ROUTES.find(item => item.path === path);

    expect(route('teacher')?.children?.map(child => child.path)).toEqual(['', '**']);
    expect(route('sessions')?.data?.['coachingRoles']).toEqual(['Student']);
    expect(route('children')).toBeUndefined();
    expect(route('notifications')?.data?.['coachingRoles']).toEqual(['Student']);
    expect(route('assignments/:id')?.data?.['coachingRoles']).toEqual(['Student']);
  });

  it('routes Teacher users away from a student-only page to the new staff portal', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: {
          userProfile: signal({ id: 'teacher-1', roles: ['Teacher'], role: 'Teacher', product: 'coaching' }),
          waitForAuth: vi.fn().mockResolvedValue(true)
        } }
      ]
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const result = await TestBed.runInInjectionContext(() => coachingRoleGuard(
      { data: { coachingRoles: ['Student'] } } as never,
      { url: '/coaching-portal/sessions' } as never
    ));

    expect(result).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/teacher']);
  });

  it('denies parent-only users access to the Coaching portal without changing their account', async () => {
    const profile = signal({ id: 'parent-1', roles: ['Parent'], role: 'Parent', product: 'coaching' });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: {
          userProfile: profile,
          waitForAuth: vi.fn().mockResolvedValue(true)
        } }
      ]
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const result = await TestBed.runInInjectionContext(() => coachingPortalGuard(
      {} as never,
      { url: '/coaching-portal' } as never
    ));

    expect(result).toBe(false);
    expect(profile()?.roles).toEqual(['Parent']);
    expect(navigate).toHaveBeenCalledWith(['/dashboard'], { queryParams: { forbidden: 'true' } });
  });
});
