import { describe, expect, it } from 'vitest';
import { hasCoachingPortalRole, hasRequiredCoachingRole } from './auth.guard';

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
