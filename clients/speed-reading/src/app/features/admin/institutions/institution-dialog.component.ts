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
import { MatDividerModule } from '@angular/material/divider';
import { takeUntil, finalize, switchMap } from 'rxjs/operators';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';
import { Institution } from '../../../core/models/institution.model';
import { RegisterInstitutionRequest } from '../../../core/models/user.model';
import { BaseComponent } from '../../../core/components/base.component';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-institution-dialog',
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
    MatDividerModule
  ],
  templateUrl: './institution-dialog.component.html',
  styleUrls: ['./institution-dialog.component.scss']
})
export class InstitutionDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private institutionsService = inject(InstitutionsService);
  private authService = inject(AuthService);
  private usersService = inject(UsersService);
  // toaster inherited from BaseComponent

  institutionForm: FormGroup;
  isEditMode = false;
  saving = false;
  hidePassword = true;

  passwordErrorMessages = Object.entries(PASSWORD_ERROR_MESSAGES).map(([type, message]) => ({ type, message }));

  // For Edit Mode: To store the ID of the found admin user
  relatedAdminUserId: string | null = null;
  checkingAdminUser = false;

  constructor(
    public dialogRef: MatDialogRef<InstitutionDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Institution | null
  ) {
    super();
    this.isEditMode = !!data;
    this.institutionForm = this.createForm();
  }

  ngOnInit() {
    if (this.isEditMode && this.data) {
      // Try to find the admin user associated with this institution's contact email
      if (this.data.contactEmail) {
        this.checkingAdminUser = true;
        this.usersService.getUsers(this.data.contactEmail)
          .pipe(takeUntil(this.destroy$), finalize(() => this.checkingAdminUser = false))
          .subscribe({
            next: (users) => {
              // Assuming the first match is the relevant one or exact match logic
              const adminUser = users.find(u => u.email === this.data!.contactEmail);
              if (adminUser) {
                this.relatedAdminUserId = adminUser.id;
                // Add password control now that we found an admin
                this.institutionForm.addControl(
                  'password',
                  this.fb.control('', [strongPasswordValidator()])
                );
              }
            },
            error: () => {
              // Silent fail: just means no password reset available
              console.log('Could not resolve admin user for institution');
            }
          });
      }
    }

  }

  createForm(): FormGroup {
    if (this.isEditMode) {
      // Edit Mode - Enhanced
      return this.fb.group({
        name: [this.data?.name || '', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
        contactEmail: [this.data?.contactEmail || '', [Validators.required, Validators.email]],
        phoneNumber: [this.data?.phoneNumber || '', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
        address: [this.data?.address || ''],
        city: [this.data?.city || '', Validators.maxLength(100)],
        district: [this.data?.district || '', Validators.maxLength(100)],
        isActive: [this.data?.isActive || false]
        // password control is added dynamically in ngOnInit if admin user is found
      });
    } else {
      // Create Mode - Full Registration Fields
      return this.fb.group({
        // Institution Info
        schoolName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
        phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
        address: [''],
        city: ['', [Validators.required, Validators.maxLength(100)]],
        district: ['', [Validators.required, Validators.maxLength(100)]],

        // Admin Info
        firstName: ['', [Validators.required, Validators.minLength(2)]],
        lastName: ['', [Validators.required, Validators.minLength(2)]],
        adminEmail: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],

        // Hidden/Implicit
        acceptTerms: [true],
        acceptKVKK: [true]
      });
    }
  }

  async onCancel() {
    if (this.institutionForm.dirty) {
      const confirmed = await this.confirm('Değişiklikler kaydedilmedi. Çıkmak istediğinizden emin misiniz?');
      if (!confirmed) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.institutionForm.invalid) {
      Object.keys(this.institutionForm.controls).forEach(key => {
        this.institutionForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    const formValue = this.institutionForm.value;

    if (this.isEditMode) {
      // Base update operation
      const updateOp = this.institutionsService.updateInstitution(this.data!.id, {
        name: formValue.name,
        email: formValue.contactEmail,
        phone: formValue.phoneNumber,
        address: formValue.address,
        city: formValue.city,
        district: formValue.district
      });

      // Chain password reset if applicable
      let finalOp: any = updateOp;

      if (this.relatedAdminUserId && formValue.password) {
        finalOp = this.usersService.adminResetPassword(this.relatedAdminUserId, formValue.password).pipe(
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
            const msg = (this.relatedAdminUserId && formValue.password)
              ? 'Kurum ve yönetici şifresi güncellendi'
              : 'Kurum güncellendi';
            this.handleSuccess(msg);
            this.dialogRef.close(true);
          },
          error: (err: any) => this.toaster.error('Güncelleme işlemi başarısız oldu. Lütfen tekrar deneyin.')
        });

    } else {
      // Use AuthService for full registration
      const request: RegisterInstitutionRequest = {
        Email: formValue.adminEmail,
        Password: formValue.password,
        FirstName: formValue.firstName,
        LastName: formValue.lastName,
        InstitutionName: formValue.schoolName,
        InstitutionType: 1,
        Phone: formValue.phoneNumber,
        City: `${formValue.city}, ${formValue.district}`
      };

      this.authService.registerInstitution(request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.saving = false)
        )
        .subscribe({
          next: () => {
            this.handleSuccess('Kurum ve yönetici hesabı oluşturuldu');
            this.dialogRef.close(true);
          },
          error: (err: any) => this.toaster.error('Oluşturma işlemi başarısız oldu. Lütfen bilgileri kontrol edin.')
        });
    }
  }
}
