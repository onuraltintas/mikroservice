import { AfterViewInit, Component, inject, OnDestroy, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
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
import { ToasterService } from '../../../core/services/toaster.service';
import { RegisterTeacherRequest } from '../../../core/models/user.model';
import {
  GoogleIdentityCallback,
  GoogleIdentityService,
  GoogleIdentityResponse
} from '../../../core/services/google-identity.service';
import { strongPasswordValidator } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-register-teacher',
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
  templateUrl: './register-teacher.component.html',
  styleUrl: './register-teacher.component.scss'
})
export class RegisterTeacherComponent implements AfterViewInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  private toaster = inject(ToasterService);
  private googleIdentity = inject(GoogleIdentityService);
  private readonly googleCallback: GoogleIdentityCallback = (response: GoogleIdentityResponse) =>
    this.handleGoogleResponse(response);

  isLoading = false;
  error = '';
  successMessage = '';
  showInstitutionCode = false;
  hidePassword = true;
  hideConfirmPassword = true;

  registerForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPasswordValidator()]],
    confirmPassword: ['', [Validators.required]],
    joinInstitution: [false],
    institutionCode: [''], // Optional initially
    acceptTerms: [false, [Validators.requiredTrue]],
    acceptKVKK: [false, [Validators.requiredTrue]]
  }, {
    validators: this.passwordMatchValidator
  });

  constructor() {
    this.registerForm.get('joinInstitution')?.valueChanges.subscribe(checked => {
      this.showInstitutionCode = !!checked;
      const codeControl = this.registerForm.get('institutionCode');
      if (checked) {
        codeControl?.setValidators([
          Validators.required,
          Validators.minLength(6),
          Validators.pattern('^[A-Za-z0-9]*$')
        ]);
      } else {
        codeControl?.clearValidators();
        codeControl?.setValue('');
      }
      codeControl?.updateValueAndValidity();
    });
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const buttonElement = document.getElementById('google-register-teacher-button');
    if (!buttonElement) {
      console.error('Google Teacher Sign-Up button element not found');
      return;
    }

    this.googleIdentity.renderButton(buttonElement, 'signup_with', this.googleCallback)
      .catch(error => {
        console.error('Error initializing Google Teacher Sign-Up:', error);
        this.error = 'Google ile öğretmen kaydı şu anda kullanılamıyor.';
      });
  }

  ngOnDestroy(): void {
    this.googleIdentity.clearCallback(this.googleCallback);
  }

  private handleGoogleResponse(response: any): void {
    this.isLoading = true;
    this.error = '';
    this.successMessage = '';

    // Pass 'Teacher' role to enforce correct registration/login context
    this.authService.googleAuth(response.credential, 'Teacher').subscribe({
      next: (authResponse) => {
        this.successMessage = 'Google ile giriş başarılı! Yönlendiriliyorsunuz...';
        setTimeout(() => {
          this.router.navigate(['/teacher/dashboard']);
        }, 1500);
      },
      error: (err) => {
        // Show clear error message from backend (e.g., "User already exists")
        this.error = err.error?.message || err.error?.Message || 'Google girişi başarısız oldu.';
        this.isLoading = false;
        this.toaster.error(this.error, 5000);
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
    const request: RegisterTeacherRequest = {
      firstName: formValue.firstName!,
      lastName: formValue.lastName!,
      email: formValue.email!,
      password: formValue.password!,
      acceptTerms: formValue.acceptTerms!,
      acceptKVKK: formValue.acceptKVKK!,
      institutionCode: formValue.joinInstitution ? formValue.institutionCode! : undefined
    };

    this.authService.registerTeacher(request).subscribe({
      next: () => {
        this.successMessage = 'Öğretmen kaydınız başarıyla oluşturuldu! Yönlendiriliyorsunuz...';
        setTimeout(() => {
          this.router.navigate(['/teacher/dashboard']);
        }, 2000);
      },
      error: (err) => {
        this.error = err.error?.message || 'Kayıt sırasında bir hata oluştu. Lütfen bilgilerinizi kontrol edin.';
        this.isLoading = false;
      }
    });
  }
}
