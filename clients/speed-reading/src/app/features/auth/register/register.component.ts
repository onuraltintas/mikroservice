import { AfterViewInit, Component, inject, OnDestroy, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { AuthService } from '../../../core/services/auth.service';
import {
  GoogleIdentityCallback,
  GoogleIdentityService,
  GoogleIdentityResponse
} from '../../../core/services/google-identity.service';
import { strongPasswordValidator, PASSWORD_ERROR_MESSAGES } from '../../../shared/validators/password.validator';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatIconModule,
    MatCheckboxModule
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly googleIdentity = inject(GoogleIdentityService);
  private readonly googleCallback: GoogleIdentityCallback = (response: GoogleIdentityResponse) =>
    this.handleGoogleResponse(response);

  registerForm: FormGroup;
  loading = false;
  error = '';
  passwordErrorMessages = PASSWORD_ERROR_MESSAGES;
  hidePassword = true; // Added visibility toggle state
  hideConfirmPassword = true;

  constructor() {
    this.registerForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, strongPasswordValidator()]],
      confirmPassword: ['', [Validators.required]],
      acceptTerms: [false, Validators.requiredTrue],
      acceptKVKK: [false, Validators.requiredTrue]
    }, { validators: this.passwordMatchValidator });
  }

  passwordMatchValidator(g: FormGroup) {
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

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const buttonElement = document.getElementById('google-signup-button');
    if (!buttonElement) {
      console.error('Google Sign-Up button element not found');
      return;
    }

    this.googleIdentity.renderButton(buttonElement, 'signup_with', this.googleCallback)
      .catch(error => {
        console.error('Error initializing Google Sign-Up:', error);
        this.error = 'Google ile kayıt hizmeti şu anda kullanılamıyor.';
      });
  }

  ngOnDestroy(): void {
    this.googleIdentity.clearCallback(this.googleCallback);
  }

  private handleGoogleResponse(response: any): void {
    this.loading = true;
    this.error = '';

    this.authService.googleAuth(response.credential).subscribe({
      next: (authResponse) => {
        const role = authResponse.roles?.[0];
        if (role === 'Student') {
          this.router.navigate(['/student/dashboard']);
        } else if (role === 'Teacher') {
          this.router.navigate(['/teacher/dashboard']);
        } else if (role === 'Admin') {
          this.router.navigate(['/admin/dashboard']);
        } else {
          this.router.navigate(['/']);
        }
      },
      error: (err) => {
        // Backend'den gelen hata mesajını göster (Message veya message)
        if (err.error?.Message) {
          this.error = err.error.Message;  // C# backend büyük M kullanıyor
        } else if (err.error?.message) {
          this.error = err.error.message;
        } else if (err.error?.title) {
          this.error = err.error.title;
        } else if (typeof err.error === 'string') {
          this.error = err.error;
        } else {
          this.error = 'Google kaydı başarısız oldu.';
        }
        this.loading = false;
      }
    });
  }

  onSubmit(): void {
    if (this.registerForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.register(this.registerForm.value).subscribe({
      next: (response) => {
        // Kayıt başarılı, login sayfasına yönlendir
        this.router.navigate(['/auth/login'], {
          queryParams: {
            registered: 'true',
            message: 'Kayıt başarılı! Lütfen e-posta adresinizi onaylayın ve giriş yapın.'
          }
        });
      },
      error: (err) => {
        // Backend'den gelen hata mesajını göster (Message veya message)
        if (err.error?.Message) {
          this.error = err.error.Message;  // C# backend büyük M kullanıyor
        } else if (err.error?.message) {
          this.error = err.error.message;
        } else if (err.error?.title) {
          this.error = err.error.title;
        } else if (typeof err.error === 'string') {
          this.error = err.error;
        } else {
          this.error = 'Kayıt başarısız. Lütfen bilgilerinizi kontrol edin.';
        }
        this.loading = false;
      }
    });
  }
}
