import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { debounceTime, distinctUntilChanged, map, startWith, switchMap } from 'rxjs/operators';
import { Student } from '../../../core/models/student.model';
import { Teacher } from '../../../core/models/teacher.model';
import { StudentsService } from '../../../core/services/students.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
  selector: 'app-student-management-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatAutocompleteModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>Öğrenci bilgilerini düzenle</h2>
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <mat-dialog-content>
        <p class="description">{{ data.student.firstName }} {{ data.student.lastName }}</p>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Okul sınıfı</mat-label>
          <mat-select formControlName="gradeLevel">
            <mat-option [value]="null">Belirtilmedi</mat-option>
            <mat-option *ngFor="let grade of grades" [value]="grade">{{ grade }}. sınıf</mat-option>
          </mat-select>
          <mat-hint>Bu alan okul sınıfıdır; öğrencinin Hızlı Okuma seviyesi bundan ayrı tutulur.</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Sınıf öğretmeni</mat-label>
          <input matInput [formControl]="teacherSearchControl" [matAutocomplete]="teacherOptions" maxlength="100">
          <mat-autocomplete #teacherOptions="matAutocomplete" [displayWith]="displayTeacher" (optionSelected)="selectTeacher($event)">
            <mat-option [value]="null">Atanmamış</mat-option>
            <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher">
              {{ teacher.firstName }} {{ teacher.lastName }}<ng-container *ngIf="teacher.email"> · {{ teacher.email }}</ng-container>
            </mat-option>
          </mat-autocomplete>
          <mat-hint>İsme veya e-postaya göre ara.</mat-hint>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close type="button">İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="loading">
          {{ loading ? 'Kaydediliyor...' : 'Değişiklikleri Kaydet' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`.description { margin: 0 0 16px; } .full-width { width: 100%; }`]
})
export class StudentManagementDialogComponent implements OnInit {
  readonly grades = Array.from({ length: 12 }, (_, index) => index + 1);
  readonly form: FormGroup;
  readonly teacherSearchControl: FormControl<string | Teacher | null>;
  teachers$!: Observable<Teacher[]>;
  loading = false;

  constructor(
    fb: FormBuilder,
    private readonly studentsService: StudentsService,
    private readonly teachersService: TeachersService,
    private readonly toaster: ToasterService,
    private readonly dialogRef: MatDialogRef<StudentManagementDialogComponent>,
    @Inject(MAT_DIALOG_DATA) readonly data: { student: Student }
  ) {
    this.teacherSearchControl = new FormControl<string | Teacher | null>(data.student.teacherName ?? '');
    this.form = fb.group({
      gradeLevel: [data.student.gradeLevel ?? null],
      teacherUserId: [data.student.teacherId ?? null]
    });
  }

  ngOnInit(): void {
    this.teachers$ = this.teacherSearchControl.valueChanges.pipe(
      startWith(this.teacherSearchControl.value),
      map(value => typeof value === 'string' ? value.trim() : ''),
      debounceTime(200),
      distinctUntilChanged(),
      switchMap(searchTerm => this.teachersService.getTeachersPage(
        1,
        25,
        searchTerm || undefined,
        this.data.student.institutionId,
        true).pipe(map(page => page.items)))
    );
  }

  displayTeacher(value: string | Teacher | null): string {
    return typeof value === 'string' || value === null
      ? value ?? ''
      : `${value.firstName} ${value.lastName}`.trim();
  }

  selectTeacher(event: MatAutocompleteSelectedEvent): void {
    const teacher = event.option.value as Teacher | null;
    this.form.get('teacherUserId')?.setValue(teacher?.id ?? null);
  }

  onSubmit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    const value = this.form.getRawValue();
    this.studentsService.updateInstitutionStudent(
      this.data.student.id,
      value.gradeLevel === null ? null : Number(value.gradeLevel),
      value.teacherUserId || null,
      this.data.student.institutionId
    ).subscribe({
      next: () => {
        this.toaster.success('Öğrenci bilgileri güncellendi.');
        this.dialogRef.close(true);
      },
      error: () => this.loading = false
    });
  }
}
