import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../../core/services/auth.service';
import { LocationService, City, District } from '../../../core/services/location.service';
import { RegisterInstitutionRequest } from '../../../core/models/user.model';
import { strongPasswordValidator } from '../../../shared/validators/password.validator';

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
    MatSelectModule,
    MatProgressSpinnerModule,
    MatDividerModule
  ],
  templateUrl: './register-school.component.html',
  styleUrls: ['./register-school.component.scss']
})
export class RegisterSchoolComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private locationService = inject(LocationService);
  private router = inject(Router);

  isLoading = false;
  error = '';
  successMessage = '';
  hidePassword = true;
  hideConfirmPassword = true;

  cities: City[] = [];
  districts: District[] = [];
  isLoadingDistricts = false;

  registerForm = this.fb.group({
    schoolName: ['', [Validators.required, Validators.minLength(3)]],
    contactEmail: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]], // Standardized Validation
    address: [''], // Optional
    cityId: ['', [Validators.required]],
    districtId: ['', [Validators.required]],
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

  ngOnInit() {
    this.loadCities();

    // Listen for city changes to load districts
    this.registerForm.get('cityId')?.valueChanges.subscribe(cityId => {
      if (cityId) {
        this.loadDistricts(cityId);
      } else {
        this.districts = [];
        this.registerForm.get('districtId')?.reset();
      }
    });
  }

  loadCities() {
    this.locationService.getCities().subscribe({
      next: (cities) => this.cities = cities,
      error: (err) => console.error('Error loading cities', err)
    });
  }

  loadDistricts(cityId: string) {
    this.isLoadingDistricts = true;
    this.registerForm.get('districtId')?.disable();

    this.locationService.getDistricts(cityId).subscribe({
      next: (districts) => {
        this.districts = districts;
        this.isLoadingDistricts = false;
        this.registerForm.get('districtId')?.enable();
      },
      error: (err) => {
        console.error('Error loading districts', err);
        this.isLoadingDistricts = false;
        this.registerForm.get('districtId')?.enable();
      }
    });
  }

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

  onSubmit() {
    if (this.registerForm.invalid) return;

    this.isLoading = true;
    this.error = '';

    const formValue = this.registerForm.value;
    const request: any = { // Using any as DTO might update later or use partial
      schoolName: formValue.schoolName!,
      contactEmail: formValue.contactEmail!,
      phoneNumber: formValue.phoneNumber!,
      address: formValue.address || null,
      cityId: formValue.cityId!,
      districtId: formValue.districtId!,
      firstName: formValue.firstName!,
      lastName: formValue.lastName!,
      adminEmail: formValue.adminEmail!,
      password: formValue.password!,
      acceptTerms: formValue.acceptTerms!,
      acceptKVKK: formValue.acceptKVKK!
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
        this.error = err.error?.message || err.error?.Message || 'Kayıt sırasında bir hata oluştu. Lütfen bilgilerinizi kontrol edin.';
        this.isLoading = false;
      }
    });
  }
}
