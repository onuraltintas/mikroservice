import { FormControl } from '@angular/forms';
import {
  PASSWORD_POLICY_SUMMARY,
  isPasswordPolicySatisfied,
  passwordRequirements,
  strongPasswordValidator,
} from './password.validator';

// Same table as the Identity PasswordPolicyConsistencyTests (RegistrationPasswordRules).
const cases: Array<[string, boolean]> = [
  ['Sifre-2026', true],
  ['Sifre_2026', true],
  ['Sifre 2026', true],
  ['Şifre+2026A', true],
  ['Sifre€2026', true],
  ['Sifre2026', false],
  ['sifre-2026', false],
  ['SIFRE-2026', false],
  ['Sifre-Yeni', false],
  ['Ab-1', false],
  ['Sifre-2026' + 'x'.repeat(119), false],
];

describe('speed-reading password policy (client = server)', () => {
  cases.forEach(([password, valid]) => {
    it(`${password.slice(0, 20)} → ${valid}`, () => {
      expect(isPasswordPolicySatisfied(password)).toBe(valid);
      expect(strongPasswordValidator()(new FormControl(password)) === null).toBe(valid);
    });
  });

  it('reports a password longer than 128 characters', () => {
    expect(strongPasswordValidator()(new FormControl('Sifre-2026' + 'x'.repeat(119)))?.['passwordStrength']?.maxLength).toBeTrue();
  });

  it('lists every requirement with a live state', () => {
    expect(passwordRequirements('').map(r => r.met)).toEqual([false, false, false, false, false]);
    expect(passwordRequirements('sifre2026').filter(r => !r.met).map(r => r.key)).toEqual(['upper', 'special']);
  });

  it('describes the policy in one Turkish sentence', () => {
    expect(PASSWORD_POLICY_SUMMARY).toBe('En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter');
  });
});
