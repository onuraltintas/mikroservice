import { Component, OnInit, inject, signal, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityService, UserProfileDto } from '../../../../core/services/identity.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { GoogleLoginProvider, SocialAuthService } from '@abacritt/angularx-social-login';

import { RouterModule } from '@angular/router';

@Component({
    selector: 'app-profile-settings',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule, RouterModule],
    templateUrl: './profile-settings.html',
    styleUrl: './profile-settings.scss'
})
export class ProfileSettingsComponent implements OnInit {
    private fb = inject(FormBuilder);
    private identityService = inject(IdentityService);
    private toaster = inject(ToasterService);
    private authService = inject(AuthService);
    private socialAuthService = inject(SocialAuthService);
    private platformId = inject(PLATFORM_ID);

    profileForm!: FormGroup;
    passwordForm!: FormGroup;

    user = signal<UserProfileDto | null>(null);
    loading = signal(true);
    savingProfile = signal(false);
    savingPassword = signal(false);
    linkingGoogle = signal(false);

    ngOnInit() {
        this.initForms();

        if (!isPlatformBrowser(this.platformId)) {
            this.loading.set(false);
            return;
        }

        this.loadProfile();
    }

    private initForms() {
        this.profileForm = this.fb.group({
            firstName: ['', [Validators.required, Validators.minLength(2)]],
            lastName: ['', [Validators.required, Validators.minLength(2)]],
            email: [{ value: '', disabled: true }], // Email usually cannot be changed easily
            phoneNumber: ['']
        });

        this.passwordForm = this.fb.group({
            currentPassword: ['', [Validators.required]],
            newPassword: ['', [Validators.required, Validators.minLength(6)]],
            confirmPassword: ['', [Validators.required]]
        }, {
            validators: this.passwordMatchValidator
        });
    }

    private passwordMatchValidator(g: FormGroup) {
        const newPassword = g.get('newPassword')?.value;
        const confirmPassword = g.get('confirmPassword')?.value;
        return newPassword === confirmPassword ? null : { mismatch: true };
    }

    loadProfile() {
        this.loading.set(true);
        this.identityService.getMyProfile().subscribe({
            next: (res) => {
                this.user.set(res);
                this.profileForm.patchValue({
                    firstName: res.firstName,
                    lastName: res.lastName,
                    email: res.email,
                    phoneNumber: res.phoneNumber
                });
                this.loading.set(false);
            },
            error: (err) => {
                this.toaster.error('Profil bilgileri yüklenemedi.');
                this.loading.set(false);
            }
        });
    }

    updateProfile() {
        if (this.profileForm.invalid) return;

        this.savingProfile.set(true);
        this.identityService.updateMyProfile(this.profileForm.value).subscribe({
            next: () => {
                this.toaster.success('Profiliniz başarıyla güncellendi.');
                this.savingProfile.set(false);
                // We might want to refresh the global user profile in AuthService if it changed
                // For now, let's just reload local data
                this.loadProfile();
            },
            error: (err) => {
                this.toaster.error('Profil güncellenirken bir hata oluştu.');
                this.savingProfile.set(false);
            }
        });
    }

    updatePassword() {
        if (this.passwordForm.invalid) return;

        this.savingPassword.set(true);
        const { currentPassword, newPassword } = this.passwordForm.value;

        this.identityService.changeMyPassword({ currentPassword, newPassword }).subscribe({
            next: () => {
                this.toaster.success('Şifreniz başarıyla güncellendi.');
                this.passwordForm.reset();
                this.savingPassword.set(false);
            },
            error: (err) => {
                const errorMsg = err.error?.Error?.Message || 'Şifre güncellenirken bir hata oluştu.';
                this.toaster.error(errorMsg);
                this.savingPassword.set(false);
            }
        });
    }

    async linkGoogleAccount() {
        if (this.linkingGoogle()) return;

        this.linkingGoogle.set(true);
        try {
            const googleUser = await this.socialAuthService.signIn(GoogleLoginProvider.PROVIDER_ID);
            if (!googleUser?.idToken) {
                throw new Error('Google kimlik doğrulama belirteci alınamadı.');
            }

            await this.authService.linkGoogleAccount(googleUser.idToken);
            this.toaster.success('Google hesabınız başarıyla bağlandı.');
        } catch (error: any) {
            const message = error?.error?.message
                || error?.error?.description
                || 'Google hesabı bağlanamadı.';
            this.toaster.error(message);
        } finally {
            this.linkingGoogle.set(false);
        }
    }
}
