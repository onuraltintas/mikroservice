import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize, takeUntil } from 'rxjs/operators';
import { UsersService } from '../../../core/services/users.service';
import { UserDto, UserSessionDto } from '../../../core/models/user.model';
import { BaseComponent } from '../../../core/components/base.component';
import { PASSWORD_ERROR_MESSAGES, strongPasswordValidator } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-user-access-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule
  ],
  template: `
    <h2 mat-dialog-title><mat-icon>security</mat-icon> Erişim ve MFA</h2>
    <mat-dialog-content>
      <p class="user-summary">{{ data.firstName }} {{ data.lastName }} · {{ data.email }}</p>

      <div class="toolbar">
        <button mat-stroked-button color="warn" (click)="revokeAllSessions()" [disabled]="loading() || processing || sessions.length === 0">
          <mat-icon>logout</mat-icon> Tüm oturumları sonlandır
        </button>
        <button mat-stroked-button color="warn" (click)="resetMfa()" [disabled]="loading() || processing || !data.mfaEnabled">
          <mat-icon>lock_reset</mat-icon> MFA'yı sıfırla
        </button>
      </div>

      <section class="password-section">
        <h3>Parola yönetimi</h3>
        <p class="hint">Yönetici tarafından belirlenen yeni parola, kullanıcının mevcut oturumlarını geçersiz kılar.</p>
        <div class="password-row">
          <mat-form-field appearance="outline">
            <mat-label>Yeni parola</mat-label>
            <input matInput [type]="hidePassword ? 'password' : 'text'" [formControl]="newPassword" [disabled]="processing || accessUnavailable" autocomplete="new-password">
            <button mat-icon-button matSuffix type="button" (click)="hidePassword = !hidePassword" [attr.aria-label]="hidePassword ? 'Parolayı göster' : 'Parolayı gizle'">
              <mat-icon>{{ hidePassword ? 'visibility' : 'visibility_off' }}</mat-icon>
            </button>
            <mat-hint>En az 8 karakter; büyük/küçük harf, rakam ve özel karakter.</mat-hint>
            <mat-error *ngIf="newPassword.hasError('required')">Parola zorunludur.</mat-error>
            <mat-error *ngIf="newPassword.hasError('passwordStrength')">{{ passwordStrengthMessage }}</mat-error>
          </mat-form-field>
          <button mat-flat-button color="primary" (click)="resetPassword()" [disabled]="processing || accessUnavailable || newPassword.invalid">
            <mat-icon>vpn_key</mat-icon> Parolayı sıfırla
          </button>
        </div>
      </section>

      <div *ngIf="loading()" class="loading"><mat-spinner diameter="32"></mat-spinner></div>
      <p *ngIf="!loading() && accessUnavailable" class="error-message">{{ accessErrorMessage }}</p>
      <p *ngIf="!loading() && !accessUnavailable && sessions.length === 0" class="empty">Aktif oturum bulunmuyor.</p>
      <mat-list *ngIf="!loading() && !accessUnavailable && sessions.length > 0">
        <mat-list-item *ngFor="let session of sessions">
          <mat-icon matListItemIcon>devices</mat-icon>
          <div matListItemTitle>{{ session.createdByIp || 'IP bilgisi yok' }}</div>
          <div matListItemLine>
            {{ session.createdAt | date:'dd MMM yyyy HH:mm' }} – {{ session.expiresAt | date:'dd MMM yyyy HH:mm' }}
            <span *ngIf="session.isPersistent"> · Kalıcı</span>
            <span *ngIf="session.mfaVerifiedAt"> · MFA doğrulandı</span>
          </div>
                    <button mat-icon-button type="button" color="warn" matListItemMeta (click)="revokeSession(session)" [disabled]="processing" aria-label="Oturumu sonlandır">
            <mat-icon>logout</mat-icon>
          </button>
        </mat-list-item>
      </mat-list>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-raised-button color="primary" mat-dialog-close [disabled]="processing">Kapat</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; }
    mat-dialog-content { min-width: min(680px, 82vw); }
    .user-summary { color: #6b7280; margin-top: 0; }
    .toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin: 18px 0; }
    .password-section { border-top: 1px solid #e5e7eb; border-bottom: 1px solid #e5e7eb; padding: 18px 0; }
    .password-section h3 { margin: 0 0 4px; font-size: 16px; }
    .hint { color: #6b7280; margin: 0 0 12px; font-size: 13px; }
    .password-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .password-row mat-form-field { flex: 1 1 320px; }
    .loading { display: grid; place-items: center; min-height: 100px; }
    .empty { color: #6b7280; padding: 24px 0; }
    .error-message { color: #b91c1c; padding: 24px 0; }
    @media (max-width: 600px) { mat-dialog-content { min-width: 0; } }
  `]
})
export class UserAccessDialogComponent extends BaseComponent implements OnInit {
  private readonly usersService = inject(UsersService);

