import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import {
  InstitutionLocationOption,
  SpeedReadingInstitutionAccess,
  SpeedReadingInstitutionProfile,
  SpeedReadingInstitutionSettingsService,
} from './speed-reading-institution-settings.service';

@Component({
  selector: 'staff-speed-reading-institution-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './speed-reading-institution-settings.component.html',
  styleUrl: './speed-reading-institution-settings.component.scss',
})
export class SpeedReadingInstitutionSettingsComponent implements OnInit {
  private readonly service = inject(SpeedReadingInstitutionSettingsService);
  readonly profile = signal<SpeedReadingInstitutionProfile | null>(null);
  readonly access = signal<SpeedReadingInstitutionAccess | null>(null);
  readonly provinces = signal<InstitutionLocationOption[]>([]);
  readonly districts = signal<InstitutionLocationOption[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly locationErrorMessage = signal<string | null>(null);
  readonly accessErrorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  name = '';
  email = '';
  phone = '';
  address = '';
  provinceId = '';
  districtId = '';
  provinceSearch = '';
  districtSearch = '';
  private institutionId = '';

  ngOnInit(): void {
    this.loadInstitution();
  }

  get filteredProvinces(): InstitutionLocationOption[] {
    const term = this.provinceSearch.trim().toLocaleLowerCase('tr-TR');
    return term ? this.provinces().filter(item => item.name.toLocaleLowerCase('tr-TR').includes(term)) : this.provinces();
  }

  get filteredDistricts(): InstitutionLocationOption[] {
    const term = this.districtSearch.trim().toLocaleLowerCase('tr-TR');
    return term ? this.districts().filter(item => item.name.toLocaleLowerCase('tr-TR').includes(term)) : this.districts();
  }

  loadInstitution(): void {
    this.errorMessage.set(null);
    this.isLoading.set(true);
    this.service.getMyInstitution().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: institution => {
        this.institutionId = institution.institutionId;
        this.loadInstitutionProfile();
        this.loadAccess();
        this.loadProvinces();
      },
      error: () => this.errorMessage.set('Hızlı Okuma kurum bilgisi alınamadı. Kurum yöneticisi yetkinizi kontrol edip yeniden deneyin.'),
    });
  }

  private loadInstitutionProfile(): void {
    if (!this.institutionId) return;
    this.isLoading.set(true);
    this.service.getInstitutionProfile(this.institutionId)
      .pipe(finalize(() => this.isLoading.set(false))).subscribe({
        next: profile => {
          this.profile.set(profile);
          this.name = profile.name;
          this.email = profile.contactEmail;
          this.phone = profile.phoneNumber ?? '';
          this.address = profile.address ?? '';
          this.provinceId = profile.provinceId ?? '';
          this.districtId = profile.districtId ?? '';
          if (this.provinceId) this.loadDistricts(this.provinceId, this.districtId);
        },
        error: () => this.errorMessage.set('Kurum iletişim bilgileri yüklenemedi. Lütfen yeniden deneyin.'),
      });
  }

  private loadAccess(): void {
    this.accessErrorMessage.set(null);
    this.service.getMySpeedReadingAccess().subscribe({
      next: access => this.access.set(access),
      error: () => {
        this.access.set(null);
        this.accessErrorMessage.set('Hızlı Okuma lisans bilgisi şu anda alınamıyor.');
      },
    });
  }

  private loadProvinces(): void {
    this.locationErrorMessage.set(null);
    this.service.getProvinces().subscribe({
      next: provinces => this.provinces.set(provinces),
      error: () => this.locationErrorMessage.set('İl seçenekleri yüklenemedi.'),
    });
  }

  private loadDistricts(provinceId: string, selectedDistrictId = ''): void {
    this.districts.set([]);
    this.districtSearch = '';
    if (!provinceId) return;
    this.service.getDistricts(provinceId).subscribe({
      next: districts => {
        this.districts.set(districts);
        this.districtId = selectedDistrictId;
      },
      error: () => this.locationErrorMessage.set('İlçe seçenekleri yüklenemedi.'),
    });
  }

  onProvinceChange(): void {
    this.districtId = '';
    this.loadDistricts(this.provinceId);
  }

  saveProfile(): void {
    const name = this.name.trim();
    const email = this.email.trim();
    if (!this.institutionId || name.length < 3 || name.length > 200 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.errorMessage.set('Kurum adı en az 3 karakter olmalı ve geçerli bir iletişim e-postası girilmelidir.');
      return;
    }
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.service.updateInstitutionProfile(this.institutionId, {
      name,
      email,
      phone: this.phone.trim() || undefined,
      address: this.address.trim() || undefined,
      provinceId: this.provinceId || undefined,
      districtId: this.districtId || undefined,
    }).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => {
        this.profile.update(profile => profile ? {
          ...profile,
          name,
          contactEmail: email,
          phoneNumber: this.phone.trim() || null,
          address: this.address.trim() || null,
          provinceId: this.provinceId || null,
          districtId: this.districtId || null,
        } : profile);
        this.successMessage.set('Kurum bilgileri güncellendi.');
      },
      error: () => this.errorMessage.set('Kurum bilgileri güncellenemedi. Yetkinizi kontrol edip yeniden deneyin.'),
    });
  }

  licenseName(): string {
    return this.access()?.plan?.name || 'Lisans bilgisi yok';
  }

  formatDate(value?: string | null): string {
    if (!value) return 'Tanımlı değil';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Tanımlı değil' : new Intl.DateTimeFormat('tr-TR').format(date);
  }

  daysRemaining(): string {
    const end = this.access()?.endDate ?? this.profile()?.subscriptionEndDate;
    if (!end) return 'Tanımlı değil';
    const remaining = Math.ceil((new Date(end).getTime() - Date.now()) / 86_400_000);
    return remaining < 0 ? 'Süresi doldu' : `${remaining} gün kaldı`;
  }
}
