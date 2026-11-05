import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';
import { getApiErrorMessage } from '../../../../core/http/api-error-message';
import { PasswordRequirementsComponent } from '../../../../shared/components/password-requirements/password-requirements.component';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';
import { SiteAnalyticsService } from '../../../../core/services/site-analytics.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { RegistrationLegalConsentComponent } from '../../registration-legal-consent.component';
import { ExistingProductJoinComponent } from '../../existing-product-join.component';
import { RegistrationLegalAcceptance } from '../../../../core/services/identity.service';
import { RegistrationSuccessComponent } from '../registration-success.component';
import { RegistrationSuccessCopy, readVerificationEmailOutcome, registrationSuccessCopy } from '../registration-outcome';

@Component({
    selector: 'app-teacher-register',
    standalone: true,
    imports: [
        ExistingProductJoinComponent,
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatSelectModule, MatProgressSpinnerModule,
        RegistrationLegalConsentComponent, PasswordRequirementsComponent, RegistrationSuccessComponent
    ],
    templateUrl: './teacher-register.component.html'
})
export class TeacherRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private analytics = inject(SiteAnalyticsService);
    private toaster = inject(ToasterService);
    private configService = inject(ConfigurationService);
    private platformId = inject(PLATFORM_ID);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    /** Set when the address was already verified: the page shows "sign in now" instead of redirecting. */
    verifiedNotice = signal<RegistrationSuccessCopy | null>(null);
    readonly loginQueryParams: Record<string, string> = { registered: 'true', role: 'teacher' };
    joinExisting = false;
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required],
        branch: ['', Validators.required]
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
                        this.toaster.warning('Yeni kullanıcı kayıtları şu an kapalıdır.');
                        this.router.navigate(['/auth/register']);
                    }
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
            branch: formData.branch,
            legalAcceptances: this.legalAcceptances,
            utm: this.analytics.registrationUtm()
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/teacher`, payload).subscribe({
            next: (response) => {
                const outcome = readVerificationEmailOutcome(response);
                const copy = registrationSuccessCopy(outcome, 'siz');
                this.analytics.track('sign_up', { method: 'password', role: 'teacher' });
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
                const msg = getApiErrorMessage(err, 'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.', {
                    voice: 'siz',
                    conflict: 'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifrenizi sıfırlayabilirsiniz.'
                });
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    togglePassword(e: Event) { e.preventDefault(); this.hidePassword.update(v => !v); }

    get hasRequiredLegalAcceptances(): boolean {
        return this.legalReady && this.legalAcceptances.length === 3;
    }
}
