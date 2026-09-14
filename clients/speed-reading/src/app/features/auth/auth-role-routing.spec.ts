import { resolveAuthDestination } from './auth-role-routing';

describe('resolveAuthDestination', () => {
  it('routes each supported role to its own workspace', () => {
    expect(resolveAuthDestination(['Student'])).toBe('student');
    expect(resolveAuthDestination(['Teacher'])).toBe('teacher');
    expect(resolveAuthDestination(['InstitutionAdmin'])).toBe('institution');
    expect(resolveAuthDestination(['InstitutionOwner'])).toBe('institution');
    expect(resolveAuthDestination(['Admin'])).toBe('admin');
  });

  it('uses the most privileged supported role regardless of API role order', () => {
    expect(resolveAuthDestination(['Student', 'InstitutionAdmin'])).toBe('institution');
    expect(resolveAuthDestination(['Teacher', 'SystemAdmin'])).toBe('admin');
    expect(resolveAuthDestination(['Student', 'Teacher'])).toBe('teacher');
  });

  it('returns null when no supported role is present', () => {
    expect(resolveAuthDestination([])).toBeNull();
    expect(resolveAuthDestination(undefined)).toBeNull();
  });
});
