import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { environment } from '../../../../environments/environment';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

interface SystemSetting {
  key: string;
  value: string;
  category: string;
  description: string;
  isEncrypted: boolean;
}

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
    MatTabsModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent extends BaseComponent implements OnInit {
  private http = inject(HttpClient);
  // toaster inherited from BaseComponent
  private apiUrl = `${environment.apiUrl}/v1/settings`;

  // loading inherited from BaseComponent, but here it's boolean, BaseComponent has loading = signal<boolean>(false)
  // We should use the inherited signal or keep this one if we want to avoid changing template too much.
  // However, BaseComponent pattern encourages using the signal.
  // The template uses `*ngIf="loading"`. If we change `loading` to a signal, we need to update template to `loading()`.
  // Let's update the template as well.

  saving = false;
  hidePassword = true;

  emailSettings: Record<string, any> = {};
  platformSettings: Record<string, any> = {};
  securitySettings: Record<string, any> = {};

  ngOnInit(): void {
    this.loading.set(true);
    this.loadSettings();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadSettings(): void {
    this.http.get<SystemSetting[]>(this.apiUrl)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (settings: SystemSetting[]) => {
          settings.forEach((setting) => {
            if (setting.category === 'Email') {
              this.emailSettings[setting.key] = this.parseValue(setting.value);
            } else if (setting.category === 'Platform') {
              this.platformSettings[setting.key] = this.parseValue(setting.value);
            } else if (setting.category === 'Security') {
              this.securitySettings[setting.key] = this.parseValue(setting.value);
            }
          });
        },
        error: (err) => {
          console.error('Error loading settings:', err);
          this.initializeDefaultSettings();
          this.handleError(err, 'Ayarlar yüklenirken hata oluştu, varsayılan değerler yüklendi');
        }
      });
  }

  parseValue(value: string): any {
    if (value === '' || value === '***') return '';
    if (value === 'true') return true;
    if (value === 'false') return false;
    if (value.trim() !== '' && !isNaN(Number(value))) return Number(value);
    return value;
  }

  initializeDefaultSettings(): void {
    // Email defaults
    this.emailSettings = {
      'Email.SmtpHost': 'smtp.gmail.com',
      'Email.SmtpPort': 587,
      'Email.FromAddress': 'noreply@speedreading.com',
      'Email.FromName': 'Speed Reading Platform',
      'Email.Username': '',
      'Email.Password': '',
      'Email.EnableSsl': true
    };

    // Platform defaults
    this.platformSettings = {
      'Platform.Name': 'Speed Reading Platform',
      'Platform.Url': 'https://speedreading.com',
      'Platform.SupportEmail': 'support@speedreading.com',
      'Platform.DefaultLanguage': 'tr-TR',
      'Platform.SessionTimeout': 60,
      'Platform.MaxUploadSize': 10,
      'Platform.EnableRegistration': true,
      'Platform.RequireEmailConfirmation': true,
      'Platform.EnableMaintenanceMode': false
    };

    // Security defaults
    this.securitySettings = {
      'Security.MinPasswordLength': 8,
      'Security.MaxFailedLoginAttempts': 5,
      'Security.LockoutDuration': 30,
      'Security.JwtExpirationMinutes': 60,
      'Security.RefreshTokenExpirationDays': 7,
      'Security.RequireUppercase': true,
      'Security.RequireDigit': true,
      'Security.RequireSpecialChar': true,
      'Security.Enable2FA': false
    };
  }

  saveSettings(): void {
    this.saving = true;

    const allSettings = {
      ...this.convertToStringValues(this.emailSettings),
      ...this.convertToStringValues(this.platformSettings),
      ...this.convertToStringValues(this.securitySettings)
    };

    this.http.put(this.apiUrl, allSettings)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.saving = false;
          this.handleSuccess('Ayarlar başarıyla kaydedildi');
        },
        error: (err) => {
          console.error('Error saving settings:', err);
          this.saving = false;
          this.handleError(err, 'Ayarlar kaydedilirken hata oluştu');
        }
      });
  }

  convertToStringValues(settings: Record<string, any>): Record<string, string> {
    const result: Record<string, string> = {};
    Object.entries(settings).forEach(([key, value]) => {
      result[key] = String(value);
    });
    return result;
  }

  testEmailConfiguration(): void {
    const testEmail = prompt('Test e-postası gönderilecek adres:');
    if (!testEmail) return;

    this.http.post(`${this.apiUrl}/test-email`, { toEmail: testEmail })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.handleSuccess('Test e-postası başarıyla gönderildi');
        },
        error: (err) => {
          console.error('Error sending test email:', err);
          this.handleError(err, 'Test e-postası gönderilirken hata oluştu');
        }
      });
  }
}
