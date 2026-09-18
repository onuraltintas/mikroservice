import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { AuthResponse } from '../../../core/models/user.model';
import { environment } from '../../../../environments/environment';
import { resolveAuthDestination } from '../auth-role-routing';

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

      this.authService.loginFromCallback(authResponse).subscribe({
        next: hydratedResponse => this.navigateByRole(hydratedResponse.roles),
        error: () => {
          this.error = 'Giriş tamamlanamadı. Lütfen tekrar deneyin.';
        }
      });
    });
  }

  private navigateByRole(roles: readonly string[]): void {
    const destination = resolveAuthDestination(roles);
    if (destination === 'student') {
      // Let the dashboard guard route an unassessed student to the free
      // assessment before checking paid module access.
      this.router.navigate(['/student/dashboard']);
    } else if (destination === 'teacher' || destination === 'institution') {
      this.router.navigate(['/teacher/dashboard']);
    } else if (destination === 'admin') {
      void this.redirectToCentralAdmin();
    } else if (destination === 'coach') {
      this.router.navigate(['/coaching/dashboard']);
    } else {
      this.router.navigate(['/auth/login']);
    }
  }

  private async redirectToCentralAdmin(): Promise<void> {
    await this.authService.handoffToCentralAdmin();
    if (typeof window !== 'undefined') {
      window.location.replace(environment.centralAdminLoginUrl);
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
