import { describe, expect, it } from 'vitest';
import { hasCoachingPortalRole } from './auth.guard';

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
