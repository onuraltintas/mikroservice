import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

// Material Modules
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';

import { GoogleSigninButtonModule, SocialAuthService } from '@abacritt/angularx-social-login';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterLink,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        GoogleSigninButtonModule
    ],
    templateUrl: './login.component.html',
    styleUrl: './login.component.scss'
})
export class LoginComponent {
    private authService = inject(AuthService);
    private router = inject(Router);
    private toaster = inject(ToasterService);
    private socialAuthService = inject(SocialAuthService);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    showResendLink = signal(false);
    showSupportLink = signal(false);
    resendingEmail = signal(false);

    // Password Flow Properties
    email = '';
    password = '';
    rememberMe = false; // Yeni özellik
    hidePassword = signal(true);

    constructor() {
        // Listen for Google Login
        this.socialAuthService.authState.subscribe((user) => {
            if (user && user.idToken) {
                this.handleGoogleLogin(user.idToken);
            }
        });
    }

    async handleGoogleLogin(idToken: string) {
        this.isLoading.set(true);
        try {
            await this.authService.loginWithGoogle(idToken);
            this.toaster.success('Google ile giriş başarılı!');
        } catch (error: any) {
            console.error('Google Login Error:', error);
            const errorCode = error.error?.code; // Changed from error.error?.Error which was likely incorrect based on typical formatting
            // Better yet, let's look at the structure. Usually it's error.error.Code or error.Code depending on HttpErrorResponse
            // Assuming standard format { Error: { Code: ..., Description: ... } } or { code: ..., message: ... }

            // Backend returns: Result.Failure(new Error("Code", "Message")) -> serialized as { code: "...", message: "...", ... } or similar.
            // Let's assume error.error.code based on previous logic.

            const errorMsg = error.error?.message || error.error?.description || 'Bir hata oluştu.';

            /* MAINTENANCE MODE HANDLING & REGISTRATION CHECK */
            if (errorCode === 'System.MaintenanceMode') {
                this.errorMessage.set(errorMsg || 'Sistem bakım modundadır.');
                this.toaster.warning('⚠️ Sistem Bakım Modu Aktif');
            } else if (errorCode === 'Auth.UserInactive') {
                this.errorMessage.set('Hesabınız pasif durumdadır.');
                this.toaster.error('Hesabınız pasif.');
            } else if (errorCode === 'Identity.RegistrationDisabled') {
                this.errorMessage.set(errorMsg);
                this.toaster.info('Yeni Kullanıcı Kaydı Kapalı');
            } else {
                this.toaster.error(errorMsg);
                this.errorMessage.set(errorMsg);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    async onSubmit(event: Event) {
        event.preventDefault();

        if (!this.email || !this.password) return;

        this.isLoading.set(true);
        this.errorMessage.set(null);

        try {
            // rememberMe bilgisini de gönderiyoruz
            await this.authService.loginWithPassword(this.email, this.password, this.rememberMe);
            this.toaster.success('Giriş başarılı! Yönlendiriliyorsunuz.');
        } catch (error: any) {
            console.error('Login error:', error);

            const errorCode = error.error?.code;

            /* MAINTENANCE MODE HANDLING */
            if (errorCode === 'System.MaintenanceMode') {
                this.errorMessage.set(error.error?.message || 'Sistem bakım modundadır.');
                this.toaster.warning('⚠️ Bakım Modu Aktif');
            } else if (errorCode === 'Auth.EmailNotConfirmed') {
                this.errorMessage.set('Lütfen e-posta adresinizi doğrulayın.');
                this.showResendLink.set(true);
            } else if (errorCode === 'Auth.UserInactive') {
                this.errorMessage.set('Hesabınız şu anda pasif durumdadır.');
                this.showSupportLink.set(true);
            } else {
                this.errorMessage.set(error.error?.message || 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.');
            }
            this.isLoading.set(false);
        }
    }

    async resendVerification() {
        if (!this.email) return;

        this.resendingEmail.set(true);
        try {
            await this.authService.resendVerificationEmail(this.email);
            this.toaster.success('Doğrulama e-postası tekrar gönderildi. Lütfen gelen kutunuzu kontrol edin.');
            this.showResendLink.set(false);
        } catch (error: any) {
            this.toaster.error(error.error?.message || 'E-posta gönderilemedi.');
        } finally {
            this.resendingEmail.set(false);
        }
    }

    onLogin() {
        // Fallback or Social Login could use this
        this.authService.login();
    }

    togglePasswordVisibility() {
        this.hidePassword.update(v => !v);
    }

    navigateToSupport() {
        this.router.navigate(['/auth/support']);
    }
}
