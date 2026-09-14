import { isInstitutionManager } from './institution-manager.guard';

describe('isInstitutionManager', () => {
  it('allows institution owners as well as institution administrators', () => {
    expect(isInstitutionManager(['InstitutionOwner'])).toBeTrue();
    expect(isInstitutionManager(['InstitutionAdmin'])).toBeTrue();
    expect(isInstitutionManager(['Teacher'])).toBeFalse();
  });
});
