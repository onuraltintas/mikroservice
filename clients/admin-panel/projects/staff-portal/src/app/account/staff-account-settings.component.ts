import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { StaffAccountProfile, StaffAccountService } from './staff-account.service';

@Component({
  selector: 'staff-account-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './staff-account-settings.component.html',
  styleUrl: './staff-account-settings.component.scss',
})
export class StaffAccountSettingsComponent implements OnInit {
  private readonly service = inject(StaffAccountService);
  readonly profile = signal<StaffAccountProfile | null>(null);
  readonly isLoading = signal(true);
  readonly isSavingProfile = signal(false);
  readonly isChangingPassword = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly profileErrorMessage = signal<string | null>(null);
  readonly passwordErrorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  firstName = '';
  lastName = '';
  phoneNumber = '';
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.errorMessage.set(null);
    this.isLoading.set(true);
    this.service.getMyProfile().pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: profile => {
        this.profile.set(profile);
        this.firstName = profile.firstName ?? '';
        this.lastName = profile.lastName ?? '';
        this.phoneNumber = profile.phoneNumber ?? '';
      },
      error: () => this.errorMessage.set('Profil bilgileri alınamadı. Lütfen yeniden deneyin.'),
    });
  }

  saveProfile(): void {
    const firstName = this.firstName.trim();
    const lastName = this.lastName.trim();
    if (firstName.length < 2 || firstName.length > 50 || lastName.length < 2 || lastName.length > 50) {
      this.profileErrorMessage.set('Ad ve soyad 2–50 karakter olmalıdır.');
      return;
    }
    this.profileErrorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingProfile.set(true);
    this.service.updateMyProfile({ firstName, lastName, phoneNumber: this.phoneNumber.trim() || null })
      .pipe(finalize(() => this.isSavingProfile.set(false))).subscribe({
        next: () => {
          this.profile.update(profile => profile
            ? { ...profile, firstName, lastName, phoneNumber: this.phoneNumber.trim() || null }
            : profile);
          this.successMessage.set('Profil bilgileriniz güncellendi.');
        },
        error: () => this.profileErrorMessage.set('Profil güncellenemedi. Bilgilerinizi kontrol edip yeniden deneyin.'),
      });
  }

  changePassword(): void {
    const passwordPolicy = /^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$/;
    if (!passwordPolicy.test(this.newPassword)) {
      this.passwordErrorMessage.set('Yeni şifre en az 8 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermelidir.');
      return;
    }
    if (!this.currentPassword || this.newPassword !== this.confirmPassword) {
      this.passwordErrorMessage.set('Mevcut şifrenizi girin ve yeni şifreyi doğrulama alanında tekrar edin.');
      return;
    }
    this.passwordErrorMessage.set(null);
    this.successMessage.set(null);
    this.isChangingPassword.set(true);
    this.service.changeMyPassword(this.currentPassword, this.newPassword)
      .pipe(finalize(() => this.isChangingPassword.set(false))).subscribe({
        next: () => {
          this.currentPassword = '';
          this.newPassword = '';
          this.confirmPassword = '';
          this.successMessage.set('Şifreniz güncellendi.');
        },
        error: () => this.passwordErrorMessage.set('Şifre değiştirilemedi. Mevcut şifrenizi kontrol edip yeniden deneyin.'),
      });
  }
}
