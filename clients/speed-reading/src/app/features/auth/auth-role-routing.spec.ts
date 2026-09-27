import { resolveAuthDestination, resolveInvitationReturnUrl } from './auth-role-routing';

describe('resolveAuthDestination', () => {
  it('routes each supported role to its own workspace', () => {
    expect(resolveAuthDestination(['Student'])).toBe('student');
    expect(resolveAuthDestination(['Teacher'])).toBe('teacher');
    expect(resolveAuthDestination(['InstitutionAdmin'])).toBe('institution');
    expect(resolveAuthDestination(['InstitutionOwner'])).toBe('institution');
    expect(resolveAuthDestination(['Admin'])).toBe('exercisePreview');
    expect(resolveAuthDestination(['Editor'])).toBe('exercisePreview');
  });

  it('uses the most privileged supported role regardless of API role order', () => {
    expect(resolveAuthDestination(['Student', 'InstitutionAdmin'])).toBe('institution');
    expect(resolveAuthDestination(['Teacher', 'SystemAdmin'])).toBe('exercisePreview');
    expect(resolveAuthDestination(['Student', 'Teacher'])).toBe('teacher');
  });

  it('returns null when no supported role is present', () => {
    expect(resolveAuthDestination([])).toBeNull();
    expect(resolveAuthDestination(undefined)).toBeNull();
  });
});

describe('resolveInvitationReturnUrl', () => {
  it('preserves only the local invitation acceptance route and query', () => {
    expect(resolveInvitationReturnUrl('/auth/accept-invitation?id=invite-1'))
      .toBe('/auth/accept-invitation?id=invite-1');
    expect(resolveInvitationReturnUrl('/auth/login')).toBeNull();
    expect(resolveInvitationReturnUrl('https://attacker.example/auth/accept-invitation?id=invite-1')).toBeNull();
    expect(resolveInvitationReturnUrl('//attacker.example/auth/accept-invitation')).toBeNull();
    expect(resolveInvitationReturnUrl('/auth/accept-invitation-elsewhere')).toBeNull();
  });
});
