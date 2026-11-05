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
  ['Sifre2026', false],
  ['sifre-2026', false],
  ['SIFRE-2026', false],
  ['Sifre-Yeni', false],
  ['Ab-1', false],
  ['Sifre-2026' + 'x'.repeat(119), false],
];

describe('password policy (client = server)', () => {
  it.each(cases)('%s → %s', (password, valid) => {
    expect(isPasswordPolicySatisfied(password)).toBe(valid);
    expect(strongPasswordValidator()(new FormControl(password)) === null).toBe(valid);
  });

  it('accepts exactly 128 characters and rejects 129', () => {
    expect(isPasswordPolicySatisfied('Sifre-2026' + 'x'.repeat(118))).toBe(true);
    expect(isPasswordPolicySatisfied('Sifre-2026' + 'x'.repeat(119))).toBe(false);
  });

  it('lists every requirement with a live state', () => {
    expect(passwordRequirements('').map(r => r.met)).toEqual([false, false, false, false, false]);
    expect(passwordRequirements('Sifre-2026').every(r => r.met)).toBe(true);
    expect(passwordRequirements('sifre2026').filter(r => !r.met).map(r => r.key)).toEqual(['upper', 'special']);
  });

  it('describes the policy in one Turkish sentence', () => {
    expect(PASSWORD_POLICY_SUMMARY).toBe('En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter');
  });
});
