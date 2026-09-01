import { CommonModule } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { UserDetailDto } from '../../../core/models/user.model';

@Component({
  selector: 'app-user-details-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatChipsModule,
    MatDividerModule,
    MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>account_circle</mat-icon>
      Kullanıcı Detayları
    </h2>

    <mat-dialog-content class="details-content">
      <section class="identity-header">
        <div class="avatar">{{ initials }}</div>
        <div>
          <h3>{{ data.firstName }} {{ data.lastName }}</h3>
          <p>{{ data.email }}</p>
        </div>
      </section>

      <mat-divider></mat-divider>

      <dl class="details-grid">
        <div><dt>Durum</dt><dd>{{ data.isActive ? 'Aktif' : 'Pasif' }}</dd></div>
        <div><dt>E-posta</dt><dd>{{ data.emailConfirmed ? 'Onaylı' : 'Onaysız' }}</dd></div>
        <div><dt>MFA</dt><dd>{{ data.mfaEnabled ? 'Açık' : 'Kapalı' }}</dd></div>
        <div><dt>Telefon</dt><dd>{{ data.phoneNumber || '-' }}</dd></div>
        <div><dt>Kurum</dt><dd>{{ data.institutionName || '-' }}</dd></div>
        <div><dt>Son giriş</dt><dd>{{ data.lastLoginAt ? (data.lastLoginAt | date:'dd MMM yyyy HH:mm') : '-' }}</dd></div>
        <div><dt>Oluşturulma</dt><dd>{{ data.createdAt ? (data.createdAt | date:'dd MMM yyyy HH:mm') : '-' }}</dd></div>
      </dl>

      <h4>Roller</h4>
      <mat-chip-set>
        <mat-chip *ngFor="let role of data.roles">{{ role }}</mat-chip>
        <span *ngIf="data.roles.length === 0">Rol atanmamış</span>
      </mat-chip-set>

      <ng-container *ngIf="data.dateOfBirth || data.learningStyle || data.currentLevel || data.targetWPM || data.targetComprehension || data.dailyGoalMinutes">
        <h4>Profil</h4>
        <dl class="details-grid">
          <div *ngIf="data.dateOfBirth"><dt>Doğum tarihi</dt><dd>{{ data.dateOfBirth | date:'dd MMM yyyy' }}</dd></div>
          <div *ngIf="data.learningStyle"><dt>Öğrenme stili</dt><dd>{{ data.learningStyle }}</dd></div>
          <div *ngIf="data.currentLevel"><dt>Seviye</dt><dd>{{ data.currentLevel }}</dd></div>
          <div *ngIf="data.targetWPM"><dt>Hedef WPM</dt><dd>{{ data.targetWPM }}</dd></div>
          <div *ngIf="data.targetComprehension"><dt>Hedef kavrama</dt><dd>%{{ data.targetComprehension }}</dd></div>
          <div *ngIf="data.dailyGoalMinutes"><dt>Günlük hedef</dt><dd>{{ data.dailyGoalMinutes }} dk</dd></div>
        </dl>
      </ng-container>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-raised-button color="primary" mat-dialog-close>Kapat</button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host { display: block; }
    .details-content { min-width: min(620px, 80vw); }
    .identity-header { display: flex; align-items: center; gap: 16px; margin-bottom: 20px; }
    .avatar { width: 56px; height: 56px; border-radius: 50%; display: grid; place-items: center; background: #e8eaf6; color: #3949ab; font-weight: 700; font-size: 20px; }
    h3 { margin: 0 0 4px; font-size: 20px; }
    p { margin: 0; color: #6b7280; }
    h4 { margin: 24px 0 10px; }
    .details-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin: 20px 0 0; }
    dt { color: #6b7280; font-size: 12px; margin-bottom: 4px; }
    dd { margin: 0; font-weight: 500; }
    @media (max-width: 600px) { .details-content { min-width: 0; } .details-grid { grid-template-columns: 1fr; } }
  `]
})
export class UserDetailsDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: UserDetailDto) {}

  get initials(): string {
    return `${this.data.firstName.charAt(0)}${this.data.lastName.charAt(0)}`.toUpperCase();
  }
}
