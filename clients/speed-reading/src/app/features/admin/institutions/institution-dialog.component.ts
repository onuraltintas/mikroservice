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
import { MatDividerModule } from '@angular/material/divider';
import { takeUntil, finalize, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { InstitutionsService } from '../../../core/services/institutions.service';
import { AuthService } from '../../../core/services/auth.service';
import { LocationService, City, District } from '../../../core/services/location.service';
import { UsersService } from '../../../core/services/users.service';
import { Institution } from '../../../core/models/institution.model';
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
    MatSelectModule,
    MatDividerModule
  ],
  templateUrl: './institution-dialog.component.html',
  styleUrls: ['./institution-dialog.component.scss']
})
export class InstitutionDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private institutionsService = inject(InstitutionsService);
  private authService = inject(AuthService);
  private locationService = inject(LocationService);
  private usersService = inject(UsersService);
  // toaster inherited from BaseComponent

  institutionForm: FormGroup;
  isEditMode = false;
  saving = false;
  hidePassword = true;

  cities: City[] = [];
  districts: District[] = [];
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
    this.loadCities();

    if (this.isEditMode && this.data) {
      if (this.data.cityId) {
        this.loadDistricts(this.data.cityId);
      }

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

    this.institutionForm.get('cityId')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(cityId => {
        if (cityId) {
          this.loadDistricts(cityId);
        } else {
          this.districts = [];
          this.institutionForm.get('districtId')?.reset();
        }
      });
  }

  loadCities() {
    this.locationService.getCities()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (cities) => this.cities = cities,
        error: (err) => this.toaster.error('Şehirler yüklenirken bir hata oluştu.')
      });
  }

  loadDistricts(cityId: string) {
    this.institutionForm.get('districtId')?.disable();
    this.locationService.getDistricts(cityId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.institutionForm.get('districtId')?.enable())
      )
      .subscribe({
        next: (districts) => this.districts = districts,
        error: (err) => this.toaster.error('İlçeler yüklenirken bir hata oluştu.')
      });
  }

  createForm(): FormGroup {
    if (this.isEditMode) {
      // Edit Mode - Enhanced
      return this.fb.group({
        name: [this.data?.name || '', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
        contactEmail: [this.data?.contactEmail || '', [Validators.required, Validators.email]],
        phoneNumber: [this.data?.phoneNumber || '', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
        address: [this.data?.address || ''],
        cityId: [this.data?.cityId || '', Validators.required],
        districtId: [this.data?.districtId || '', Validators.required],
        isActive: [this.data?.isActive || false]
        // password control is added dynamically in ngOnInit if admin user is found
      });
    } else {
      // Create Mode - Full Registration Fields
      return this.fb.group({
        // Institution Info
        schoolName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
        contactEmail: ['', [Validators.required, Validators.email]],
        phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
        address: [''],
        cityId: ['', Validators.required],
        districtId: ['', Validators.required],

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
        id: this.data!.id,
        name: formValue.name,
        contactEmail: formValue.contactEmail,
        phoneNumber: formValue.phoneNumber,
        address: formValue.address,
        cityId: formValue.cityId,
        districtId: formValue.districtId,
        isActive: formValue.isActive
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
      const request: any = {
        schoolName: formValue.schoolName,
        contactEmail: formValue.contactEmail,
        phoneNumber: formValue.phoneNumber,
        address: formValue.address || null,
        cityId: formValue.cityId,
        districtId: formValue.districtId,
        firstName: formValue.firstName,
        lastName: formValue.lastName,
        adminEmail: formValue.adminEmail,
        password: formValue.password,
        acceptTerms: true,
        acceptKVKK: true
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
