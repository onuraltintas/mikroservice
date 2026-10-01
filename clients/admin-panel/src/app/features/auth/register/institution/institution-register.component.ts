import { Component, inject, signal, OnInit, PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { RegistrationLegalConsentComponent } from '../../registration-legal-consent.component';
import { RegistrationLegalAcceptance } from '../../../../core/services/identity.service';

@Component({
    selector: 'app-institution-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
        RegistrationLegalConsentComponent
    ],
    templateUrl: './institution-register.component.html'
})
export class InstitutionRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private toaster = inject(ToasterService);
    private configService = inject(ConfigurationService);
    private platformId = inject(PLATFORM_ID);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];

    form = this.fb.group({
        institutionName: ['', Validators.required],
        taxNumber: ['', Validators.required],
        firstName: ['', Validators.required], // Manager First Name
        lastName: ['', Validators.required],  // Manager Last Name
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required]
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
        const formData = this.form.value;

        const payload = {
            institutionName: formData.institutionName,
            taxNumber: formData.taxNumber,
            managerFirstName: formData.firstName,
            managerLastName: formData.lastName,
            email: formData.email,
            password: formData.password,
            legalAcceptances: this.legalAcceptances
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/institution`, payload).subscribe({
            next: () => {
                this.toaster.success('Kayıt işleminiz başarıyla tamamlandı. E-posta adresinizi doğrulamak için size gönderdiğimiz onay linkine tıklayın.', 'Doğrulama Gerekli');
                this.router.navigate(['/auth/login'], { queryParams: { registered: 'true', role: 'institution' } });
            },
            error: (err) => {
                this.isLoading.set(false);
                const msg = err.error?.Error || 'Kayıt başarısız oldu.';
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
