import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatSelectModule } from '@angular/material/select';
import { NgxMatSelectSearchModule } from 'ngx-mat-select-search';
import { AuthService } from '../../../core/services/auth.service';
import { RegisterInstitutionRequest } from '../../../core/models/user.model';
import { strongPasswordValidator } from '../../../shared/validators/password.validator';
import { DistrictOption, LocationsService, ProvinceOption } from '../../../core/services/locations.service';
import { getErrorMessage } from '../../../core/utils/error-message';

@Component({
  selector: 'app-register-school',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatSelectModule,
    NgxMatSelectSearchModule
  ],
  templateUrl: './register-school.component.html',
  styleUrls: ['./register-school.component.scss']
})
export class RegisterSchoolComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private locations = inject(LocationsService);

  isLoading = false;
  error = '';
  successMessage = '';
  hidePassword = true;
  hideConfirmPassword = true;
  provinces: ProvinceOption[] = [];
  districts: DistrictOption[] = [];
  filteredProvinces: ProvinceOption[] = [];
  filteredDistricts: DistrictOption[] = [];
  readonly provinceFilter = new FormControl('', { nonNullable: true });
  readonly districtFilter = new FormControl('', { nonNullable: true });

  registerForm = this.fb.group({
    schoolName: ['', [Validators.required, Validators.minLength(3)]],
    contactEmail: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]], // Standardized Validation
    address: [''], // Optional
    provinceId: ['', Validators.required],
    districtId: ['', Validators.required],
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    adminEmail: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPasswordValidator()]],
    confirmPassword: ['', [Validators.required]],
    acceptTerms: [false, [Validators.requiredTrue]],
    acceptKVKK: [false, [Validators.requiredTrue]]
  }, {
    validators: this.passwordMatchValidator
  });

  passwordMatchValidator(g: any) {
    const password = g.get('password')?.value;
    const confirmControl = g.get('confirmPassword');
    const confirm = confirmControl?.value;

    if (password && confirm && password !== confirm) {
      confirmControl?.setErrors({ ...confirmControl.errors, mismatch: true });
      return { mismatch: true };
    } else {
      if (confirmControl?.hasError('mismatch')) {
        const { mismatch, ...otherErrors } = confirmControl.errors || {};
        confirmControl.setErrors(Object.keys(otherErrors).length ? otherErrors : null);
      }
      return null;
    }
  }

  ngOnInit(): void {
    this.locations.getProvinces().subscribe({
      next: provinces => {
        this.provinces = provinces;
        this.filteredProvinces = provinces;
      },
      error: () => this.error = 'Konum seçenekleri yüklenemedi. Lütfen sayfayı yenileyin.'
    });
    this.provinceFilter.valueChanges.subscribe(search => {
      this.filteredProvinces = this.filterOptions(this.provinces, search);
    });
    this.districtFilter.valueChanges.subscribe(search => {
      this.filteredDistricts = this.filterOptions(this.districts, search);
    });
  }

  onProvinceChange(): void {
    const provinceId = this.registerForm.controls.provinceId.value;
    this.registerForm.controls.districtId.reset();
    this.districtFilter.setValue('');
    this.districts = [];
    this.filteredDistricts = [];
    if (!provinceId) return;

    this.locations.getDistricts(provinceId).subscribe({
      next: districts => {
        this.districts = districts;
        this.filteredDistricts = districts;
      },
      error: () => this.error = 'İlçe seçenekleri yüklenemedi. Lütfen ili yeniden seçin.'
    });
  }

  private filterOptions<T extends ProvinceOption>(options: T[], search: string): T[] {
    const normalized = search.trim().toLocaleLowerCase('tr-TR');
    return normalized ? options.filter(option =>
      option.name.toLocaleLowerCase('tr-TR').includes(normalized)) : options;
  }

  onSubmit() {
    if (this.registerForm.invalid) return;

    this.isLoading = true;
    this.error = '';

    const formValue = this.registerForm.value;
    const request: RegisterInstitutionRequest = {
      Email: formValue.adminEmail!,
      Password: formValue.password!,
      FirstName: formValue.firstName!,
      LastName: formValue.lastName!,
      InstitutionName: formValue.schoolName!,
      InstitutionType: 1,
      Phone: formValue.phoneNumber!,
      ProvinceId: formValue.provinceId!,
      DistrictId: formValue.districtId!
    };

    this.authService.registerInstitution(request).subscribe({
      next: () => {
        this.successMessage = 'Okul kaydınız başarıyla oluşturuldu! E-posta adresinize doğrulama linki gönderildi. Giriş yapabilmek için lütfen e-postanızı doğrulayın.';
        this.isLoading = false;
        setTimeout(() => {
          this.router.navigate(['/auth/login']);
        }, 3000);
      },
      error: (err) => {
        this.error = getErrorMessage(err, 'Kayıt sırasında bir hata oluştu. Lütfen bilgilerinizi kontrol edin.');
        this.isLoading = false;
      }
    });
  }
}
