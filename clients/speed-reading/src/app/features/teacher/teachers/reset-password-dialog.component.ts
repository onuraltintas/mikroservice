import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { environment } from '../../../../environments/environment';

interface DialogData {
  teacherId: string;
  teacherName: string;
}

@Component({
  selector: 'app-reset-password-dialog',
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
    <h2 mat-dialog-title>Şifre Sıfırla</h2>
    <mat-dialog-content>
      <p class="info-text">
        <strong>{{ data.teacherName }}</strong> için yeni şifre belirleyin.
      </p>
      
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Yeni Şifre</mat-label>
          <input matInput formControlName="newPassword" type="password">
          <mat-error *ngIf="form.get('newPassword')?.hasError('required')">Zorunlu alan</mat-error>
          <mat-error *ngIf="form.get('newPassword')?.hasError('minlength')">En az 6 karakter</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Şifre Tekrar</mat-label>
          <input matInput formControlName="confirmPassword" type="password">
          <mat-error *ngIf="form.hasError('mismatch')">Şifreler eşleşmiyor</mat-error>
        </mat-form-field>

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
        {{ loading ? 'Kaydediliyor...' : 'Şifreyi Güncelle' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .info-text {
      margin-bottom: 16px;
      color: #666;
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
  `]
})
export class ResetPasswordDialogComponent {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private dialogRef = inject(MatDialogRef<ResetPasswordDialogComponent>);

  form: FormGroup;
  loading = false;
  error = '';

  constructor(@Inject(MAT_DIALOG_DATA) public data: DialogData) {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmPassword')?.value
      ? null : { mismatch: true };
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.error = '';

    const payload = { newPassword: this.form.get('newPassword')?.value };

    this.http.post(`${environment.apiUrl}/v1/teachers/${this.data.teacherId}/reset-password`, payload).subscribe({
      next: () => {
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.message || 'Bir hata oluştu';
        this.loading = false;
      }
    });
  }
}
