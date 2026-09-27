import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AuthService } from '../../../core/services/auth.service';
import { Observable, of } from 'rxjs';
import { Teacher } from '../../../core/models/teacher.model';
import { catchError, debounceTime, distinctUntilChanged, map, startWith, switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-link-student-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule],
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
          <input matInput [formControl]="teacherSearchControl" [matAutocomplete]="teacherOptions" autocomplete="off">
          <mat-autocomplete #teacherOptions="matAutocomplete" [displayWith]="displayTeacher" (optionSelected)="onTeacherSelected($event)">
            <mat-option [value]="null">Şimdilik atama yapma</mat-option>
            <mat-option *ngFor="let teacher of teachers$ | async" [value]="teacher">
              {{ teacher.firstName }} {{ teacher.lastName }}<ng-container *ngIf="teacher.email"> · {{ teacher.email }}</ng-container>
            </mat-option>
          </mat-autocomplete>
          <mat-hint>{{ teacherSearchError() || 'İsme veya e-postaya göre arayın; yalnızca aktif öğretmenler gösterilir.' }}</mat-hint>
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
  readonly teacherSearchControl = new FormControl<string | Teacher | null>('');
  readonly teacherSearchError = signal<string | null>(null);
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
    if (!this.isInstitutionAdmin) return;

    const institutionId = this.authService.currentUserValue?.institutionId;
    this.teachers$ = this.teacherSearchControl.valueChanges.pipe(
      startWith(this.teacherSearchControl.value),
      map(value => typeof value === 'string' ? value.trim() : ''),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(searchTerm => {
        this.teacherSearchError.set(null);
        return this.teachersService.getTeachersPage(1, 25, searchTerm || undefined, institutionId, true).pipe(
          map(page => page.items),
          catchError(() => {
            this.teacherSearchError.set('Öğretmen listesi yüklenemedi. Aramanızı daraltıp yeniden deneyin.');
            return of([]);
          })
        );
      })
    );
  }

  displayTeacher(value: string | Teacher | null): string {
    return typeof value === 'string' || value === null
      ? value ?? ''
      : `${value.firstName} ${value.lastName}`.trim();
  }

  onTeacherSelected(event: MatAutocompleteSelectedEvent): void {
    const teacher = event.option.value as Teacher | null;
    this.linkForm.get('teacherUserId')?.setValue(teacher?.id ?? null);
  }

  onSubmit(): void {
    if (this.linkForm.invalid) return;
    this.loading = true;
    const value = this.linkForm.value;
    let request: Observable<any>;
    if (this.isInstitutionAdmin) {
      request = this.studentsService.linkStudent(
        value.email.trim(),
        this.authService.currentUserValue?.institutionId,
        value.teacherUserId || undefined);
    } else {
      request = this.teachersService.linkStudent(value.email.trim());
    }

    request.subscribe({
      next: () => {
        this.toaster.success('Davet gönderildi. Öğrenci kabul ettiğinde liste güncellenecek.');
        this.dialogRef.close(true);
      },
      error: error => {
        this.loading = false;
        this.toaster.error(error?.error?.message || error?.message || 'Davet gönderilemedi. Kurum bilgilerinizi kontrol edip yeniden deneyin.');
      }
    });
  }
}
