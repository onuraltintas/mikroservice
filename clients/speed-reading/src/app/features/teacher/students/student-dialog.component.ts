import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AuthService } from '../../../core/services/auth.service';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';
import { Observable } from 'rxjs';
import { Teacher } from '../../../core/models/teacher.model';

@Component({
  selector: 'app-student-dialog',
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
    <h2 mat-dialog-title>{{ isEdit ? 'Öğrenci Düzenle' : 'Yeni Öğrenci Ekle' }}</h2>
    <form [formGroup]="studentForm" (ngSubmit)="onSubmit()">
      <mat-dialog-content>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Ad</mat-label>
            <input matInput formControlName="firstName" placeholder="Örn: Ahmet">
            <mat-error *ngIf="studentForm.get('firstName')?.hasError('required')">Ad zorunludur</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Soyad</mat-label>
            <input matInput formControlName="lastName" placeholder="Örn: Yılmaz">
            <mat-error *ngIf="studentForm.get('lastName')?.hasError('required')">Soyad zorunludur</mat-error>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>E-posta</mat-label>
          <input matInput formControlName="email" type="email" placeholder="ahmet@ornek.com">
          <mat-error *ngIf="studentForm.get('email')?.hasError('required')">E-posta zorunludur</mat-error>
          <mat-error *ngIf="studentForm.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>

        <!-- Teacher Selection (Admin Only) -->
        <mat-form-field appearance="outline" class="full-width" *ngIf="isAdminOrInstAdmin">
            <mat-label>Sınıf Öğretmeni</mat-label>
            <mat-select formControlName="teacherId">
                <mat-option [value]="null">Atanmamış (Boş)</mat-option>
                <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher.id">
                    {{ teacher.firstName }} {{ teacher.lastName }}
                </mat-option>
            </mat-select>
            <mat-hint>Öğrencinin atanacağı öğretmeni seçiniz.</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ isEdit ? 'Yeni Şifre (Değiştirmek istemiyorsanız boş bırakın)' : 'Şifre' }}</mat-label>
          <input matInput formControlName="password" [type]="hidePassword ? 'password' : 'text'">
          <mat-hint *ngIf="!isEdit">En az 8 karakter, büyük/küçük harf, rakam ve özel karakter</mat-hint>
          <button mat-icon-button matSuffix (click)="hidePassword = !hidePassword" type="button">
            <mat-icon>{{hidePassword ? 'visibility_off' : 'visibility'}}</mat-icon>
          </button>
          <mat-error *ngIf="studentForm.get('password')?.hasError('required')">Şifre zorunludur</mat-error>
          <mat-error *ngIf="studentForm.get('password')?.getError('passwordStrength')?.minLength">{{ passwordErrorMessages['minLength'] }}</mat-error>
          <mat-error *ngIf="studentForm.get('password')?.getError('passwordStrength')?.uppercase">{{ passwordErrorMessages['uppercase'] }}</mat-error>
          <mat-error *ngIf="studentForm.get('password')?.getError('passwordStrength')?.lowercase">{{ passwordErrorMessages['lowercase'] }}</mat-error>
          <mat-error *ngIf="studentForm.get('password')?.getError('passwordStrength')?.digit">{{ passwordErrorMessages['digit'] }}</mat-error>
          <mat-error *ngIf="studentForm.get('password')?.getError('passwordStrength')?.specialChar">{{ passwordErrorMessages['specialChar'] }}</mat-error>
        </mat-form-field>
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close type="button">İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="studentForm.invalid || loading">
          {{ loading ? 'Kaydediliyor...' : 'Kaydet' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .form-row {
      display: flex;
      gap: 16px;
    }
    .full-width {
      width: 100%;
      margin-bottom: 8px;
    }
    mat-form-field {
      width: 100%;
    }
  `]
})
export class StudentDialogComponent implements OnInit {
  studentForm: FormGroup;
  isEdit: boolean = false;
  loading: boolean = false;
  hidePassword = true;
  passwordErrorMessages = PASSWORD_ERROR_MESSAGES;

  isAdminOrInstAdmin = false;
  teachers$!: Observable<Teacher[]>;

  constructor(
    private fb: FormBuilder,
    private teachersService: TeachersService,
    private studentsService: StudentsService,
    private toaster: ToasterService,
    private authService: AuthService,
    public dialogRef: MatDialogRef<StudentDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.isEdit = !!data?.student;
    this.isAdminOrInstAdmin = this.authService.hasRole('Admin') || this.authService.hasRole('InstitutionAdmin');

    this.studentForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      teacherId: [null], // Field for Admin
      password: [this.isEdit ? '' : '', this.isEdit ? [strongPasswordValidator()] : [Validators.required, strongPasswordValidator()]]
    });
  }

  ngOnInit(): void {
    // If admin, fetch teachers
    if (this.isAdminOrInstAdmin) {
      if (this.isAdminOrInstAdmin) {
        this.teachers$ = this.teachersService.getTeachers(undefined, undefined, undefined);
      }
    }

    if (this.isEdit && this.data.student) {
      this.studentForm.patchValue({
        firstName: this.data.student.firstName,
        lastName: this.data.student.lastName,
        email: this.data.student.email,
        teacherId: this.data.student.teacherId
      });
    }
  }

  onSubmit(): void {
    if (this.studentForm.valid) {
      this.loading = true;
      const formValue = this.studentForm.value;

      if (this.isEdit) {
        // Update
        const updateData: any = {
          firstName: formValue.firstName,
          lastName: formValue.lastName,
          email: formValue.email,
          teacherId: formValue.teacherId // Include teacher update if Admin
        };
        if (formValue.password) {
          updateData.password = formValue.password;
        }

        // Use correct service based on context or use studentsService for everything?
        // teachersService.updateStudent might be Teacher-restricted. 
        // studentsService.updateStudent is generic.

        this.studentsService.updateStudent(this.data.student.id, updateData).subscribe({
          next: () => {
            this.toaster.success('Öğrenci başarıyla güncellendi');
            this.dialogRef.close(true);
          },
          error: (err) => {
            // Error handled globally by interceptor
            this.loading = false;
          }
        });
      } else {
        // Create
        // Use StudentsService for creation to allow passing teacherId
        const createData = {
          ...formValue,
          teacherId: this.isAdminOrInstAdmin ? formValue.teacherId : undefined
        };

        this.studentsService.createStudent(createData).subscribe({
          next: () => {
            this.toaster.success('Öğrenci başarıyla oluşturuldu');
            this.dialogRef.close(true);
          },
          error: (err) => {
            // Error handled globally by interceptor
            this.loading = false;
          }
        });
      }
    }
  }
}
