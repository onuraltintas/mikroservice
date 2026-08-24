import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AuthService } from '../../../core/services/auth.service';
import { Observable } from 'rxjs';
import { Teacher } from '../../../core/models/teacher.model';

@Component({
  selector: 'app-link-student-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon class="dialog-icon">link</mat-icon>
      Mevcut Öğrenciyi Bağla
    </h2>
    <form [formGroup]="linkForm" (ngSubmit)="onSubmit()">
      <mat-dialog-content>
        <p class="description">
          Sistemde kayıtlı bir öğrenciyi {{ isAdminOrInstAdmin ? 'bir öğretmene' : 'sınıfınıza' }} eklemek için e-posta adresini girin.
        </p>
        
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Öğrenci E-posta Adresi</mat-label>
          <input matInput formControlName="email" type="email" placeholder="ornek@email.com">
          <mat-icon matPrefix>email</mat-icon>
          <mat-error *ngIf="linkForm.get('email')?.hasError('required')">E-posta zorunludur</mat-error>
          <mat-error *ngIf="linkForm.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>

        <!-- Teacher Selection (Admin Only) -->
        <mat-form-field appearance="outline" class="full-width" *ngIf="isAdminOrInstAdmin">
            <mat-label>Sınıf Öğretmeni</mat-label>
            <mat-select formControlName="teacherId">
                <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher.id">
                    {{ teacher.firstName }} {{ teacher.lastName }}
                </mat-option>
            </mat-select>
            <mat-error *ngIf="linkForm.get('teacherId')?.hasError('required')">Öğretmen seçimi zorunludur</mat-error>
        </mat-form-field>

      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close type="button">İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="linkForm.invalid || loading">
          {{ loading ? 'Bağlanıyor...' : 'Öğrenciyi Bağla' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .dialog-icon {
      vertical-align: middle;
      margin-right: 8px;
      color: var(--mat-primary, #673ab7);
    }
    .description {
      color: rgba(0, 0, 0, 0.6);
      margin-bottom: 16px;
      font-size: 14px;
    }
    .full-width {
      width: 100%;
    }
    mat-dialog-content {
      min-width: 350px;
    }
  `]
})
export class LinkStudentDialogComponent implements OnInit {
  linkForm: FormGroup;
  loading = false;
  isAdminOrInstAdmin = false;
  teachers$!: Observable<Teacher[]>;

  constructor(
    private fb: FormBuilder,
    private teachersService: TeachersService,
    private studentsService: StudentsService,
    private authService: AuthService,
    private toaster: ToasterService,
    public dialogRef: MatDialogRef<LinkStudentDialogComponent>
  ) {
    this.isAdminOrInstAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');

    this.linkForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      teacherId: [null, this.isAdminOrInstAdmin ? [Validators.required] : []]
    });
  }

  ngOnInit(): void {
    if (this.isAdminOrInstAdmin) {
      this.teachers$ = this.teachersService.getTeachers(undefined, undefined, undefined);
    }
  }

  onSubmit(): void {
    if (this.linkForm.valid) {
      this.loading = true;
      const email = this.linkForm.value.email;

      // If Admin, use StudentsService.linkStudent(email, teacherId)
      // If Teacher, use TeachersService.linkStudent(email)

      const request$ = this.isAdminOrInstAdmin
        ? this.studentsService.linkStudent(email, this.linkForm.value.teacherId)
        : this.teachersService.linkStudent(email);

      request$.subscribe({
        next: () => {
          this.toaster.success('Öğrenci başarıyla sınıfınıza eklendi.');
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.loading = false;
          // Handle specific error cases
          if (err.status === 404) {
            this.toaster.error('Bu e-posta adresiyle kayıtlı öğrenci bulunamadı.');
          } else if (err.status === 400) {
            this.toaster.error('Bu kullanıcı öğrenci olarak eklenemez. Lütfen bilgileri kontrol edin.');
          } else if (err.status === 403) {
            this.toaster.error('Bu işlem için yetkiniz yok (veya farklı kurum öğretmeni seçildi).');
          } else {
            this.toaster.error('Öğrenci eklenirken bir hata oluştu. Lütfen tekrar deneyin.');
          }
        }
      });
    }
  }
}
