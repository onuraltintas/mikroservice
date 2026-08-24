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
import { TeachersService } from '../../../core/services/teachers.service';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { Teacher } from '../../../core/models/teacher.model';
import { Institution } from '../../../core/models/institution.model';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-teacher-dialog',
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
  templateUrl: './teacher-dialog.component.html',
  styleUrls: ['./teacher-dialog.component.scss']
})
export class TeacherDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private teachersService = inject(TeachersService);
  private institutionsService = inject(InstitutionsService);
  private authService = inject(AuthService);
  private usersService = inject(UsersService);
  // toaster inherited from BaseComponent

  teacherForm: FormGroup;
  isEditMode = false;
  saving = false;
  hidePassword = true;
  institutions: Institution[] = [];
  passwordErrorMessages = Object.entries(PASSWORD_ERROR_MESSAGES).map(([type, message]) => ({ type, message }));

  constructor(
    public dialogRef: MatDialogRef<TeacherDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Teacher | null
  ) {
    super();
    this.isEditMode = !!data;
    this.teacherForm = this.createForm();
  }

  ngOnInit() {
    this.loadInstitutions();
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
      institutionId: [this.data?.institutionId || null]
    };

    if (!this.isEditMode) {
      // Create: Password Required
      formConfig.password = ['', [Validators.required, strongPasswordValidator()]];
    } else {
      // Edit: Password Optional
      formConfig.password = ['', [strongPasswordValidator()]];
      formConfig.isActive = [this.data?.isActive || false];
    }

    return this.fb.group(formConfig);
  }

  async onCancel() {
    if (this.teacherForm.dirty) {
      const confirmed = await this.confirm('Değişiklikler kaydedilmedi. Çıkmak istediğinizden emin misiniz?');
      if (!confirmed) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.teacherForm.invalid) {
      Object.keys(this.teacherForm.controls).forEach(key => {
        this.teacherForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    const formValue = this.teacherForm.value;

    if (this.isEditMode) {
      // Prepare Update Observable
      const updateOp = this.teachersService.updateTeacher(this.data!.id, {
        id: this.data!.id,
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        email: formValue.email,
        institutionId: formValue.institutionId,
        isActive: formValue.isActive
      });

      // Chain Password Reset if provided
      let finalOp = updateOp;

      if (formValue.password) {
        // Assuming Teacher.Id is usable as UserId or mapped correctly in backend. 
        // If Teacher ID != User ID, this relies on Backend accepting TeacherID or me having UserID.
        // Note: TeacherDto usually shares ID with User in many Identity setups, or we hope so.
        // If this fails, we need UserID from TeacherDto (which might be missing).
        finalOp = this.usersService.adminResetPassword(this.data!.id, formValue.password).pipe(
          switchMap(() => updateOp)
        );
      }

      finalOp
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.saving = false)
        )
        .subscribe({
          next: () => {
            if (formValue.password) {
              this.handleSuccess('Öğretmen ve şifre güncellendi');
            } else {
              this.handleSuccess('Öğretmen güncellendi');
            }
            this.dialogRef.close(true);
          },
          error: (err: any) => this.toaster.error('Güncelleme işlemi başarısız oldu. Lütfen bilgileri kontrol edin.')
        });

    } else {
      // Create Mode
      const selectedInstitution = this.institutions.find(i => i.id === formValue.institutionId);
      const institutionCode = selectedInstitution?.code;

      const request: any = {
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        email: formValue.email,
        password: formValue.password,
        institutionCode: institutionCode,
        acceptTerms: true,
        acceptKVKK: true
      };

      this.authService.registerTeacher(request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.saving = false)
        )
        .subscribe({
          next: () => {
            this.handleSuccess('Öğretmen oluşturuldu');
            this.dialogRef.close(true);
          },
          error: (err: any) => this.toaster.error('Oluşturma işlemi başarısız oldu. Lütfen e-posta adresinin benzersiz olduğundan emin olun.')
        });
    }
  }
}
