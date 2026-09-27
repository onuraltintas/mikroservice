import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ToasterService } from '../../../core/services/toaster.service';
import { TeachersService } from '../../../core/services/teachers.service';

@Component({
  selector: 'app-add-teacher-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>Yeni Öğretmen Ekle</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="teacher-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>E-posta</mat-label>
          <input matInput formControlName="email" type="email">
          <mat-error *ngIf="form.get('email')?.hasError('required')">Zorunlu alan</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>

        <p class="info-text">Öğretmene Hızlı Okuma kurum daveti gönderilir. Henüz hesabı yoksa davet bağlantısından Hızlı Okuma’ya kaydolabilir.</p>

        <div *ngIf="error" class="error-message">
          <mat-icon>error</mat-icon>
          {{ error }}
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" (click)="submit()" [disabled]="form.invalid || loading">
        <mat-spinner diameter="20" *ngIf="loading"></mat-spinner>
        {{ loading ? 'Kaydediliyor...' : 'Kaydet' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .teacher-form {
      min-width: 100%;
      padding-top: 8px;
    }

    .form-row {
      display: flex;
      gap: 16px;
    }

    .form-row mat-form-field {
      flex: 1;
    }

    .full-width {
      width: 100%;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #f44336;
      margin-top: 8px;
    }
    .info-text { margin: 0; color: #617271; font-size: 0.875rem; line-height: 1.45; }
  `]
})
export class AddTeacherDialogComponent {
  private fb = inject(FormBuilder);
  private teachersService = inject(TeachersService);
  private dialogRef = inject(MatDialogRef<AddTeacherDialogComponent>);
  private toaster = inject(ToasterService);

  form: FormGroup;
  loading = false;
  error = '';

  constructor() {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.error = '';

    const value = this.form.value;
    this.teachersService.inviteTeacher(value.email.trim()).subscribe({
      next: () => {
        this.toaster.success('Hızlı Okuma öğretmen daveti gönderildi.');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.message || 'Bir hata oluştu';
        this.loading = false;
      }
    });
  }
}
