import { describe, expect, it } from 'vitest';
import { UserProfile, hasRequiredPermission } from './auth.service';

const user = (permissions: string[], roles: string[] = ['InstitutionAdmin']): UserProfile => ({
  id: 'user-1',
  email: 'admin@example.test',
  firstName: 'Test',
  lastName: 'Admin',
  username: 'admin@example.test',
  roles,
  role: roles[0],
  permissions
});

describe('hasRequiredPermission', () => {
  it('allows a user with the exact permission', () => {
    expect(hasRequiredPermission(user(['Permissions.Institutions.View']), 'Permissions.Institutions.View')).toBe(true);
  });

  it('does not treat a similarly named permission as a match', () => {
    expect(hasRequiredPermission(user(['Permissions.Institutions.Edit']), 'Permissions.Institutions.View')).toBe(false);
  });

  it('does not grant access to an unauthenticated user', () => {
    expect(hasRequiredPermission(null, 'Permissions.Institutions.View')).toBe(false);
  });
});
