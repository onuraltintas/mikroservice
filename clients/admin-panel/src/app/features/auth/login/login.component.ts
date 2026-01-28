import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
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
// Rebuild trigger

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
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

    // Password Flow Properties
    email = '';
    password = '';
    rememberMe = false; // Yeni özellik
    hidePassword = signal(true);

    constructor() {
        // Listen for Google Login
        this.socialAuthService.authState.subscribe((user) => {
            console.log('Google User:', user);
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
        } catch (error) {
            console.error('Google Login Error:', error);
            this.toaster.error('Google ile giriş başarısız.');
            this.errorMessage.set('Google ile giriş yapılamadı.');
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
        } catch (error) {
            console.error('Login error:', error);
            this.errorMessage.set('Giriş başarısız. Lütfen bilgilerinizi kontrol edin.');
            this.isLoading.set(false);
        }
    }

    onLogin() {
        // Fallback or Social Login could use this
        this.authService.login();
    }

    togglePasswordVisibility() {
        this.hidePassword.update(v => !v);
    }
}
