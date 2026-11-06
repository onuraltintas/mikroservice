import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/auth/auth.service';
import { getApiErrorCode, getApiErrorMessage } from '../../../core/http/api-error-message';
import { ToasterService } from '../../../core/services/toaster.service';
import { injectAuthCopy } from '../auth-voice';

/**
 * Paket F #10: the e-mail verification link. Each outcome gets one message: verified, already verified (sign in),
 * expired or invalid (ask for a new e-mail through the resend endpoint, which keeps its rate limit and never says
 * whether an account exists), or a temporary failure (try again).
 */
export type ConfirmEmailState = 'verifying' | 'verified' | 'alreadyVerified' | 'expired' | 'invalid' | 'unavailable';

/** Reads the reason the identity API gives (`status` on success, the error code on failure). */
export function confirmEmailState(outcome: { ok: true; body: unknown } | { ok: false; error: unknown }): ConfirmEmailState {
  if (outcome.ok) {
    const status = outcome.body && typeof outcome.body === 'object' ? (outcome.body as { status?: unknown }).status : undefined;
    return status === 'alreadyConfirmed' ? 'alreadyVerified' : 'verified';
  }
  const httpStatus = (outcome.error as { status?: unknown } | null)?.status;
  if (typeof httpStatus === 'number' && (httpStatus === 0 || httpStatus === 429 || httpStatus >= 500)) return 'unavailable';
  return getApiErrorCode(outcome.error) === 'User.TokenExpired' ? 'expired' : 'invalid';
}

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen w-full flex items-center justify-center bg-gray-50 dark:bg-gray-900 p-4 relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] bg-blue-400/10 rounded-full blur-[120px]"></div>
        <div class="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] bg-purple-400/10 rounded-full blur-[120px]"></div>
      </div>

      <main class="w-full max-w-md bg-white dark:bg-gray-800 rounded-[2rem] shadow-2xl p-8 text-center relative z-10 border border-gray-100 dark:border-gray-700"
        aria-labelledby="confirm-email-title">
        <div class="mb-8 flex justify-center">
          <div class="w-20 h-20 bg-blue-100 dark:bg-blue-900/30 rounded-full flex items-center justify-center">
            <mat-icon class="!text-blue-600 dark:!text-blue-400 !text-4xl !w-10 !h-10">mark_email_read</mat-icon>
          </div>
        </div>

        <h1 id="confirm-email-title" class="text-3xl font-bold text-gray-900 dark:text-white mb-4">E-posta doğrulama</h1>

        @switch (state()) {
          @case ('verifying') {
            <div class="flex flex-col items-center gap-4 py-8" role="status">
              <mat-spinner diameter="48"></mat-spinner>
              <p class="text-gray-600 dark:text-gray-400">{{ copy.verifying }}</p>
            </div>
          }
          @case ('verified') {
            <div class="p-4 bg-green-50 dark:bg-green-900/20 rounded-xl border border-green-100 dark:border-green-800 text-green-700 dark:text-green-400 mb-8"
              role="status" data-testid="confirm-email-message">
              <p class="font-medium text-lg">{{ copy.verifiedTitle }}</p>
              <p>{{ copy.verifiedHint }}</p>
            </div>
            <a mat-flat-button color="primary" routerLink="/auth/login" data-testid="confirm-email-login"
              class="!py-6 !rounded-xl !text-lg !font-bold w-full">Giriş yap</a>
          }
          @case ('alreadyVerified') {
            <div class="p-4 bg-green-50 dark:bg-green-900/20 rounded-xl border border-green-100 dark:border-green-800 text-green-700 dark:text-green-400 mb-8"
              role="status" data-testid="confirm-email-message">
              <p class="font-medium text-lg">{{ copy.alreadyVerifiedTitle }}</p>
              <p>{{ copy.alreadyVerifiedHint }}</p>
            </div>
            <a mat-flat-button color="primary" routerLink="/auth/login" data-testid="confirm-email-login"
              class="!py-6 !rounded-xl !text-lg !font-bold w-full">Giriş yap</a>
          }
          @default {
            <div class="p-4 bg-red-50 dark:bg-red-900/20 rounded-xl border border-red-100 dark:border-red-800 text-red-700 dark:text-red-400 mb-6"
              role="alert" data-testid="confirm-email-message">
              <p class="font-medium text-lg">{{ title() }}</p>
              <p>{{ hint() }}</p>
            </div>

            @if (state() === 'unavailable') {
              <button mat-flat-button color="primary" type="button" data-testid="confirm-email-retry" (click)="retry()"
                class="!py-6 !rounded-xl !text-lg !font-bold w-full mb-4">Yeniden dene</button>
            } @else {
              @if (resendStatus()) {
                <p class="mb-6 rounded-xl bg-blue-50 p-4 text-blue-800 dark:bg-blue-900/20 dark:text-blue-200" role="status"
                  data-testid="confirm-email-resend-status">{{ resendStatus() }}</p>
              } @else {
                <form class="mb-6 flex flex-col gap-3 text-left" data-testid="confirm-email-resend" (ngSubmit)="resend()">
                  <label for="confirm-email-address" class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ copy.resendEmailLabel }}</label>
                  <input id="confirm-email-address" name="email" type="email" autocomplete="email" maxlength="255" required
                    [(ngModel)]="email" [attr.aria-describedby]="resendError() ? 'confirm-email-resend-error' : null"
                    class="w-full rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-gray-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-gray-600 dark:bg-gray-700/50 dark:text-white">
                  @if (resendError()) {
                    <p id="confirm-email-resend-error" class="text-sm text-red-600 dark:text-red-400" role="alert">{{ resendError() }}</p>
                  }
                  <button mat-flat-button color="primary" type="submit" [disabled]="resending() || !email.trim()"
                    class="!py-5 !rounded-xl !font-bold w-full">
                    {{ resending() ? 'Gönderiliyor…' : 'Yeni doğrulama e-postası gönder' }}
                  </button>
                </form>
              }
            }
            <a mat-stroked-button color="primary" routerLink="/auth/login" data-testid="confirm-email-login"
              class="!py-6 !rounded-xl !text-lg !font-bold w-full">Giriş sayfasına dön</a>
          }
        }
      </main>
    </div>
  `
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  /** Paket F #12: "sen" on the Coaching site, "siz" on the management panel (eduivme.com). */
  private readonly authCopy = injectAuthCopy();
  readonly copy = this.authCopy.copy;

  readonly state = signal<ConfirmEmailState>('verifying');
  readonly resending = signal(false);
  readonly resendStatus = signal<string | null>(null);
  readonly resendError = signal<string | null>(null);
  email = '';
  private userId = '';
  private token = '';

  ngOnInit() {
    this.userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.userId || !this.token) {
      this.state.set('invalid');
      return;
    }
    void this.verifyEmail();
  }

  title(): string {
    switch (this.state()) {
      case 'expired': return this.copy.expiredTitle;
      case 'unavailable': return this.copy.unavailableTitle;
      default: return this.copy.invalidTitle;
    }
  }

  hint(): string {
    switch (this.state()) {
      case 'expired': return this.copy.expiredHint;
      case 'unavailable': return this.copy.unavailableHint;
      default: return this.copy.invalidHint;
    }
  }

  retry() {
    this.state.set('verifying');
    void this.verifyEmail();
  }

  async verifyEmail() {
    try {
      const body = await this.authService.confirmEmail(this.userId, this.token);
      const state = confirmEmailState({ ok: true, body });
      this.state.set(state);
      if (state === 'verified') this.toaster.success(this.copy.verifiedToast);
    } catch (error) {
      // One message on the page, no second toast.
      this.state.set(confirmEmailState({ ok: false, error }));
    }
  }

  /** The existing resend endpoint: rate limited, and the answer is the same whether or not the account exists. */
  async resend() {
    const email = this.email.trim();
    if (!email || this.resending()) return;
    this.resending.set(true);
    this.resendError.set(null);
    try {
      await this.authService.resendVerificationEmail(email);
      this.resendStatus.set(this.copy.resendSent);
    } catch (error) {
      this.resendError.set(getApiErrorMessage(error, this.copy.resendFailed, { voice: this.authCopy.voice }));
    } finally {
      this.resending.set(false);
    }
  }
}
