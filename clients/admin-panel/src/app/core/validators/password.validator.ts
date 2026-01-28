import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function strongPasswordValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
        const value = control.value;

        if (!value) {
            return null;
        }

        const hasUpperCase = /[A-Z]+/.test(value);
        const hasLowerCase = /[a-z]+/.test(value);
        const hasNumeric = /[0-9]+/.test(value);
        const hasSymbol = /[\W_]+/.test(value); // Özel karakterler: !@#$%^&* vb.
        const validLength = value.length >= 8;

        const passwordValid = hasUpperCase && hasLowerCase && hasNumeric && hasSymbol && validLength;

        return !passwordValid ? {
            passwordStrength: {
                hasUpperCase,
                hasLowerCase,
                hasNumeric,
                hasSymbol,
                validLength
            }
        } : null;
    }
}
