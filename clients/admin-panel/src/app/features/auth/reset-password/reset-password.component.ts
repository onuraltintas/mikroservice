import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IdentityService } from '../../../core/services/identity.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
    selector: 'app-reset-password',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        MatIconModule,
        MatButtonModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './reset-password.component.html',
    styleUrl: './reset-password.component.scss'
})
export class ResetPasswordComponent implements OnInit {
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);
    private route = inject(ActivatedRoute);
    private router = inject(Router);

    email = '';
    token = '';
    newPassword = '';
    confirmPassword = '';

    isLoading = signal(false);
    isSuccess = signal(false);
    errorMessage = signal('');
    hidePassword = signal(true);
    hideConfirmPassword = signal(true);

    ngOnInit() {
        // Get token and email from URL query params
        this.route.queryParams.subscribe(params => {
            this.token = params['token'] || '';
            this.email = params['email'] || '';

            if (!this.token || !this.email) {
                this.errorMessage.set('Geçersiz veya eksik sıfırlama bağlantısı.');
            }
        });
    }

    get passwordsMatch(): boolean {
        return this.newPassword === this.confirmPassword;
    }

    // Individual validation checks
    get hasMinLength(): boolean {
        return this.newPassword.length >= 8;
    }

    get hasUppercase(): boolean {
        return /[A-Z]/.test(this.newPassword);
    }

    get hasLowercase(): boolean {
        return /[a-z]/.test(this.newPassword);
    }

    get hasNumber(): boolean {
        return /[0-9]/.test(this.newPassword);
    }

    get hasSpecialChar(): boolean {
        return /[!@#$%^&*(),.?":{}|<>_\-+=\[\]\\;'/`~]/.test(this.newPassword);
    }

    get passwordStrong(): boolean {
        return this.hasMinLength &&
            this.hasUppercase &&
            this.hasLowercase &&
            this.hasNumber &&
            this.hasSpecialChar;
    }

    get passwordStrengthLevel(): number {
        let level = 0;
        if (this.hasMinLength) level++;
        if (this.hasUppercase) level++;
        if (this.hasLowercase) level++;
        if (this.hasNumber) level++;
        if (this.hasSpecialChar) level++;
        return level;
    }

    togglePasswordVisibility() {
        this.hidePassword.set(!this.hidePassword());
    }

    toggleConfirmPasswordVisibility() {
        this.hideConfirmPassword.set(!this.hideConfirmPassword());
    }

    onSubmit(event: Event) {
        event.preventDefault();

        if (!this.token || !this.email) {
            this.errorMessage.set('Geçersiz sıfırlama bağlantısı.');
            return;
        }

        if (!this.passwordsMatch) {
            this.errorMessage.set('Şifreler eşleşmiyor.');
            return;
        }

        if (!this.passwordStrong) {
            this.errorMessage.set('Şifre tüm gereksinimleri karşılamalıdır.');
            return;
        }

        this.isLoading.set(true);
        this.errorMessage.set('');

        this.identityService.resetPassword(this.email, this.token, this.newPassword).subscribe({
            next: () => {
                this.isSuccess.set(true);
                this.isLoading.set(false);
                this.toaster.success('Şifreniz başarıyla değiştirildi!');
            },
            error: (err) => {
                this.isLoading.set(false);
                const message = err.error?.message || err.error?.Message || 'Şifre sıfırlama başarısız oldu.';
                this.errorMessage.set(message);
            }
        });
    }

    goToLogin() {
        this.router.navigate(['/auth/login']);
    }
}
