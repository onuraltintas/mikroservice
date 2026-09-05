import { AfterViewInit, Component, inject, OnDestroy, OnInit, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { AuthService } from '../../../core/services/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { AuthResponse } from '../../../core/models/user.model';
import {
  GoogleIdentityCallback,
  GoogleIdentityService,
  GoogleIdentityResponse
} from '../../../core/services/google-identity.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements AfterViewInit, OnDestroy, OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly toaster = inject(ToasterService);
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly googleIdentity = inject(GoogleIdentityService);
  private readonly googleCallback: GoogleIdentityCallback = (response: GoogleIdentityResponse) =>
    this.handleGoogleResponse(response);

  loginForm: FormGroup;
  loading = false;
  error = '';
  showEmailVerificationWarning = false;
  unverifiedEmail = '';
  resendingEmail = false;
  hidePassword = true;
  mfaStage: 'none' | 'setup' | 'verify' | 'recovery' = 'none';
  mfaSecret = '';
  mfaOtpAuthUri = '';
  mfaRecoveryCodes: string[] = [];
  mfaChallengeToken = '';
  mfaSetupToken = '';
  mfaCode = '';
  mfaRecoveryCode = '';
  mfaUsingRecoveryCode = false;

  constructor() {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required],
      rememberMe: [false]
    });
  }

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadRememberedEmail();
      this.checkForRegistrationMessage();
    }
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const buttonElement = document.getElementById('google-signin-button');
    if (!buttonElement) {
      console.error('Google Sign-In button element not found');
      return;
    }

    this.googleIdentity.renderButton(buttonElement, 'signin_with', this.googleCallback)
      .catch(error => {
        console.error('Error initializing Google Sign-In:', error);
        this.error = 'Google ile giriş hizmeti şu anda kullanılamıyor. Lütfen e-posta ile giriş yapın.';
      });
  }

  ngOnDestroy(): void {
    this.googleIdentity.clearCallback(this.googleCallback);
  }

  private checkForRegistrationMessage(): void {
    this.route.queryParams.subscribe(params => {
      if (params['registered'] === 'true' && params['message']) {
        this.toaster.success(params['message'], 5000);
      }
    });
  }

  private loadRememberedEmail(): void {
    const rememberedEmail = localStorage.getItem('rememberedEmail');
    if (rememberedEmail) {
      this.loginForm.patchValue({
        email: rememberedEmail,
        rememberMe: true
      });
    }
  }

  private handleGoogleResponse(response: any): void {
    this.loading = true;
    this.error = '';

    this.authService.googleAuth(response.credential).subscribe({
      next: (authResponse) => {
        if (authResponse.requiresMfa) {
          void this.beginMfa(
            authResponse.mfaChallengeToken ?? null,
            authResponse.mfaEnrollmentRequired === true);
          return;
        }
        this.handleAuthenticatedResponse(authResponse);
      },
      error: (err) => {
        if (err.error?.Message) {
          this.error = err.error.Message;
        } else if (err.error?.message) {
          this.error = err.error.message;
        } else if (err.error?.title) {
          this.error = err.error.title;
        } else if (typeof err.error === 'string') {
          this.error = err.error;
        } else {
          this.error = 'Google girişi başarısız oldu.';
        }
        this.loading = false;
      }
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    const { email, password, rememberMe } = this.loginForm.value;

    // Handle remember me
    if (rememberMe) {
      localStorage.setItem('rememberedEmail', email);
    } else {
      localStorage.removeItem('rememberedEmail');
    }

    this.authService.login({ email, password })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (response: any) => {
          if (response.requiresMfa) {
            void this.beginMfa(
              response.mfaChallengeToken ?? null,
              response.mfaEnrollmentRequired === true);
            return;
          }
          this.handleAuthenticatedResponse(response);
        },
        error: (err) => {
          // Check if error is about email verification
          const errorMessage = (err.error?.Message || err.error?.message || '').toLowerCase(); // Normalize case

          if (errorMessage && (errorMessage.includes('verify your email') || errorMessage.includes('doğrulanma') || errorMessage.includes('doğrula'))) {
            this.showEmailVerificationWarning = true;
            this.unverifiedEmail = email;
            this.error = 'E-posta adresiniz doğrulanmamış. Lütfen e-postanızdaki doğrulama linkine tıklayın.';
          } else {
            // Backend'den gelen hata mesajını göster
            if (err.error?.Message) {
              this.error = err.error.Message;
            } else if (err.error?.message) {
              this.error = err.error.message;
            } else if (err.error?.title) {
              this.error = err.error.title;
            } else if (typeof err.error === 'string') {
              this.error = err.error;
            } else {
              this.error = 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.';
            }
            this.toaster.error(this.error, 5000);
          }
        }
      });
  }

  async submitMfa(): Promise<void> {
    const code = this.mfaCode.trim();
    const recoveryCode = this.mfaRecoveryCode.trim();
    if (!this.mfaChallengeToken || this.loading) return;
    if (this.mfaStage === 'setup' && !/^\d{6}$/.test(code)) return;
    if (this.mfaStage === 'verify' && !this.mfaUsingRecoveryCode && !/^\d{6}$/.test(code)) return;
    if (this.mfaStage === 'verify' && this.mfaUsingRecoveryCode && !recoveryCode) return;

    this.loading = true;
    this.error = '';

    try {
      if (this.mfaStage === 'setup') {
        this.mfaRecoveryCodes = await this.authService.enableMfa(
          this.mfaChallengeToken,
          this.mfaSetupToken,
          code);
        this.mfaCode = '';
        this.mfaStage = 'recovery';
        this.toaster.success('İki adımlı doğrulama etkinleştirildi.', 5000);
        return;
      }

      await this.authService.verifyMfa(
        this.mfaChallengeToken,
        this.mfaUsingRecoveryCode ? null : code,
        this.mfaUsingRecoveryCode ? recoveryCode : null);
      this.resetMfaState();
      this.handleAuthenticatedResponse(this.authService.currentUserValue);
    } catch (error) {
      this.error = this.readErrorMessage(error, 'MFA doğrulama kodu geçersiz.');
      this.toaster.error(this.error, 5000);
    } finally {
      this.loading = false;
    }
  }

  finishMfaEnrollment(): void {
    this.resetMfaState();
    this.handleAuthenticatedResponse(this.authService.currentUserValue);
  }

  toggleRecoveryCode(): void {
    this.mfaUsingRecoveryCode = !this.mfaUsingRecoveryCode;
    this.mfaCode = '';
    this.mfaRecoveryCode = '';
    this.error = '';
  }

  isMfaCodeValid(): boolean {
    return /^\d{6}$/.test(this.mfaCode.trim());
  }

  private async beginMfa(challengeToken: string | null, enrollmentRequired: boolean): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      if (!challengeToken) {
        throw new Error('MFA doğrulama oturumu başlatılamadı.');
      }

      this.mfaChallengeToken = challengeToken;
      this.mfaCode = '';
      this.mfaRecoveryCode = '';
      this.mfaUsingRecoveryCode = false;

      if (enrollmentRequired) {
        const setup = await this.authService.startMfaSetup(challengeToken);
        this.mfaSecret = setup.secret;
        this.mfaOtpAuthUri = setup.otpAuthUri;
        this.mfaSetupToken = setup.setupToken;
        this.mfaChallengeToken = setup.challengeToken ?? challengeToken;
        this.mfaStage = 'setup';
      } else {
        this.mfaStage = 'verify';
      }
    } catch (error) {
      this.error = this.readErrorMessage(error, 'MFA doğrulaması başlatılamadı.');
      this.toaster.error(this.error, 5000);
    } finally {
      this.loading = false;
    }
  }

  private handleAuthenticatedResponse(response: AuthResponse | null): void {
    this.loading = false;
    const role = response?.roles?.[0];
    console.log('Login Role:', role);

    if (!role) {
      this.toaster.error('Kullanıcı rolü bulunamadı.', 5000);
      return;
    }

    const normalizedRole = role.toLowerCase();
    if (normalizedRole === 'student') {
      this.navigateStudent();
    } else if (normalizedRole === 'teacher') {
      this.router.navigate(['/teacher/dashboard']);
    } else if (normalizedRole === 'institutionadmin') {
      this.router.navigate(['/teacher/dashboard']);
    } else if (normalizedRole === 'admin' || normalizedRole === 'systemadmin') {
      this.router.navigate(['/admin/dashboard']);
    } else if (normalizedRole === 'editor') {
      this.router.navigate(['/admin/dashboard']);
    } else if (normalizedRole === 'coach') {
      this.router.navigate(['/coaching/dashboard']);
    } else {
      console.warn('Unknown role:', role);
      this.toaster.error(`Tanımlanamayan kullanıcı rolü: ${role}`, 5000);
      this.router.navigate(['/']);
    }
  }

  private resetMfaState(): void {
    this.mfaStage = 'none';
    this.mfaSecret = '';
    this.mfaOtpAuthUri = '';
    this.mfaRecoveryCodes = [];
    this.mfaChallengeToken = '';
    this.mfaSetupToken = '';
    this.mfaCode = '';
    this.mfaRecoveryCode = '';
    this.mfaUsingRecoveryCode = false;
  }

  private readErrorMessage(error: any, fallback: string): string {
    return error?.error?.Message
      || error?.error?.message
      || error?.error?.description
      || error?.message
      || fallback;
  }

  private navigateStudent(): void {
    if (!this.authService.hasCompletedProfile()) {
      this.router.navigate(['/student/profile-setup']);
      return;
    }
    // Abonelik kontrolü — SpeedReading modülü yoksa no-access sayfasına yönlendir
    this.subscriptionService.getMyModules().subscribe({
      next: (modules) => {
        if (modules.hasSpeedReading) {
          this.router.navigate(['/student/dashboard']);
        } else {
          this.router.navigate(['/no-access']);
        }
      },
      // Abonelik servisi erişilemezse doğrudan dashboard'a git
      error: () => this.router.navigate(['/student/dashboard']),
    });
  }

  resendVerificationEmail(): void {
    this.resendingEmail = true;

    this.authService.resendVerification(this.unverifiedEmail).subscribe({
      next: () => {
        this.resendingEmail = false;
        this.toaster.success('Doğrulama e-postası gönderildi! Lütfen gelen kutunuzu kontrol edin.', 5000);
      },
      error: () => {
        this.resendingEmail = false;
        this.toaster.error('E-posta gönderilemedi. Lütfen tekrar deneyin.', 3000);
      }
    });
  }
}
