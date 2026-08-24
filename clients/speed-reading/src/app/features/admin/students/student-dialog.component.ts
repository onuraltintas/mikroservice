import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { takeUntil, finalize, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { StudentsService } from '../../../core/services/students.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { UsersService } from '../../../core/services/users.service';
import { Student } from '../../../core/models/student.model';
import { Institution } from '../../../core/models/institution.model';
import { Teacher } from '../../../core/models/teacher.model';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-student-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatSelectModule
  ],
  templateUrl: './student-dialog.component.html',
  styleUrls: ['./student-dialog.component.scss']
})
export class StudentDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private studentsService = inject(StudentsService);
  private institutionsService = inject(InstitutionsService);
  private teachersService = inject(TeachersService);
  private usersService = inject(UsersService);
  // toaster inherited from BaseComponent

  studentForm: FormGroup;
  isEditMode = false;
  saving = false;
  hidePassword = true;
  institutions: Institution[] = [];
  teachers: Teacher[] = [];
  passwordErrorMessages = PASSWORD_ERROR_MESSAGES;

  constructor(
    public dialogRef: MatDialogRef<StudentDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Student | null
  ) {
    super();
    this.isEditMode = !!data;
    this.studentForm = this.createForm();
  }

  ngOnInit() {
    this.loadInstitutions();

    // Load teachers based on current institution (or all if no institution)
    const currentInstitutionId = this.data?.institutionId;
    this.loadTeachers(currentInstitutionId || undefined);

    // Subscribe to Institution changes for cascading
    this.studentForm.get('institutionId')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(institutionId => {
        this.studentForm.get('teacherId')?.setValue(null); // Clear teacher when institution changes
        this.loadTeachers(institutionId || undefined);
      });
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadInstitutions() {
    this.institutionsService.getInstitutions(undefined, true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (institutions) => {
          this.institutions = institutions;
        },
        error: (error) => {
          this.toaster.error('Kurumlar yüklenirken bir hata oluştu');
        }
      });
  }

  loadTeachers(institutionId?: string) {
    // Note: Not filtering by isActive to show all teachers
    this.teachersService.getTeachers(undefined, institutionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (teachers) => {
          this.teachers = teachers;
        },
        error: (error) => {
          this.toaster.error('Öğretmenler yüklenirken bir hata oluştu');
        }
      });
  }

  createForm(): FormGroup {
    const formConfig: any = {
      firstName: [
        this.data?.firstName || '',
        [Validators.required, Validators.minLength(2)]
      ],
      lastName: [
        this.data?.lastName || '',
        [Validators.required, Validators.minLength(2)]
      ],
      email: [
        this.data?.email || '',
        [Validators.required, Validators.email]
      ],
      institutionId: [this.data?.institutionId || null],
      teacherId: [this.data?.teacherId || null],
      currentLevel: [
        this.data?.currentLevel || 1,
        [Validators.required, Validators.min(1)]
      ],
      targetWPM: [
        this.data?.targetWPM || 250,
        [Validators.required, Validators.min(0)]
      ],
      targetComprehension: [
        this.data?.targetComprehension || 70,
        [Validators.required, Validators.min(0), Validators.max(100)]
      ],
      dailyGoalMinutes: [
        this.data?.dailyGoalMinutes || 20,
        [Validators.required, Validators.min(5)]
      ],
      learningStyle: [
        this.data?.learningStyle || 'visual',
        [Validators.required]
      ]
    };

    // Password field is required for new, optional for edit
    if (!this.isEditMode) {
      formConfig.password = ['', [Validators.required, strongPasswordValidator()]];
    } else {
      formConfig.password = ['', [strongPasswordValidator()]];
    }

    // Add isActive field only for edit mode
    if (this.isEditMode) {
      formConfig.isActive = [this.data?.isActive || false];
    }

    return this.fb.group(formConfig);
  }

  async onCancel() {
    if (this.studentForm.dirty) {
      const confirmed = await this.confirm('Değişiklikler kaydedilmedi. Çıkmak istediğinizden emin misiniz?');
      if (!confirmed) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.studentForm.invalid) {
      Object.keys(this.studentForm.controls).forEach(key => {
        this.studentForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    const formValue = this.studentForm.value;

    if (this.isEditMode && this.data) {
      // Edit Mode with Potential Password Reset
      let updateObs$: any = of(null); // Default to start chain

      if (formValue.password) {
        // If password is provided, reset it first
        updateObs$ = this.usersService.adminResetPassword(this.data.id, formValue.password);
      }

      updateObs$.pipe(
        switchMap(() => {
          const updateRequest = {
            id: this.data!.id,
            firstName: formValue.firstName,
            lastName: formValue.lastName,
            email: formValue.email,
            institutionId: formValue.institutionId,
            teacherId: formValue.teacherId,
            currentLevel: formValue.currentLevel,
            targetWPM: formValue.targetWPM,
            targetComprehension: formValue.targetComprehension,
            dailyGoalMinutes: formValue.dailyGoalMinutes,
            learningStyle: formValue.learningStyle,
            isActive: formValue.isActive
          };
          return this.studentsService.updateStudent(this.data!.id, updateRequest);
        }),
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      ).subscribe({
        next: () => {
          const msg = formValue.password ? 'Şifre ve öğrenci güncellendi' : 'Öğrenci güncellendi';
          this.handleSuccess(msg);
          this.dialogRef.close(true);
        },
        error: (error: any) => {
          this.toaster.error('Güncelleme işlemi başarısız oldu. Lütfen bilgileri kontrol edin.');
        }
      });

    } else {
      // Create Mode
      this.studentsService.createStudent({
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        email: formValue.email,
        password: formValue.password,
        institutionId: formValue.institutionId,
        teacherId: formValue.teacherId,
        currentLevel: formValue.currentLevel,
        targetWPM: formValue.targetWPM,
        targetComprehension: formValue.targetComprehension,
        dailyGoalMinutes: formValue.dailyGoalMinutes,
        learningStyle: formValue.learningStyle
      })
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.saving = false)
        )
        .subscribe({
          next: () => {
            this.handleSuccess('Öğrenci oluşturuldu');
            this.dialogRef.close(true);
          },
          error: (error) => {
            this.toaster.error('Oluşturma işlemi başarısız oldu. Lütfen e-posta adresinin benzersiz olduğundan emin olun.');
          }
        });
    }
  }
}
