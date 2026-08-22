import { describe, expect, it } from 'vitest';
import { hasCoachingPortalRole, hasRequiredCoachingRole } from './auth.guard';
import { COACHING_PORTAL_ROUTES } from '../../features/coaching-portal/coaching-portal.routes';

describe('hasCoachingPortalRole', () => {
  it('allows student, teacher and parent identities', () => {
    expect(hasCoachingPortalRole({ roles: ['Student'] })).toBe(true);
    expect(hasCoachingPortalRole({ roles: ['Teacher'] })).toBe(true);
    expect(hasCoachingPortalRole({ roles: ['Parent'] })).toBe(true);
  });

  it('does not allow management-only identities', () => {
    expect(hasCoachingPortalRole({ roles: ['SystemAdmin'] })).toBe(false);
    expect(hasCoachingPortalRole({ roles: ['InstitutionAdmin'] })).toBe(false);
    expect(hasCoachingPortalRole(null)).toBe(false);
  });
});

describe('hasRequiredCoachingRole', () => {
  it('matches one of the roles required by a child portal route', () => {
    expect(hasRequiredCoachingRole({ roles: ['Teacher'] }, ['Teacher'])).toBe(true);
    expect(hasRequiredCoachingRole({ roles: ['Parent'] }, ['Teacher', 'Parent'])).toBe(true);
  });

  it('rejects a role outside the child route allow-list', () => {
    expect(hasRequiredCoachingRole({ roles: ['Student'] }, ['Teacher'])).toBe(false);
    expect(hasRequiredCoachingRole(null, ['Teacher'])).toBe(false);
  });
});

describe('coaching portal route role contract', () => {
  it('declares the role boundary for teacher, parent and shared routes', () => {
    const route = (path: string) => COACHING_PORTAL_ROUTES.find(item => item.path === path);

    expect(route('teacher/students')?.data?.['coachingRoles']).toEqual(['Teacher']);
    expect(route('teacher/assignments/new')?.data?.['coachingRoles']).toEqual(['Teacher']);
    expect(route('teacher/academic')?.data?.['coachingRoles']).toEqual(['Teacher']);
    expect(route('teacher/sessions/:id/edit')?.data?.['coachingRoles']).toEqual(['Teacher']);
    expect(route('children')?.data?.['coachingRoles']).toEqual(['Parent']);
    expect(route('assignments/:id')?.data?.['coachingRoles']).toEqual(['Student', 'Teacher', 'Parent']);
  });
});
