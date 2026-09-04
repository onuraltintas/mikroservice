import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../../core/services/auth.service';
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
    MatProgressSpinnerModule,
    MatDividerModule
  ],
  templateUrl: './register-school.component.html',
  styleUrls: ['./register-school.component.scss']
})
export class RegisterSchoolComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  isLoading = false;
  error = '';
  successMessage = '';
  hidePassword = true;
  hideConfirmPassword = true;

  registerForm = this.fb.group({
    schoolName: ['', [Validators.required, Validators.minLength(3)]],
    contactEmail: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]], // Standardized Validation
    address: [''], // Optional
    city: ['', [Validators.required, Validators.maxLength(100)]],
    district: ['', [Validators.required, Validators.maxLength(100)]],
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
      City: `${formValue.city}, ${formValue.district}`
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
