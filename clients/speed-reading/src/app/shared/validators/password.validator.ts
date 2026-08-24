import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Şifre validasyon kuralları için interface
 */
export interface PasswordValidationErrors {
    minLength?: boolean;
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
    uppercase: 'Şifre en az bir büyük harf içermelidir',
    lowercase: 'Şifre en az bir küçük harf içermelidir',
    digit: 'Şifre en az bir rakam içermelidir',
    specialChar: 'Şifre en az bir özel karakter içermelidir (!@#$%^&*)'
};

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

        // Minimum 8 karakter
        if (value.length < 8) {
            errors.minLength = true;
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
        if (!/[!@#$%^&*()_+\-=\[\]{}|;:'",.<>?\/\\]/.test(value)) {
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
    if (/[!@#$%^&*()_+\-=\[\]{}|;:'",.<>?\/\\]/.test(password)) score += 15;

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
