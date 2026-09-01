import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { takeUntil, finalize, switchMap } from 'rxjs/operators';
import { Observable, of } from 'rxjs';
import { UsersService } from '../../../core/services/users.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { UserDetailDto, Institution, RoleDto, UpdateUserProfileRequest } from '../../../core/models/user.model';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-user-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  templateUrl: './user-dialog.component.html',
  styleUrls: ['./user-dialog.component.scss']
})
export class UserDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private usersService = inject(UsersService);
  public ageGroupService = inject(AgeGroupConfigurationService);
  // toaster inherited from BaseComponent

  userForm: FormGroup;
  isEditMode = false;
  saving = false;
  hidePassword = true;
  institutions: Institution[] = [];
  ageGroups: AgeGroupConfiguration[] = [];
  passwordErrorMessages = PASSWORD_ERROR_MESSAGES;

  constructor(
    public dialogRef: MatDialogRef<UserDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: UserDetailDto | null
  ) {
    super();
    this.isEditMode = !!data;
    this.userForm = this.createForm();
  }



  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  createForm(): FormGroup {
    const formConfig: any = {
      firstName: [this.data?.firstName || '', [Validators.required, Validators.minLength(2)]],
      lastName: [this.data?.lastName || '', [Validators.required, Validators.minLength(2)]],
      email: [
        this.isEditMode
          ? { value: this.data?.email || '', disabled: true }
          : this.data?.email || '',
        [Validators.required, Validators.email]
      ],
      phoneNumber: [this.data?.phoneNumber || '', [Validators.pattern(/^[+]?[(]?[0-9]{3}[)]?[-\s.]?[0-9]{3}[-\s.]?[0-9]{4,6}$/)]],
      institutionId: [this.data?.institutionId || null]
    };

    // Add password field only for create mode
    if (!this.isEditMode) {
      formConfig.password = ['', [Validators.required, strongPasswordValidator()]];
      formConfig.roles = [[]];
    } else {
      // Profile fields
      formConfig.dateOfBirth = [this.data?.dateOfBirth ? new Date(this.data.dateOfBirth) : null];
      formConfig.ageGroupId = [{ value: this.data?.ageGroupId || null, disabled: true }];
      formConfig.learningStyle = [this.data?.learningStyle || null];
      formConfig.currentLevel = [{ value: this.data?.currentLevel || null, disabled: true }, [Validators.min(1), Validators.max(5)]];
      formConfig.targetWPM = [{ value: this.data?.targetWPM || null, disabled: true }, [Validators.min(100), Validators.max(1000)]];
      formConfig.targetComprehension = [{ value: this.data?.targetComprehension || null, disabled: true }, [Validators.min(60), Validators.max(100)]];
      formConfig.dailyGoalMinutes = [{ value: this.data?.dailyGoalMinutes || null, disabled: true }, [Validators.min(10), Validators.max(120)]];
    }

    return this.fb.group(formConfig);
  }

  loadInstitutions() {
    this.usersService.getInstitutions()
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

  loadAgeGroups() {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.ageGroups = groups;
        },
        error: (error) => {
          this.toaster.error('Yaş grupları yüklenirken bir hata oluştu');
        }
      });
  }

  roles: RoleDto[] = [];

  loadRoles() {
    this.usersService.getAvailableRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.roles = roles;
        },
        error: (error) => {
          this.toaster.error('Roller yüklenirken bir hata oluştu');
        }
      });
  }

  getRoleDisplayName(role: string): string {
    const displayNames: { [key: string]: string } = {
      'Admin': 'Admin',
      'Teacher': 'Öğretmen',
      'Student': 'Öğrenci',
      'Editor': 'Editör',
      'InstitutionAdmin': 'Kurum Yöneticisi'
    };
    return displayNames[role] || role;
  }

  ngOnInit() {
    this.loadInstitutions();
    this.loadAgeGroups();
    this.loadRoles();
  }

  async onCancel() {
    if (this.userForm.dirty) {
      const confirmed = await this.confirm('Değişiklikler kaydedilmedi. Çıkmak istediğinizden emin misiniz?');
      if (!confirmed) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  translateErrorMessage(message: string): string {
    const translations: { [key: string]: string } = {
      'A user with this email already exists.': 'Bu e-posta adresi ile kayıtlı bir kullanıcı zaten mevcut.',
      'Failed to create user:': 'Kullanıcı oluşturulamadı:',
      'User created but failed to assign roles:': 'Kullanıcı oluşturuldu ancak roller atanamadı:',
      'Institution': 'Kurum',
      'not found': 'bulunamadı'
    };

    let translatedMessage = message;
    Object.keys(translations).forEach(key => {
      translatedMessage = translatedMessage.replace(key, translations[key]);
    });

    return translatedMessage;
  }

  onSave() {
    if (this.userForm.invalid) {
      Object.keys(this.userForm.controls).forEach(key => {
        this.userForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    const formValue = this.userForm.getRawValue();

    // Clean up null/empty values and format data
    const userData: any = {};
    Object.keys(formValue).forEach(key => {
      if (formValue[key] !== null && formValue[key] !== '') {
        // Convert Date object to ISO string for dateOfBirth
        if (key === 'dateOfBirth' && formValue[key] instanceof Date) {
          userData[key] = formValue[key].toISOString();
        } else {
          userData[key] = formValue[key];
        }
      }
    });

    let operation: Observable<unknown>;
    if (this.isEditMode) {
      const basicUpdate = this.usersService.updateUser(this.data!.id, {
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        phoneNumber: formValue.phoneNumber || undefined
      });

      const roleProfileUpdate: UpdateUserProfileRequest = {
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        phoneNumber: formValue.phoneNumber || undefined,
        institutionId: formValue.institutionId || null,
        studentBirthDate: formValue.dateOfBirth
          ? new Date(formValue.dateOfBirth).toISOString()
          : null,
        studentLearningStyle: formValue.learningStyle || null
      };

      const hasRoleProfile = this.data!.roles.some(role => role === 'Student' || role === 'Teacher');
      operation = basicUpdate.pipe(
        switchMap(() => hasRoleProfile
          ? this.usersService.updateUserProfile(this.data!.id, roleProfileUpdate)
          : of(null))
      );
    } else {
      operation = this.usersService.createUser(userData);
    }

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Kullanıcı güncellendi' : 'Kullanıcı oluşturuldu'
          );
          this.dialogRef.close(true);
        },
        error: (_error: unknown) => {
          this.toaster.error('Kullanıcı kaydedilirken bir hata oluştu. Lütfen bilgileri kontrol edin (E-posta adresi kullanımda olabilir).');
        }
      });
  }
}
