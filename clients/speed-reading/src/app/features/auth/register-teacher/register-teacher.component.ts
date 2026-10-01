import { AfterViewInit, Component, inject, OnDestroy, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
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
import { getErrorMessage } from '../../../core/utils/error-message';
import { resolveInvitationReturnUrl } from '../auth-role-routing';
import { RegistrationLegalAcceptance } from '../../../core/models/user.model';
import { RegistrationLegalConsentComponent } from '../registration-legal-consent.component';

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
    MatIconModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    RegistrationLegalConsentComponent
  ],
  templateUrl: './register-teacher.component.html',
  styleUrl: './register-teacher.component.scss'
})
export class RegisterTeacherComponent implements AfterViewInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private platformId = inject(PLATFORM_ID);
  private toaster = inject(ToasterService);
  private googleIdentity = inject(GoogleIdentityService);
  private readonly googleCallback: GoogleIdentityCallback = (response: GoogleIdentityResponse) =>
    this.handleGoogleResponse(response);

  isLoading = false;
  error = '';
  successMessage = '';
  hidePassword = true;
  hideConfirmPassword = true;
  legalReady = false;
  legalAcceptances: RegistrationLegalAcceptance[] = [];

  registerForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPasswordValidator()]],
    confirmPassword: ['', [Validators.required]]
  }, {
    validators: this.passwordMatchValidator
  });

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
    if (this.isLoading) return;
    if (!this.hasRequiredLegalAcceptances) {
      this.error = 'Önce güncel yasal metinleri inceleyip onaylayın, ardından Google ile kayıt düğmesine tekrar basın.';
      return;
    }
    this.isLoading = true;
    this.error = '';
    this.successMessage = '';

    // Pass 'Teacher' role to enforce correct registration/login context
    this.authService.googleAuth(response.credential, 'Teacher', this.legalAcceptances).subscribe({
      next: (authResponse) => {
        this.successMessage = 'Google ile giriş başarılı! Yönlendiriliyorsunuz...';
        setTimeout(() => {
          const invitationReturnUrl = resolveInvitationReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
          if (invitationReturnUrl) {
            this.router.navigateByUrl(invitationReturnUrl);
          } else {
            this.router.navigate(['/teacher/dashboard']);
          }
        }, 1500);
      },
      error: (err) => {
        this.error = getErrorMessage(err, 'Google girişi başarısız oldu.');
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
    if (this.registerForm.invalid || !this.hasRequiredLegalAcceptances) return;

    this.isLoading = true;
    this.error = '';

    const formValue = this.registerForm.value;
    const request: RegisterTeacherRequest = {
      firstName: formValue.firstName!,
      lastName: formValue.lastName!,
      email: formValue.email!,
      password: formValue.password!,
      legalAcceptances: this.legalAcceptances
    };

    this.authService.registerTeacher(request).subscribe({
      next: () => {
        this.successMessage = 'Öğretmen kaydınız oluşturuldu. E-posta adresinizi doğruladıktan sonra giriş yapabilirsiniz.';
        setTimeout(() => {
          this.router.navigate(['/auth/login'], {
            queryParams: {
              registered: 'true',
              message: 'Kayıt oluşturuldu. Lütfen e-posta adresinizi onaylayıp giriş yapın.',
              ...(resolveInvitationReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))
                ? { returnUrl: resolveInvitationReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')) }
                : {})
            }
          });
        }, 2000);
      },
      error: (err) => {
        this.error = getErrorMessage(err, 'Kayıt sırasında bir hata oluştu. Lütfen bilgilerinizi kontrol edin.');
        this.isLoading = false;
      }
    });
  }

  get hasRequiredLegalAcceptances(): boolean {
    return this.legalReady && this.legalAcceptances.length === 3;
  }
}
