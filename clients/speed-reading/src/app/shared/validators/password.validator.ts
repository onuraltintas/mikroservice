import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Şifre validasyon kuralları için interface
 */
export interface PasswordValidationErrors {
    minLength?: boolean;
    maxLength?: boolean;
    uppercase?: boolean;
    lowercase?: boolean;
    digit?: boolean;
    specialChar?: boolean;
}

/**
 * Şifre validasyon hata mesajları (Türkçe)
 */
export const PASSWORD_ERROR_MESSAGES: Record<string, string> = {
    minLength: 'Şifre en az 8 karakter olmalıdır',
    maxLength: 'Şifre en fazla 128 karakter olabilir',
    uppercase: 'Şifre en az bir büyük harf içermelidir',
    lowercase: 'Şifre en az bir küçük harf içermelidir',
    digit: 'Şifre en az bir rakam içermelidir',
    specialChar: 'Şifre en az bir özel karakter içermelidir (ör. - _ . ! @ #)'
};

/**
 * Platformun tek şifre kuralı; Identity RegistrationPasswordRules ile aynıdır (kayıt, şifre
 * sıfırlama, şifre değiştirme): 8-128 karakter, büyük harf (A-Z), küçük harf (a-z), rakam ve
 * harf ya da rakam olmayan herhangi bir karakter (ör. - _ . ! @ # veya boşluk).
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;
export const PASSWORD_POLICY_SUMMARY = 'En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter';
const SPECIAL_CHARACTER = /[^A-Za-z0-9]/;

export type PasswordRequirementKey = 'length' | 'upper' | 'lower' | 'digit' | 'special';
export interface PasswordRequirement { key: PasswordRequirementKey; label: string; met: boolean; }

export function passwordRequirements(value: string | null | undefined): PasswordRequirement[] {
    const password = value ?? '';
    return [
        { key: 'length', label: '8-128 karakter', met: password.length >= PASSWORD_MIN_LENGTH && password.length <= PASSWORD_MAX_LENGTH },
        { key: 'upper', label: 'Büyük harf (A-Z)', met: /[A-Z]/.test(password) },
        { key: 'lower', label: 'Küçük harf (a-z)', met: /[a-z]/.test(password) },
        { key: 'digit', label: 'Rakam (0-9)', met: /[0-9]/.test(password) },
        { key: 'special', label: 'Özel karakter (ör. - _ . ! @ #)', met: SPECIAL_CHARACTER.test(password) },
    ];
}

export function isPasswordPolicySatisfied(value: string | null | undefined): boolean {
    return passwordRequirements(value).every(requirement => requirement.met);
}

/**
 * Güçlü şifre validatörü
 * Best practice şifre kurallarını uygular:
 * - Minimum 8 karakter
 * - En az 1 büyük harf (A-Z)
 * - En az 1 küçük harf (a-z)
 * - En az 1 rakam (0-9)
 * - En az 1 özel karakter (!@#$%^&*()_+-=[]{}|;:',.<>?/)
 */
export function strongPasswordValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
        const value = control.value;

        if (!value) {
            return null; // required validator bunu kontrol etsin
        }

        const errors: PasswordValidationErrors = {};

        // 8-128 karakter
        if (value.length < PASSWORD_MIN_LENGTH) {
            errors.minLength = true;
        }
        if (value.length > PASSWORD_MAX_LENGTH) {
            errors.maxLength = true;
        }

        // En az bir büyük harf
        if (!/[A-Z]/.test(value)) {
            errors.uppercase = true;
        }

        // En az bir küçük harf
        if (!/[a-z]/.test(value)) {
            errors.lowercase = true;
        }

        // En az bir rakam
        if (!/[0-9]/.test(value)) {
            errors.digit = true;
        }

        // En az bir özel karakter
        if (!SPECIAL_CHARACTER.test(value)) {
            errors.specialChar = true;
        }

        return Object.keys(errors).length > 0 ? { passwordStrength: errors } : null;
    };
}

/**
 * Şifre eşleşme validatörü (şifre tekrar kontrolü için)
 * @param passwordControlName Karşılaştırılacak şifre alanının adı
 */
export function passwordMatchValidator(passwordControlName: string): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
        const parent = control.parent;
        if (!parent) {
            return null;
        }

        const passwordControl = parent.get(passwordControlName);
        if (!passwordControl) {
            return null;
        }

        if (control.value !== passwordControl.value) {
            return { passwordMismatch: true };
        }

        return null;
    };
}

/**
 * Şifre güç hesaplayıcı (UI gösterimi için)
 * @returns 0-100 arası bir güç skoru
 */
export function calculatePasswordStrength(password: string): number {
    if (!password) return 0;

    let score = 0;

    // Uzunluk puanı
    if (password.length >= 8) score += 20;
    if (password.length >= 12) score += 10;
    if (password.length >= 16) score += 10;

    // Karakter çeşitliliği puanı
    if (/[a-z]/.test(password)) score += 15;
    if (/[A-Z]/.test(password)) score += 15;
    if (/[0-9]/.test(password)) score += 15;
    if (SPECIAL_CHARACTER.test(password)) score += 15;

    return Math.min(score, 100);
}

/**
 * Şifre güç seviyesi
 */
export type PasswordStrengthLevel = 'weak' | 'medium' | 'strong' | 'very-strong';

/**
 * Şifre güç seviyesini döndürür
 */
export function getPasswordStrengthLevel(password: string): PasswordStrengthLevel {
    const score = calculatePasswordStrength(password);

    if (score < 40) return 'weak';
    if (score < 60) return 'medium';
    if (score < 80) return 'strong';
    return 'very-strong';
}

/**
 * Şifre güç seviyesi Türkçe açıklamaları
 */
export const PASSWORD_STRENGTH_LABELS: Record<PasswordStrengthLevel, string> = {
    'weak': 'Zayıf',
    'medium': 'Orta',
    'strong': 'Güçlü',
    'very-strong': 'Çok Güçlü'
};
