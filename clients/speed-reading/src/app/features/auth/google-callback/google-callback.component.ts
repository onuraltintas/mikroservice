import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { AuthResponse } from '../../../core/models/user.model';

/**
 * Handles the server-side Google OAuth redirect callback.
 * Backend redirects here with token/user info as query params after successful OAuth.
 * Route: /auth/google-callback
 */
@Component({
  selector: 'app-google-callback',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  template: `
    <div class="ui-page-center">
      @if (error) {
        <p class="ui-error-text">{{ error }}</p>
        <a href="/auth/login">Giriş sayfasına dön</a>
      } @else {
        <mat-spinner diameter="48"></mat-spinner>
        <p>Giriş yapılıyor...</p>
      }
    </div>
  `
})
export class GoogleCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly subscriptionService = inject(SubscriptionService);

  error = '';

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['error']) {
        this.error = this.mapError(params['error']);
        return;
      }

      const token        = params['token'];
      const refreshToken = params['refreshToken'];
      const email        = params['email'];
      const firstName    = params['firstName'] ?? '';
      const lastName     = params['lastName']  ?? '';
      const rolesRaw     = params['roles']     ?? '';
      const roles        = rolesRaw ? rolesRaw.split(',') : [];

      if (!token || !email) {
        this.error = 'Geçersiz giriş yanıtı.';
        return;
      }

      const authResponse: AuthResponse = {
        id: '',
        token,
        refreshToken,
        email,
        firstName,
        lastName,
        roles
      };

      this.authService.loginFromCallback(authResponse);
      this.navigateByRole(roles[0]?.toLowerCase() ?? '');
    });
  }

  private navigateByRole(role: string): void {
    if (role === 'student') {
      this.subscriptionService.getMyModules().subscribe({
        next: (m) => this.router.navigate(
          m.hasSpeedReading ? ['/student/dashboard'] : ['/no-access']
        ),
        error: () => this.router.navigate(['/student/dashboard'])
      });
    } else if (role === 'teacher' || role === 'institutionadmin') {
      this.router.navigate(['/teacher/dashboard']);
    } else if (role === 'admin' || role === 'editor') {
      this.router.navigate(['/admin/dashboard']);
    } else if (role === 'coach') {
      this.router.navigate(['/coaching/dashboard']);
    } else {
      this.router.navigate(['/auth/login']);
    }
  }

  private mapError(code: string): string {
    const map: Record<string, string> = {
      google_auth_failed: 'Google girişi başarısız oldu.',
      email_not_provided: 'Google hesabınızda e-posta adresi bulunamadı.',
      registration_failed: 'Hesap oluşturulamadı. Lütfen tekrar deneyin.',
      account_suspended: 'Hesabınız askıya alınmıştır.'
    };
    return map[code] ?? 'Bir hata oluştu. Lütfen tekrar deneyin.';
  }
}
