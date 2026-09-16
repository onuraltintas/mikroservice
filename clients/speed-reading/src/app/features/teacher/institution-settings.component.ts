import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { ReactiveFormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { InstitutionsService } from '../../core/services/institutions.service';
import { AuthService } from '../../core/services/auth.service';
import { UsersService } from '../../core/services/users.service';
import { ToasterService } from '../../core/services/toaster.service';
import { Institution } from '../../core/models/institution.model';
import { InstitutionAccessLicense, SubscriptionService } from '../../core/services/subscription.service';
import { DistrictOption, LocationsService, ProvinceOption } from '../../core/services/locations.service';
import { MatSelectModule } from '@angular/material/select';
import { NgxMatSelectSearchModule } from 'ngx-mat-select-search';

@Component({
    selector: 'app-institution-settings',
    standalone: true,
    imports: [
        CommonModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatTabsModule,
        ReactiveFormsModule,
        MatSelectModule,
        NgxMatSelectSearchModule
    ],
    templateUrl: './institution-settings.component.html',
    styles: [`
    .settings-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 32px 24px;
    }

    .tab-content { padding-top: 24px; }
    .contact-email-field { margin-top: 16px; }
    .settings-submit { height: 48px; padding: 0 32px; }
    .button-spinner { display: inline-block; margin-right: 8px; }

    .header-section {
      margin-bottom: 32px;
      
      h1 {
        font-size: 28px;
        font-weight: 700;
        color: #1a1f36;
        margin: 0 0 8px 0;
      }
      
      p {
        color: #697386;
        font-size: 16px;
        margin: 0;
      }
    }

    .main-grid {
        display: grid;
        grid-template-columns: 1fr;
        gap: 24px;
        padding-top: 24px;
        
        @media (min-width: 768px) {
            grid-template-columns: 3fr 2fr;
        }
    }

    .settings-card {
      border-radius: 16px;
      padding: 24px;
      border: 1px solid rgba(0,0,0,0.06);
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      background: white;
      height: 100%;
      
      h2 {
        font-size: 18px;
        font-weight: 600;
        margin-bottom: 24px;
        color: #1a1f36;
        display: flex;
        align-items: center;
        gap: 8px;

        mat-icon {
            color: #3f51b5;
        }
      }
    }

    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .form-row {
        display: grid;
        grid-template-columns: 1fr;
        gap: 16px;
        
        @media (min-width: 600px) {
            grid-template-columns: 1fr 1fr;
        }
    }

    .modern-input {
      width: 100%;
    }

    .form-actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 24px;
      grid-column: 1 / -1;
    }
    
    .loading-shade {
        position: absolute;
        top: 0;
        left: 0;
        bottom: 0;
        right: 0;
        background: rgba(255,255,255,0.7);
        z-index: 10;
        display: flex;
        align-items: center;
        justify-content: center;
      border-radius: 16px;
    }

    .license-summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 16px;
    }

    .license-metric {
      border: 1px solid #e5e7eb;
      border-radius: 12px;
      padding: 16px;

      span { display: block; color: #697386; font-size: 13px; margin-bottom: 6px; }
      strong { color: #1a1f36; font-size: 18px; }
    }

    .license-note {
      margin-top: 20px;
      padding: 14px 16px;
      border-radius: 10px;
      background: #eff6ff;
      color: #1e3a5f;
      font-size: 14px;
    }
  `]
})
export class InstitutionSettingsComponent implements OnInit {
    private fb = inject(FormBuilder);
    private institutionsService = inject(InstitutionsService);
    private authService = inject(AuthService);
    private usersService = inject(UsersService);
    private subscriptionService = inject(SubscriptionService);
    private toaster = inject(ToasterService);
    private locations = inject(LocationsService);

    settingsForm: FormGroup;
    passwordForm: FormGroup;

    loading = signal(false);
    passwordLoading = signal(false);

    institutionId: string | null = null;
    institution = signal<Institution | null>(null);
    accessLicense = signal<InstitutionAccessLicense | null>(null);
    provinces: ProvinceOption[] = [];
    districts: DistrictOption[] = [];
    filteredProvinces: ProvinceOption[] = [];
    filteredDistricts: DistrictOption[] = [];
    readonly provinceFilter = new FormControl('', { nonNullable: true });
    readonly districtFilter = new FormControl('', { nonNullable: true });

    constructor() {
        this.settingsForm = this.fb.group({
            name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
            contactEmail: ['', [Validators.required, Validators.email]],
            phoneNumber: ['', [Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
            provinceId: [''],
            districtId: [''],
            address: ['']
        });

        this.passwordForm = this.fb.group({
            currentPassword: ['', [Validators.required]],
            newPassword: ['', [
                Validators.required,
                Validators.minLength(8),
                Validators.pattern('^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$')
            ]],
            confirmNewPassword: ['', [Validators.required]]
        }, { validators: this.passwordMatchValidator });
    }

    passwordMatchValidator(g: FormGroup) {
        return g.get('newPassword')?.value === g.get('confirmNewPassword')?.value
            ? null : { mismatch: true };
    }

    ngOnInit(): void {
        this.loadLocations();
        const user = this.authService.currentUserValue;

        // Login tokens intentionally contain only identity claims. Resolve the
        // institution from the authenticated profile when the cached user does
        // not already include it.
        const cachedInstitutionId = user?.institutionId;
        if (cachedInstitutionId) {
            this.initializeInstitution(cachedInstitutionId);
            return;
        }

        this.usersService.getMyProfile().subscribe({
            next: profile => {
                const institutionId = profile.institutionId;
                if (institutionId) {
                    this.initializeInstitution(institutionId);
                    return;
                }

                this.showMissingInstitutionError(profile);
            },
            error: error => {
                console.error('Institution profile could not be resolved:', error);
                this.showMissingInstitutionError(user);
            }
        });
    }

    private initializeInstitution(institutionId: string): void {
        this.institutionId = institutionId;
        this.loadInstitution();
        this.loadAccessLicense();
    }

    private showMissingInstitutionError(user: unknown): void {
        this.toaster.error('Kurum bilgisine ulaşılamadı. Lütfen yönetici ile iletişime geçin.');
        console.error('Institution ID not found in user profile:', user);
    }

    loadAccessLicense() {
        this.subscriptionService.getMyInstitutionAccess().subscribe({
            next: license => this.accessLicense.set(license),
            error: () => this.accessLicense.set(null)
        });
    }

    loadInstitution() {
        if (!this.institutionId) return;

        this.loading.set(true);
        this.institutionsService.getInstitutionById(this.institutionId).subscribe({
            next: (data: Institution) => {
                this.institution.set(data);

                this.settingsForm.patchValue({
                    name: data.name,
                    contactEmail: data.contactEmail,
                    phoneNumber: data.phoneNumber,
                    address: data.address,
                    provinceId: data.provinceId,
                    districtId: data.districtId
                }, { emitEvent: false });
                if (data.provinceId) {
                    this.loadDistricts(data.provinceId, data.districtId);
                }
                this.loading.set(false);
            },
            error: (err: any) => {
                console.error('Error loading institution', err);
                this.toaster.error('Kurum bilgileri yüklenirken hata oluştu');
                this.loading.set(false);
            }
        });
    }

    onProvinceChange(): void {
        const provinceId = this.settingsForm.controls['provinceId'].value;
        this.settingsForm.controls['districtId'].reset();
        this.loadDistricts(provinceId);
    }

    private loadLocations(): void {
        this.locations.getProvinces().subscribe({
            next: provinces => {
                this.provinces = provinces;
                this.filteredProvinces = provinces;
            },
            error: () => this.toaster.error('İl seçenekleri yüklenemedi.')
        });
        this.provinceFilter.valueChanges.subscribe(search => {
            this.filteredProvinces = this.filterOptions(this.provinces, search);
        });
        this.districtFilter.valueChanges.subscribe(search => {
            this.filteredDistricts = this.filterOptions(this.districts, search);
        });
    }

    private loadDistricts(provinceId?: string, selectedDistrictId?: string): void {
        this.districtFilter.setValue('');
        this.districts = [];
        this.filteredDistricts = [];
        if (!provinceId) return;
        this.locations.getDistricts(provinceId).subscribe({
            next: districts => {
                this.districts = districts;
                this.filteredDistricts = districts;
                if (selectedDistrictId) this.settingsForm.controls['districtId'].setValue(selectedDistrictId);
            },
            error: () => this.toaster.error('İlçe seçenekleri yüklenemedi.')
        });
    }

    private filterOptions<T extends ProvinceOption>(options: T[], search: string): T[] {
        const normalized = search.trim().toLocaleLowerCase('tr-TR');
        return normalized ? options.filter(option => option.name.toLocaleLowerCase('tr-TR').includes(normalized)) : options;
    }

    licenseName(): string {
        return this.accessLicense()?.plan.name
            ?? ['Bilinmiyor', 'Deneme', 'Basic', 'Premium', 'Kurumsal'][this.institution()?.licenseType ?? 0]
            ?? 'Tanımsız';
    }

    formatDate(value?: Date): string {
        return value ? new Intl.DateTimeFormat('tr-TR').format(value) : 'Tanımlı değil';
    }

    daysRemaining(): string {
        const licenseEndDate = this.accessLicense()?.endDate;
        const endDate = licenseEndDate ? new Date(licenseEndDate) : this.institution()?.subscriptionEndDate;
        if (!endDate) return 'Tanımlı değil';
        const days = Math.ceil((endDate.getTime() - Date.now()) / 86_400_000);
        return days < 0 ? 'Süresi doldu' : `${days} gün kaldı`;
    }

    accessStartDate(): Date | undefined {
        return this.accessLicense()?.startDate
            ? new Date(this.accessLicense()!.startDate)
            : this.institution()?.subscriptionStartDate;
    }

    accessEndDate(): Date | undefined {
        return this.accessLicense()?.endDate
            ? new Date(this.accessLicense()!.endDate)
            : this.institution()?.subscriptionEndDate;
    }

    onSubmit() {
        if (this.settingsForm.invalid || !this.institutionId) return;

        this.loading.set(true);
        const formValue = this.settingsForm.value;

        this.institutionsService.updateInstitution(this.institutionId, {
            name: formValue.name,
            email: formValue.contactEmail,
            phone: formValue.phoneNumber,
            address: formValue.address,
            provinceId: formValue.provinceId || undefined,
            districtId: formValue.districtId || undefined
        }).subscribe({
            next: (updated: any) => {
                this.toaster.success('Kurum ayarları güncellendi');
                this.loading.set(false);
            },
            error: (err: any) => {
                console.error('Error updating settings', err);
                this.toaster.error('Güncelleme başarısız oldu');
                this.loading.set(false);
            }
        });
    }

    changePassword() {
        if (this.passwordForm.invalid) return;

        this.passwordLoading.set(true);
        const val = this.passwordForm.value;

        this.authService.changePassword({
            currentPassword: val.currentPassword,
            newPassword: val.newPassword,
            confirmNewPassword: val.confirmNewPassword
        })
            .pipe(finalize(() => this.passwordLoading.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('Şifreniz başarıyla değiştirildi.');
                    this.passwordForm.reset();
                },
                error: (err) => {
                    console.error('Error changing password', err);
                    const msg = err.error?.detail || err.error?.title || 'Şifre değiştirilemedi.';
                    this.toaster.error(msg);
                }
            });
    }
}
