import { isSystemRoleName } from './roles-list.component';

describe('role protection', () => {
  it('protects SystemAdmin alongside the other built-in roles', () => {
    expect(isSystemRoleName('SystemAdmin')).toBeTrue();
    expect(isSystemRoleName('Admin')).toBeTrue();
    expect(isSystemRoleName('CustomCoach')).toBeFalse();
  });
});
