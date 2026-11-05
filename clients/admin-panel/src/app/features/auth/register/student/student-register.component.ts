import { Component, DestroyRef, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GoogleSigninButtonModule, SocialAuthService } from '@abacritt/angularx-social-login';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';
import { getApiErrorMessage } from '../../../../core/http/api-error-message';
import { PasswordRequirementsComponent } from '../../../../shared/components/password-requirements/password-requirements.component';

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
import { RegistrationSuccessComponent } from '../registration-success.component';
import { RegistrationSuccessCopy, grantedNewProductAccess, readVerificationEmailOutcome, registrationSuccessCopy } from '../registration-outcome';

@Component({
    selector: 'app-student-register',
    standalone: true,
    imports: [
        ExistingProductJoinComponent,
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
        GoogleSigninButtonModule, RegistrationLegalConsentComponent, PasswordRequirementsComponent, RegistrationSuccessComponent
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
    /** Set when the address was already verified: the page shows "sign in now" instead of redirecting. */
    verifiedNotice = signal<RegistrationSuccessCopy | null>(null);
    joinExisting = false;
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required]
        // Paket A (05.11.2026): no birth date at registration; the first-login wizard asks for it.
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
            utm: this.analytics.registrationUtm()
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/student`, payload).subscribe({
            next: (response) => {
                const outcome = readVerificationEmailOutcome(response);
                const copy = registrationSuccessCopy(outcome, 'sen');
                // Third review N3: a sign-up only when this request granted the product (not on a retry).
                if (grantedNewProductAccess(response)) this.analytics.track('sign_up', { method: 'password', role: 'student' });
                if (outcome === 'notRequired') {
                    // The panel (with focus and a sign-in button) is the message; no duplicate toast.
                    this.isLoading.set(false);
                    this.errorMessage.set(null);
                    this.verifiedNotice.set(copy);
                    return;
                }
                this.toaster.success(copy.message, copy.title);
                this.router.navigate(['/auth/login'], { queryParams: this.loginQueryParams });
            },
            error: (err) => {
                this.isLoading.set(false);
                const msg = getApiErrorMessage(err, 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.', {
                    voice: 'sen',
                    conflict: 'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.'
                });
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    get loginQueryParams(): Record<string, string> {
        return { registered: 'true', role: 'student', ...this.safeReturnUrl() };
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
            const errorMessage = getApiErrorMessage(error, 'Google ile kayıt tamamlanamadı. Biraz sonra yeniden dene.', { voice: 'sen' });

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
