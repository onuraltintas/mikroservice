import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ToasterService } from '../../../core/services/toaster.service';
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
    MatSelectModule
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Öğrenci Düzenle' : 'Yeni Öğrenci Ekle' }}</h2>
    <form [formGroup]="studentForm" (ngSubmit)="onSubmit()">
      <mat-dialog-content>
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Ad</mat-label>
            <input matInput formControlName="firstName" autocomplete="given-name">
            <mat-error *ngIf="studentForm.get('firstName')?.hasError('required')">Ad zorunludur</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Soyad</mat-label>
            <input matInput formControlName="lastName" autocomplete="family-name">
            <mat-error *ngIf="studentForm.get('lastName')?.hasError('required')">Soyad zorunludur</mat-error>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>E-posta</mat-label>
          <input matInput formControlName="email" type="email" autocomplete="email">
          <mat-hint *ngIf="!isEdit">Öğrenciye güvenli parola oluşturma bağlantısı gönderilir.</mat-hint>
          <mat-error *ngIf="studentForm.get('email')?.hasError('required')">E-posta zorunludur</mat-error>
          <mat-error *ngIf="studentForm.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Sınıf seviyesi</mat-label>
          <mat-select formControlName="gradeLevel">
            <mat-option *ngFor="let grade of grades" [value]="grade">{{ grade }}. sınıf</mat-option>
          </mat-select>
          <mat-error *ngIf="studentForm.get('gradeLevel')?.hasError('required')">Sınıf seviyesi zorunludur</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Sınıf öğretmeni</mat-label>
          <mat-select formControlName="teacherUserId">
            <mat-option [value]="null">Şimdilik atama yapma</mat-option>
            <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher.id">
              {{ teacher.firstName }} {{ teacher.lastName }}
            </mat-option>
          </mat-select>
          <mat-hint>Atama daha sonra bu ekrandan değiştirilebilir.</mat-hint>
        </mat-form-field>
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close type="button">İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="studentForm.invalid || loading">
          {{ loading ? 'Kaydediliyor...' : (isEdit ? 'Değişiklikleri Kaydet' : 'Öğrenciyi Ekle') }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .form-row { display: flex; gap: 16px; }
    .full-width, mat-form-field { width: 100%; }
    .full-width { margin-bottom: 8px; }
    @media (max-width: 520px) { .form-row { flex-direction: column; gap: 0; } }
  `]
})
export class StudentDialogComponent implements OnInit {
  readonly grades = Array.from({ length: 12 }, (_, index) => index + 1);
  isEdit = false;
  studentForm: FormGroup;
  teachers$!: Observable<Teacher[]>;
  loading = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly teachersService: TeachersService,
    private readonly studentsService: StudentsService,
    private readonly toaster: ToasterService,
    public readonly dialogRef: MatDialogRef<StudentDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: { student?: any }
  ) {
    this.isEdit = !!data?.student;
    this.studentForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      gradeLevel: [null, Validators.required],
      teacherUserId: [null]
    });
  }

  ngOnInit(): void {
    this.teachers$ = this.teachersService.getTeachers(undefined, undefined, true);
    if (this.isEdit && this.data.student) {
      this.studentForm.patchValue({
        firstName: this.data.student.firstName,
        lastName: this.data.student.lastName,
        email: this.data.student.email,
        gradeLevel: this.data.student.currentLevel || null,
        teacherUserId: this.data.student.teacherId ?? null
      });
      this.studentForm.get('email')?.disable();
    }
  }

  onSubmit(): void {
    if (this.studentForm.invalid) return;

    this.loading = true;
    const value = this.studentForm.getRawValue();
    const request = {
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      gradeLevel: Number(value.gradeLevel),
      teacherUserId: value.teacherUserId || null
    };
    const operation = this.isEdit
      ? this.studentsService.updateStudent(this.data.student.id, request)
      : this.studentsService.createStudent({ ...request, email: value.email.trim() });

    operation.subscribe({
      next: () => {
        this.toaster.success(this.isEdit ? 'Öğrenci güncellendi.' : 'Öğrenci eklendi; parola oluşturma bağlantısı gönderildi.');
        this.dialogRef.close(true);
      },
      error: () => this.loading = false
    });
  }
}
