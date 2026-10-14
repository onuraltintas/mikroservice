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
import { MatSelectModule } from '@angular/material/select';

import { environment } from '../../../../../environments/environment';
import { LocationService, ProvinceOption, DistrictOption } from '../../../../core/services/location.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { RegistrationLegalConsentComponent } from '../../registration-legal-consent.component';
import { ExistingProductJoinComponent } from '../../existing-product-join.component';
import { RegistrationLegalAcceptance } from '../../../../core/services/identity.service';

@Component({
    selector: 'app-institution-register',
    standalone: true,
    imports: [
        ExistingProductJoinComponent,
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatSelectModule,
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
    private locations = inject(LocationService);
    provinces: ProvinceOption[] = [];
    districts: DistrictOption[] = [];

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    joinExisting = false;
    legalReady = false;
    legalAcceptances: RegistrationLegalAcceptance[] = [];

    form = this.fb.group({
        institutionName: ['', Validators.required],
        institutionType: [1, Validators.required],
        provinceId: ['', Validators.required],
        districtId: ['', Validators.required],
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
        this.locations.getProvinces().subscribe({
            next: provinces => this.provinces = provinces,
            error: () => this.errorMessage.set('İl seçenekleri yüklenemedi. Sayfayı yenileyin.')
        });
    }

    onProvinceChange(): void {
        if (this.errorMessage() === 'İlçe seçenekleri yüklenemedi. İli tekrar seçin.') this.errorMessage.set(null);
        this.form.controls.districtId.setValue('');
        this.districts = [];
        const provinceId = this.form.controls.provinceId.value;
        if (!provinceId) return;
        this.locations.getDistricts(provinceId).subscribe({
            next: districts => { if (this.form.controls.provinceId.value === provinceId) this.districts = districts; },
            error: () => { if (this.form.controls.provinceId.value === provinceId) this.errorMessage.set('İlçe seçenekleri yüklenemedi. İli tekrar seçin.'); }
        });
    }

    async onSubmit() {
        if (this.form.invalid || !this.hasRequiredLegalAcceptances) return;

        this.isLoading.set(true);
        this.errorMessage.set(null);
        const formData = this.form.value;

        const payload = {
            institutionName: formData.institutionName,
            institutionType: formData.institutionType,
            provinceId: formData.provinceId,
            districtId: formData.districtId,
            firstName: formData.firstName,
            lastName: formData.lastName,
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
                const detail = err.error?.description ?? err.error?.message ?? err.error?.Error?.description;
                const msg = typeof detail === 'string' ? detail : 'Kayıt başarısız oldu.';
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
