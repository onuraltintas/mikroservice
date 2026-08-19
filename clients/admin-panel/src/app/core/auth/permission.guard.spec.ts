import { describe, expect, it } from 'vitest';
import { UserProfile, hasRequiredAccess, hasRequiredPermission, hasRequiredRole } from './auth.service';

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

describe('hasRequiredRole', () => {
  it('requires an exact server-issued role', () => {
    expect(hasRequiredRole(user([], ['SystemAdmin']), 'SystemAdmin')).toBe(true);
    expect(hasRequiredRole(user([], ['InstitutionAdmin']), 'SystemAdmin')).toBe(false);
  });
});

describe('hasRequiredAccess', () => {
  it('requires both the action permission and optional role', () => {
    const systemAdmin = user(['Permissions.Users.Create'], ['SystemAdmin']);
    const institutionAdmin = user(['Permissions.Users.Create'], ['InstitutionAdmin']);

    expect(hasRequiredAccess(systemAdmin, 'Permissions.Users.Create', 'SystemAdmin')).toBe(true);
    expect(hasRequiredAccess(institutionAdmin, 'Permissions.Users.Create', 'SystemAdmin')).toBe(false);
    expect(hasRequiredAccess(systemAdmin, 'Permissions.Users.Delete', 'SystemAdmin')).toBe(false);
  });

  it('supports permission-only actions', () => {
    expect(hasRequiredAccess(user(['Permissions.Roles.Edit']), 'Permissions.Roles.Edit')).toBe(true);
  });
});
