import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthShellComponent } from '../auth-shell/auth-shell.component';
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
  imports: [FormsModule, RouterLink, AuthShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-shell>
      <section class="ds-auth__form" aria-labelledby="confirm-email-title">
        <div class="ds-auth__heading">
          <h1 id="confirm-email-title" class="ds-auth__title">E-posta doğrulama</h1>
        </div>

        @switch (state()) {
          @case ('verifying') {
            <p class="confirm-wait" role="status"><span class="ds-spinner" aria-hidden="true"></span>{{ copy.verifying }}</p>
          }
          @case ('verified') {
            <div class="confirm-ok" role="status" data-testid="confirm-email-message">
              <strong>{{ copy.verifiedTitle }}</strong>
              <span>{{ copy.verifiedHint }}</span>
            </div>
            <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/auth/login" data-testid="confirm-email-login">Giriş yap</a>
          }
          @case ('alreadyVerified') {
            <div class="confirm-ok" role="status" data-testid="confirm-email-message">
              <strong>{{ copy.alreadyVerifiedTitle }}</strong>
              <span>{{ copy.alreadyVerifiedHint }}</span>
            </div>
            <a class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" routerLink="/auth/login" data-testid="confirm-email-login">Giriş yap</a>
          }
          @default {
            <div class="ds-alert" role="alert" data-testid="confirm-email-message">
              <strong>{{ title() }}</strong>
              <span>{{ hint() }}</span>
            </div>

            @if (state() === 'unavailable') {
              <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="button" data-testid="confirm-email-retry" (click)="retry()">Yeniden dene</button>
            } @else {
              @if (resendStatus()) {
                <p class="confirm-ok" role="status" data-testid="confirm-email-resend-status">{{ resendStatus() }}</p>
              } @else {
                <form class="ds-auth__form" data-testid="confirm-email-resend" (ngSubmit)="resend()">
                  <div class="ds-field">
                    <label for="confirm-email-address" class="ds-field__label">{{ copy.resendEmailLabel }}</label>
                    <input id="confirm-email-address" class="ds-input" name="email" type="email" autocomplete="email" maxlength="255" required
                      [(ngModel)]="email" [attr.aria-describedby]="resendError() ? 'confirm-email-resend-error' : null"
                      [attr.aria-invalid]="resendError() ? 'true' : null">
                    @if (resendError()) {
                      <p id="confirm-email-resend-error" class="ds-field__error" role="alert">{{ resendError() }}</p>
                    }
                  </div>
                  <button class="ds-btn ds-btn--primary ds-btn--large ds-btn--block" type="submit" [disabled]="resending() || !email.trim()">
                    {{ resending() ? 'Gönderiliyor…' : 'Yeni doğrulama e-postası gönder' }}
                  </button>
                </form>
              }
            }
            <a class="ds-btn ds-btn--secondary ds-btn--block" routerLink="/auth/login" data-testid="confirm-email-login">Giriş sayfasına dön</a>
          }
        }
      </section>
    </app-auth-shell>
  `,
  styles: [`
    .confirm-wait { display: flex; align-items: center; gap: 12px; margin: 0; color: var(--ds-muted); }
    .confirm-ok { display: flex; flex-direction: column; gap: 4px; margin: 0; padding: 14px 16px; border: 1px solid currentColor;
      border-radius: var(--ds-radius-control); background: var(--ds-success-soft); color: var(--ds-success); line-height: 1.5; }
    .ds-alert strong, .confirm-ok strong { font-size: 16px; }
    .ds-field__error { margin: 0; }
  `]
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
