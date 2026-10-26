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
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { DistrictOption, LocationService, ProvinceOption } from '../../../../core/services/location.service';
import { getRegistrationErrorMessage } from '../registration-error-message';

@Component({
    selector: 'app-institution-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatSelectModule
    ],
    templateUrl: './institution-register.component.html'
})
export class InstitutionRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private toaster = inject(ToasterService);
    private configService = inject(ConfigurationService);
    private locations = inject(LocationService);
    private platformId = inject(PLATFORM_ID);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);
    provinces = signal<ProvinceOption[]>([]);
    districts = signal<DistrictOption[]>([]);
    institutionTypes = [{ value: 1, label: 'Okul' }, { value: 2, label: 'Dershane' }, { value: 3, label: 'Etüt Merkezi' }, { value: 4, label: 'Online Platform' }];

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
            next: provinces => this.provinces.set(provinces),
            error: () => this.errorMessage.set('İl seçenekleri yüklenemedi. Lütfen yeniden deneyin.')
        });
    }

    onProvinceChange(provinceId: string) {
        this.form.patchValue({ districtId: '' });
        this.districts.set([]);
        if (!provinceId) return;
        this.locations.getDistricts(provinceId).subscribe({
            next: districts => this.districts.set(districts),
            error: () => this.errorMessage.set('İlçe seçenekleri yüklenemedi. Lütfen yeniden deneyin.')
        });
    }

    async onSubmit() {
        if (this.form.invalid) return;

        this.isLoading.set(true);
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
            phone: null
        };

        this.http.post(`${environment.apiUrl}/auth/coaching/register/institution`, payload).subscribe({
            next: () => {
                this.toaster.success('Kayıt işleminiz başarıyla tamamlandı. E-posta adresinizi doğrulamak için size gönderdiğimiz onay linkine tıklayın.', 'Doğrulama Gerekli');
                this.router.navigate(['/auth/login'], { queryParams: { registered: 'true', role: 'institution' } });
            },
            error: (err) => {
                this.isLoading.set(false);
                const msg = getRegistrationErrorMessage(err);
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    togglePassword(e: Event) { e.preventDefault(); this.hidePassword.update(v => !v); }
}
