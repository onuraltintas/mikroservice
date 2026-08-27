import { Component, inject, OnInit, PLATFORM_ID, Inject } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
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
import { environment } from '../../../../environments/environment';

declare const google: any;

@Component({
  selector: 'app-login',
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
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly toaster = inject(ToasterService);
  private readonly subscriptionService = inject(SubscriptionService);

  loginForm: FormGroup;
  loading = false;
  error = '';
  showEmailVerificationWarning = false;
  unverifiedEmail = '';
  resendingEmail = false;
  hidePassword = true;

  constructor() {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required],
      rememberMe: [false]
    });
  }

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadGoogleScript();
      this.loadRememberedEmail();
      this.checkForRegistrationMessage();
    }
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

  private loadGoogleScript(): void {
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => this.initializeGoogleSignIn();
    document.head.appendChild(script);
  }

  private initializeGoogleSignIn(attempt = 1): void {
    if (typeof google !== 'undefined' && google.accounts) {
      // Small delay to ensure DOM is ready
      setTimeout(() => {
        try {
          google.accounts.id.initialize({
            client_id: environment.googleClientId,
            callback: (response: any) => this.handleGoogleResponse(response),
            auto_select: false,
            cancel_on_tap_outside: true
          });

          const buttonElement = document.getElementById('google-signin-button');
          if (buttonElement) {
            // Calculate width in pixels for better compatibility
            // Default to a reasonable width if offsetWidth is 0 (hidden)
            const width = buttonElement.offsetWidth || 350;

            google.accounts.id.renderButton(buttonElement, {
              theme: 'outline',
              size: 'large',
              text: 'signin_with',
              shape: 'rectangular',
              width: width // Pass number (pixels) instead of string '100%'
            });

            // Force width style as fallback after a short delay
            setTimeout(() => {
              const iframe = buttonElement.querySelector('iframe');
              if (iframe) {
                iframe.style.width = '100%';
              }
            }, 100);
          } else {
            console.error('Google Sign-In button element not found');
          }
        } catch (error) {
          console.error('Error initializing Google Sign-In:', error);
        }
      }, 100);
    } else {
      // Retry up to 5 times
      if (attempt <= 10) {
        setTimeout(() => this.initializeGoogleSignIn(attempt + 1), 500);
      } else {
        console.error('Google Sign-In script failed to load after multiple attempts');
        this.error = 'Google ile giriş hizmeti şu anda kullanılamıyor. Lütfen daha sonra tekrar deneyin veya e-posta ile giriş yapın.';
      }
    }
  }

  private handleGoogleResponse(response: any): void {
    this.loading = true;
    this.error = '';

    this.authService.googleAuth(response.credential).subscribe({
      next: (authResponse) => {
        const role = authResponse.roles?.[0];
        console.log('Google Auth Role:', role); // Debug log

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
          const role = response.roles?.[0];
          console.log('Login Role:', role); // Debug log

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
