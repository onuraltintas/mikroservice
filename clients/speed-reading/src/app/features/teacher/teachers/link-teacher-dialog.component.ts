import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { environment } from '../../../../environments/environment';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
    selector: 'app-link-teacher-dialog',
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
    <h2 mat-dialog-title>Öğretmen Bağla</h2>
    <mat-dialog-content>
      <p class="info-text">
        Sisteme kayıtlı bir öğretmeni kurumunuza bağlayın.
        Öğretmenin e-posta adresini girin.
      </p>
      
      <form [formGroup]="form" class="link-form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Öğretmen E-posta Adresi</mat-label>
          <input matInput formControlName="email" type="email" placeholder="ogretmen@ornek.com">
          <mat-icon matSuffix>email</mat-icon>
          <mat-error *ngIf="form.get('email')?.hasError('required')">E-posta zorunludur</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>

        <div *ngIf="error" class="error-message">
          <mat-icon>error</mat-icon>
          {{ error }}
        </div>

        <div *ngIf="success" class="success-message">
          <mat-icon>check_circle</mat-icon>
          {{ success }}
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" (click)="submit()" [disabled]="form.invalid || loading">
        <mat-spinner diameter="20" *ngIf="loading"></mat-spinner>
        {{ loading ? 'Bağlanıyor...' : 'Öğretmeni Bağla' }}
      </button>
    </mat-dialog-actions>
  `,
    styles: [`
    .info-text {
      margin-bottom: 16px;
      color: #666;
      line-height: 1.5;
    }

    .link-form {
      width: 100%;
      min-width: 0;
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
      padding: 12px;
      background: #ffebee;
      border-radius: 4px;
    }

    .success-message {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #4caf50;
      margin-top: 8px;
      padding: 12px;
      background: #e8f5e9;
      border-radius: 4px;
    }
  `]
})
export class LinkTeacherDialogComponent {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private dialogRef = inject(MatDialogRef<LinkTeacherDialogComponent>);
    private toaster = inject(ToasterService);

    form: FormGroup;
    loading = false;
    error = '';
    success = '';

    constructor() {
        this.form = this.fb.group({
            email: ['', [Validators.required, Validators.email]]
        });
    }

    submit(): void {
        if (this.form.invalid) return;

        this.loading = true;
        this.error = '';
        this.success = '';

        const payload = { email: this.form.get('email')?.value };

        this.http.post(`${environment.apiUrl}/v1/teachers/link`, payload).subscribe({
            next: () => {
                this.success = 'Öğretmen kurumunuza başarıyla bağlandı!';
                this.loading = false;
                setTimeout(() => {
                    this.dialogRef.close(true);
                }, 1500);
            },
            error: (err) => {
                this.error = err.error?.message || err.error?.Message || 'Öğretmen bulunamadı veya bağlama işlemi başarısız.';
                this.loading = false;
            }
        });
    }
}
