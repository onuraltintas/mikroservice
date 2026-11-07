import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';
import { getApiErrorMessage } from '../../../../core/http/api-error-message';
import { AUTH_VOICE } from '../../auth-voice';
import { PasswordRequirementsComponent } from '../../../../shared/components/password-requirements/password-requirements.component';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';
import { SiteAnalyticsService } from '../../../../core/services/site-analytics.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { RegistrationLegalConsentComponent } from '../../registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../../../core/services/identity.service';
import { RegistrationSuccessComponent } from '../registration-success.component';
import { RegistrationSuccessCopy, grantedNewProductAccess, readVerificationEmailOutcome, registrationSuccessCopy } from '../registration-outcome';

@Component({
    selector: 'app-parent-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
        RegistrationLegalConsentComponent, PasswordRequirementsComponent, RegistrationSuccessComponent
    ],
    templateUrl: './parent-register.component.html'
})
export class ParentRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private analytics = inject(SiteAnalyticsService);
    private toaster = inject(ToasterService);
    /** Paket F L8: server texts are rewritten with "sen" on the Coaching site; eduivme.com keeps them as is. */
    private readonly hostVoice = inject(AUTH_VOICE);
    private configService = inject(ConfigurationService);
    private platformId = inject(PLATFORM_ID);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    /** Set when the address was already verified: the page shows "sign in now" instead of redirecting. */
    verifiedNotice = signal<RegistrationSuccessCopy | null>(null);
    readonly loginQueryParams: Record<string, string> = { registered: 'true', role: 'parent' };
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];
    /** Second review M2b: an existing MFA-protected account becoming a parent is asked for its code. */
    mfaRequired = signal(false);
    readonly mfaCode = new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\d{6}$/)] });

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required],
        phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9]{10,11}$')]]
    }, { validators: passwordMatchValidator });

    ngOnInit() {
        if (!isPlatformBrowser(this.platformId)) {
            return;
        }

        this.configService.getPublicConfigurationValue('auth.allowregistration')
            .subscribe({
                next: (val) => {
                    const allowed = val?.replace(/"/g, '').trim().toLowerCase() === 'true';
                    if (!allowed) {
                        this.toaster.warning('Yeni kayıtlar şu an kapalı.');
                        this.router.navigate(['/auth/register']);
                    }
                }
            });
    }

    async onSubmit() {
        if (this.form.invalid || !this.hasRequiredLegalAcceptances) return;
        if (this.mfaRequired() && (this.mfaCode.invalid || !this.mfaCode.value)) {
            this.mfaCode.markAsTouched();
            this.errorMessage.set('Doğrulama uygulamandaki altı haneli kodu gir.');
            return;
        }

        this.isLoading.set(true);
        this.errorMessage.set(null);
        const formData = this.form.value;

        const payload = {
            firstName: formData.firstName,
            lastName: formData.lastName,
            email: formData.email,
            password: formData.password,
            phoneNumber: formData.phoneNumber,
            legalAcceptances: this.legalAcceptances,
            utm: this.analytics.registrationUtm(),
            ...(this.mfaRequired() ? { mfaCode: this.mfaCode.value } : {})
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/parent`, payload).subscribe({
            next: (response) => {
                const outcome = readVerificationEmailOutcome(response);
                const copy = registrationSuccessCopy(outcome, 'sen');
                // Third review N3: a sign-up only when this request granted the product (not on a retry).
                if (grantedNewProductAccess(response)) this.analytics.track('sign_up', { method: 'password', role: 'parent' });
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
                if (['Auth.ExistingAccountMfaRequired', 'Auth.InvalidMfaCode'].includes(err?.error?.code)) {
                    this.mfaRequired.set(true);
                    this.mfaCode.reset('');
                }
                const msg = getApiErrorMessage(err, 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.', {
                    voice: this.hostVoice,
                    conflict: 'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.'
                });
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    togglePassword(e: Event) {
        e.preventDefault();
        this.hidePassword.update(v => !v);
    }

    get hasRequiredLegalAcceptances(): boolean {
        return this.legalReady && this.legalAcceptances.length === 3;
    }
}
