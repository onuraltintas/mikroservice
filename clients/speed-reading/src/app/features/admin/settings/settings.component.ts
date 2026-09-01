import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { from } from 'rxjs';
import { concatMap, finalize, takeUntil, toArray } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminConfiguration, AdminConfigurationService } from '../../../core/services/admin-configuration.service';
import { BaseComponent } from '../../../core/components/base.component';
import { AuthService, MfaSetupResponse } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent extends BaseComponent implements OnInit {
  private readonly configurationService = inject(AdminConfigurationService);
  private readonly authService = inject(AuthService);
  private readonly usersService = inject(UsersService);

  configurations: AdminConfiguration[] = [];
  values: Record<string, string | number | boolean | null> = {};
  private originalValues: Record<string, string> = {};
  saving = false;
  mfaEnabled = false;
  mfaSetup: MfaSetupResponse | null = null;
  mfaCurrentPassword = '';
  mfaCode = '';
  mfaRecoveryCodes: string[] = [];
  mfaStatusLoading = true;
  mfaStatusError = false;
  mfaLoading = false;
  mfaEnabling = false;

  ngOnInit(): void {
    this.loadSettings();
    this.loadMfaStatus();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  get groups(): string[] {
    return [...new Set(this.configurations.map(configuration => configuration.group || 'General'))].sort();
  }

  getSettings(group: string): AdminConfiguration[] {
    return this.configurations.filter(configuration => (configuration.group || 'General') === group);
  }

  loadSettings(): void {
    this.loading.set(true);
    this.configurationService.getAll()
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: configurations => {
          this.configurations = [...configurations].sort((left, right) =>
            `${left.group}-${left.key}`.localeCompare(`${right.group}-${right.key}`));
          this.values = {};
          this.originalValues = {};
          for (const configuration of this.configurations) {
            const value = this.parseValue(configuration.value, configuration.dataType);
            this.values[configuration.key] = value;
            this.originalValues[configuration.key] = this.toStringValue(value);
          }
        },
        error: error => this.handleError(error, 'Ayarlar yüklenirken hata oluştu')
      });
  }

  isBoolean(configuration: AdminConfiguration): boolean {
    return configuration.dataType === 2 || String(configuration.dataType).toLowerCase() === 'boolean';
  }

  isNumber(configuration: AdminConfiguration): boolean {
    return configuration.dataType === 1 || String(configuration.dataType).toLowerCase() === 'number';
  }

  updateValue(configuration: AdminConfiguration, value: string | number | boolean | null): void {
    this.values[configuration.key] = value;
  }

  saveSettings(): void {
    const changed = this.configurations.filter(configuration =>
      this.toStringValue(this.values[configuration.key]) !== this.originalValues[configuration.key]);
    if (changed.length === 0) {
      this.handleInfo('Kaydedilecek değişiklik bulunmuyor');
      return;
    }
    if (changed.some(configuration =>
      this.isNumber(configuration) && (this.values[configuration.key] === null || this.values[configuration.key] === ''))) {
      this.handleError(new Error('Sayısal ayarlar boş bırakılamaz'));
      return;
    }

    this.saving = true;
    from(changed)
      .pipe(
        concatMap(configuration =>
          this.configurationService.update(configuration.key, this.toStringValue(this.values[configuration.key]))),
        toArray(),
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          for (const configuration of changed) {
            this.originalValues[configuration.key] = this.toStringValue(this.values[configuration.key]);
          }
          this.handleSuccess('Ayarlar başarıyla kaydedildi');
        },
        error: error => {
          this.loadSettings();
          this.handleError(error, 'Bazı ayarlar kaydedilemedi; güncel değerler yeniden yüklendi');
        }
      });
  }

  refreshCache(): void {
    this.configurationService.refreshCache()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.handleSuccess('Ayar önbelleği yenilendi'),
        error: error => this.handleError(error, 'Ayar önbelleği yenilenirken hata oluştu')
      });
  }

  loadMfaStatus(): void {
    this.mfaStatusLoading = true;
    this.mfaStatusError = false;
    this.usersService.getMyProfile()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: profile => {
          this.mfaEnabled = profile.mfaEnabled === true;
          this.mfaStatusLoading = false;
        },
        error: () => {
          this.mfaStatusError = true;
          this.mfaStatusLoading = false;
        }
      });
  }

  async startMfaSetup(): Promise<void> {
    const password = this.mfaCurrentPassword.trim();
    if (this.mfaLoading || this.mfaEnabled || !password) return;

    this.mfaLoading = true;
    try {
      const setup = await this.authService.startAuthenticatedMfaSetup(password);
      if (!setup.challengeToken) {
        throw new Error('MFA kurulumu için doğrulama isteği alınamadı.');
      }
      this.mfaSetup = setup;
      this.mfaRecoveryCodes = [];
      this.mfaCurrentPassword = '';
    } catch (error: unknown) {
      this.toaster.error(this.getErrorMessage(error, 'MFA kurulumu başlatılamadı.'));
    } finally {
      this.mfaLoading = false;
    }
  }

  async enableMfa(): Promise<void> {
    const setup = this.mfaSetup;
    const code = this.mfaCode.trim();
    if (this.mfaEnabling || !setup?.challengeToken || !setup.setupToken || !/^\d{6}$/.test(code)) return;

    this.mfaEnabling = true;
    try {
      this.mfaRecoveryCodes = await this.authService.enableMfa(
        setup.challengeToken,
        setup.setupToken,
        code);
      this.mfaSetup = null;
      this.mfaCode = '';
      this.mfaEnabled = true;
      this.toaster.success('İki adımlı doğrulama etkinleştirildi. Kurtarma kodlarını güvenli bir yerde saklayın.');
    } catch (error: unknown) {
      this.toaster.error(this.getErrorMessage(error, 'MFA etkinleştirilemedi.'));
    } finally {
      this.mfaEnabling = false;
    }
  }

  isMfaCodeValid(): boolean {
    return /^\d{6}$/.test(this.mfaCode.trim());
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    const response = error as {
      message?: string;
      error?: { message?: string; description?: string };
    };
    return response?.error?.message
      || response?.error?.description
      || response?.message
      || fallback;
  }

  private parseValue(value: string, dataType: number | string): string | number | boolean {
    const normalizedType = String(dataType).toLowerCase();
    if (dataType === 2 || normalizedType === 'boolean') return value.toLowerCase() === 'true';
    if (dataType === 1 || normalizedType === 'number') {
      const numberValue = Number(value);
      return Number.isNaN(numberValue) ? 0 : numberValue;
    }
    return value;
  }

  private toStringValue(value: string | number | boolean | null | undefined): string {
    return value === undefined || value === null ? '' : String(value);
  }
}
