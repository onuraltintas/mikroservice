import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminConfiguration, AdminConfigurationService } from '../../../core/services/admin-configuration.service';
import { BaseComponent } from '../../../core/components/base.component';

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

  configurations: AdminConfiguration[] = [];
  values: Record<string, string | number | boolean> = {};
  private originalValues: Record<string, string> = {};
  saving = false;

  ngOnInit(): void {
    this.loadSettings();
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
    return configuration.dataType === 2;
  }

  isNumber(configuration: AdminConfiguration): boolean {
    return configuration.dataType === 1;
  }

  updateValue(configuration: AdminConfiguration, value: string | number | boolean): void {
    this.values[configuration.key] = value;
  }

  saveSettings(): void {
    const changed = this.configurations.filter(configuration =>
      this.toStringValue(this.values[configuration.key]) !== this.originalValues[configuration.key]);
    if (changed.length === 0) {
      this.handleInfo('Kaydedilecek değişiklik bulunmuyor');
      return;
    }

    this.saving = true;
    const updates = changed.map(configuration =>
      this.configurationService.update(configuration.key, this.toStringValue(this.values[configuration.key])));
    forkJoin(updates)
      .pipe(takeUntil(this.destroy$), finalize(() => this.saving = false))
      .subscribe({
        next: () => {
          for (const configuration of changed) {
            this.originalValues[configuration.key] = this.toStringValue(this.values[configuration.key]);
          }
          this.handleSuccess('Ayarlar başarıyla kaydedildi');
        },
        error: error => this.handleError(error, 'Ayarlar kaydedilirken hata oluştu')
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

  private parseValue(value: string, dataType: number): string | number | boolean {
    if (dataType === 2) return value.toLowerCase() === 'true';
    if (dataType === 1) {
      const numberValue = Number(value);
      return Number.isNaN(numberValue) ? 0 : numberValue;
    }
    return value;
  }

  private toStringValue(value: string | number | boolean | undefined): string {
    return value === undefined ? '' : String(value);
  }
}
