import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
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
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ isInstitutionAdmin ? 'Mevcut Öğrenciyi Davet Et' : 'Öğrenciyi Davet Et' }}</h2>
    <form [formGroup]="linkForm" (ngSubmit)="onSubmit()">
      <mat-dialog-content>
        <p class="description" *ngIf="isInstitutionAdmin">
          Öğrenci daveti kabul ettiğinde kurumunuza eklenir; isterseniz aynı anda bir öğretmene de atayabilirsiniz.
        </p>
        <p class="description" *ngIf="!isInstitutionAdmin">
          Davet kabul edildiğinde öğrenci sınıf listenize eklenir. Öğretmenler yalnızca kendi sınıflarına davet gönderebilir.
        </p>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Öğrenci e-posta adresi</mat-label>
          <input matInput formControlName="email" type="email" autocomplete="email">
          <mat-error *ngIf="linkForm.get('email')?.hasError('required')">E-posta zorunludur</mat-error>
          <mat-error *ngIf="linkForm.get('email')?.hasError('email')">Geçerli bir e-posta giriniz</mat-error>
        </mat-form-field>
        <mat-form-field *ngIf="isInstitutionAdmin" appearance="outline" class="full-width">
          <mat-label>Sınıf öğretmeni</mat-label>
          <mat-select formControlName="teacherUserId">
            <mat-option [value]="null">Şimdilik atama yapma</mat-option>
            <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher.id">
              {{ teacher.firstName }} {{ teacher.lastName }}
            </mat-option>
          </mat-select>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close type="button">İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="linkForm.invalid || loading">
          {{ loading ? 'Gönderiliyor...' : 'Daveti Gönder' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`.description { color: rgba(0,0,0,.65); margin: 0 0 16px; } .full-width { width: 100%; }`]
})
export class LinkStudentDialogComponent implements OnInit {
  readonly isInstitutionAdmin: boolean;
  linkForm: FormGroup;
  teachers$!: Observable<Teacher[]>;
  loading = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly teachersService: TeachersService,
    private readonly studentsService: StudentsService,
    private readonly authService: AuthService,
    private readonly toaster: ToasterService,
    public readonly dialogRef: MatDialogRef<LinkStudentDialogComponent>
  ) {
    this.isInstitutionAdmin = this.authService.hasRole('InstitutionAdmin') || this.authService.hasRole('InstitutionOwner');
    this.linkForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      teacherUserId: [null]
    });
  }

  ngOnInit(): void {
    if (this.isInstitutionAdmin) this.teachers$ = this.teachersService.getTeachers(undefined, undefined, true);
  }

  onSubmit(): void {
    if (this.linkForm.invalid) return;
    this.loading = true;
    const value = this.linkForm.value;
    const request = this.isInstitutionAdmin
      ? this.studentsService.linkStudent(value.email.trim(), value.teacherUserId || undefined)
      : this.teachersService.linkStudent(value.email.trim());

    request.subscribe({
      next: () => {
        this.toaster.success('Davet gönderildi. Öğrenci kabul ettiğinde liste güncellenecek.');
        this.dialogRef.close(true);
      },
      error: () => this.loading = false
    });
  }
}
