import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * The platform password policy, identical to Identity's RegistrationPasswordRules (used by
 * registration, admin password change, self password change and password reset):
 * 8–128 characters, an ASCII upper-case letter, an ASCII lower-case letter, a digit and any
 * character that is not an ASCII letter or digit (for example - _ + = ! @ # or a space).
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;
export const PASSWORD_POLICY_SUMMARY = 'En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter';

export type PasswordRequirementKey = 'length' | 'upper' | 'lower' | 'digit' | 'special';

export interface PasswordRequirement {
    key: PasswordRequirementKey;
    label: string;
    met: boolean;
}

export function passwordRequirements(value: string | null | undefined): PasswordRequirement[] {
    const password = value ?? '';
    return [
        { key: 'length', label: '8-128 karakter', met: password.length >= PASSWORD_MIN_LENGTH && password.length <= PASSWORD_MAX_LENGTH },
        { key: 'upper', label: 'Büyük harf (A-Z)', met: /[A-Z]/.test(password) },
        { key: 'lower', label: 'Küçük harf (a-z)', met: /[a-z]/.test(password) },
        { key: 'digit', label: 'Rakam (0-9)', met: /[0-9]/.test(password) },
        { key: 'special', label: 'Özel karakter (ör. - _ . ! @ #)', met: /[^A-Za-z0-9]/.test(password) },
    ];
}

export function isPasswordPolicySatisfied(value: string | null | undefined): boolean {
    return passwordRequirements(value).every(requirement => requirement.met);
}

export function strongPasswordValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
        const value = control.value;

        if (!value) {
            return null;
        }

        const met = Object.fromEntries(passwordRequirements(String(value)).map(r => [r.key, r.met])) as Record<PasswordRequirementKey, boolean>;
        if (Object.values(met).every(Boolean)) {
            return null;
        }

        return {
            passwordStrength: {
                hasUpperCase: met.upper,
                hasLowerCase: met.lower,
                hasNumeric: met.digit,
                hasSymbol: met.special,
                validLength: met.length,
            }
        };
    };
}

export const passwordMatchValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (!password || !confirmPassword) {
        return null;
    }

    if (confirmPassword.errors && !confirmPassword.errors['passwordMismatch']) {
        return null;
    }

    if (password.value !== confirmPassword.value) {
        confirmPassword.setErrors({ passwordMismatch: true });
        return { passwordMismatch: true };
    } else {
        confirmPassword.setErrors(null);
        return null;
    }
};