  sessions: UserSessionDto[] = [];
  processing = false;
  accessUnavailable = false;
  accessErrorMessage = '';
  hidePassword = true;
  newPassword = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, strongPasswordValidator()]
  });
  passwordErrorMessages = PASSWORD_ERROR_MESSAGES;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: UserDto
  ) {
    super();
  }

  ngOnInit(): void {
    this.loadSessions();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadSessions(): void {
    this.accessUnavailable = false;
    this.accessErrorMessage = '';
    this.loading.set(true);
    this.usersService.getSessions(this.data.id)
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: sessions => this.sessions = sessions,
        error: (error: any) => {
          this.accessUnavailable = true;
          this.accessErrorMessage = error?.status === 403
            ? 'Bu yönetim işlemi için SystemAdmin hesabında MFA doğrulaması gerekir.'
            : 'Erişim bilgileri yüklenemedi. Lütfen daha sonra tekrar deneyin.';
          this.toaster.error(this.accessErrorMessage, 4000);
        }
      });
  }

  async revokeSession(session: UserSessionDto): Promise<void> {
    const confirmed = await this.confirm('Bu oturum sonlandırılsın mı?');
    if (!confirmed) return;

    this.processing = true;
    this.usersService.revokeSession(this.data.id, session.id)
      .pipe(takeUntil(this.destroy$), finalize(() => this.processing = false))
      .subscribe({
        next: () => {
          this.sessions = this.sessions.filter(item => item.id !== session.id);
          this.toaster.success('Oturum sonlandırıldı', 2500);
        },
        error: () => this.toaster.error('Oturum sonlandırılamadı', 3000)
      });
  }

  async revokeAllSessions(): Promise<void> {
    const confirmed = await this.confirm('Kullanıcının tüm aktif oturumları sonlandırılsın mı?');
    if (!confirmed) return;

    this.processing = true;
    this.usersService.revokeAllSessions(this.data.id)
      .pipe(takeUntil(this.destroy$), finalize(() => this.processing = false))
      .subscribe({
        next: () => {
          this.sessions = [];
          this.toaster.success('Tüm oturumlar sonlandırıldı', 2500);
        },
        error: () => this.toaster.error('Oturumlar sonlandırılamadı', 3000)
      });
  }

  async resetMfa(): Promise<void> {
    const confirmed = await this.confirm('Kullanıcının MFA ayarı sıfırlansın mı?');
    if (!confirmed) return;

    this.processing = true;
    this.usersService.resetMfa(this.data.id)
      .pipe(takeUntil(this.destroy$), finalize(() => this.processing = false))
      .subscribe({
        next: () => {
          this.data.mfaEnabled = false;
          this.toaster.success('MFA sıfırlandı', 2500);
        },
        error: () => this.toaster.error('MFA sıfırlanamadı', 3000)
      });
  }

  get passwordStrengthMessage(): string {
    const strengthErrors = this.newPassword.getError('passwordStrength') as Record<string, boolean> | null;
    if (!strengthErrors) return '';

    const firstError = Object.keys(strengthErrors)[0];
    return this.passwordErrorMessages[firstError] || 'Güçlü bir parola girin.';
  }

  async resetPassword(): Promise<void> {
    if (this.newPassword.invalid) {
      this.newPassword.markAsTouched();
      return;
    }

    const confirmed = await this.confirm('Kullanıcının parolası değiştirilsin ve mevcut oturumları geçersiz kılınsın mı?');
    if (!confirmed) return;

    this.processing = true;
    this.usersService.adminResetPassword(this.data.id, this.newPassword.value, true)
      .pipe(takeUntil(this.destroy$), finalize(() => this.processing = false))
      .subscribe({
        next: () => {
          this.newPassword.reset();
          this.toaster.success('Kullanıcı parolası güncellendi', 2500);
        },
        error: () => this.toaster.error('Kullanıcı parolası güncellenemedi', 3000)
      });
  }
}
