import { Component, DestroyRef, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GoogleSigninButtonModule, SocialAuthService } from '@abacritt/angularx-social-login';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment';
import { SiteAnalyticsService } from '../../../../core/services/site-analytics.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { RegistrationLegalConsentComponent } from '../../registration-legal-consent.component';
import { ExistingProductJoinComponent } from '../../existing-product-join.component';
import { RegistrationLegalAcceptance } from '../../../../core/services/identity.service';

/** A past calendar date between 3 and 100 years ago (same rule as the API). */
export function birthDateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value as string | null;
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return { birthDate: true };
    const date = new Date(Date.UTC(+match[1], +match[2] - 1, +match[3]));
    const today = new Date();
    const latest = Date.UTC(today.getUTCFullYear() - 3, today.getUTCMonth(), today.getUTCDate());
    const earliest = Date.UTC(today.getUTCFullYear() - 100, today.getUTCMonth(), today.getUTCDate());
    return date.getTime() > latest || date.getTime() < earliest ? { birthDate: true } : null;
}

@Component({
    selector: 'app-student-register',
    standalone: true,
    imports: [
        ExistingProductJoinComponent,
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
        GoogleSigninButtonModule, RegistrationLegalConsentComponent
    ],
    templateUrl: './student-register.component.html'
})
export class StudentRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private route = inject(ActivatedRoute, { optional: true });
    private analytics = inject(SiteAnalyticsService);
    private toaster = inject(ToasterService);
    private configService = inject(ConfigurationService);
    private platformId = inject(PLATFORM_ID);
    private authService = inject(AuthService);
    private socialAuthService = inject(SocialAuthService);
    private destroyRef = inject(DestroyRef);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    joinExisting = false;
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required],
        // Roadmap Y8: the student's birth date (yyyy-MM-dd from the date input).
        birthDate: ['', [Validators.required, birthDateValidator]]
    }, { validators: passwordMatchValidator });

    constructor() {
        this.socialAuthService.authState
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(user => {
                if (user?.idToken) {
                    void this.handleGoogleRegistration(user.idToken);
                }
            });
    }

    ngOnInit() {
        if (!isPlatformBrowser(this.platformId)) {
            return;
        }

        this.configService.getPublicConfigurationValue('auth.allowregistration')
            .subscribe({
                next: (val) => {
                    const allowed = val?.replace(/"/g, '').trim().toLowerCase() === 'true';
                    if (!allowed) {
                        this.toaster.warning('Yeni kullanıcı kayıtları şu an kapalıdır.');
                        this.router.navigate(['/auth/register']);
                    }
                },
                error: () => {
                    // Fallback: If config fails, assume allowed (or log error)
                }
            });
    }

    async onSubmit() {
        if (this.form.invalid || !this.hasRequiredLegalAcceptances) return;

        this.isLoading.set(true);
        this.errorMessage.set(null);
        const formData = this.form.value;

        const payload = {
            firstName: formData.firstName,
            lastName: formData.lastName,
            email: formData.email,
            password: formData.password,
            legalAcceptances: this.legalAcceptances,
            birthDate: formData.birthDate,
            utm: this.analytics.registrationUtm()
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/student`, payload).subscribe({
            next: () => {
                this.toaster.success('Koçluk kaydınız tamamlandı. Yeni hesaplarda e-posta adresinizi gönderilen bağlantıyla doğrulayın; mevcut doğrulanmış hesabınız varsa giriş yapabilirsiniz.');
                this.analytics.track('sign_up', { method: 'password', role: 'student' });
                this.router.navigate(['/auth/login'], { queryParams: { registered: 'true', role: 'student', ...this.safeReturnUrl() } });
            },
            error: (err) => {
                this.isLoading.set(false);
                const apiError = err.error;
                const msg = apiError?.description || apiError?.message
                    || (typeof apiError?.Error === 'string' ? apiError.Error : apiError?.Error?.description)
                    || 'Kayıt başarısız oldu. Lütfen yeniden deneyin.';
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    /** A plan chosen on the public site brings the student back to the subscription page after login. */
    private safeReturnUrl(): { returnUrl?: string } {
        const value = this.route?.snapshot.queryParamMap.get('returnUrl') ?? '';
        return /^\/coaching-portal(?:\/[A-Za-z0-9/_?=&-]*)?$/.test(value) ? { returnUrl: value } : {};
    }

    private async handleGoogleRegistration(idToken: string) {
        if (this.isLoading()) return;
        if (!this.hasRequiredLegalAcceptances) {
            this.toaster.info('Önce güncel yasal metinleri inceleyip onaylayın, ardından Google ile kayıt düğmesine tekrar basın.');
            return;
        }

        this.isLoading.set(true);
        this.errorMessage.set(null);

        try {
            const result = await this.authService.loginWithGoogle(idToken, this.legalAcceptances);
            if (result.requiresMfa) {
                this.toaster.info('Bu Google hesabı mevcut. Güvenli giriş için giriş sayfasını kullanın.');
                await this.router.navigate(['/auth/login']);
                return;
            }

            this.toaster.success('Google hesabınızla devam edildi.');
        } catch (error: any) {
            const errorCode = error.error?.code;
            const errorMessage = error.error?.message
                || error.error?.description
                || 'Google ile kayıt başarısız oldu.';

            if (errorCode === 'Identity.RegistrationDisabled') {
                this.toaster.info('Yeni kullanıcı kayıtları şu an kapalıdır.');
            } else {
                this.toaster.error(errorMessage);
            }

            this.errorMessage.set(errorMessage);
        } finally {
            this.isLoading.set(false);
        }
    }

    togglePassword(e: Event) {
        e.preventDefault();
        this.hidePassword.update(v => !v);
    }

    get hasRequiredLegalAcceptances(): boolean {
        return this.legalReady && this.legalAcceptances.length === 3;
    }
}
