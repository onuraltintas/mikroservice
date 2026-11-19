import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StaffAuthRecaptchaService } from '../../../../staff-portal/src/app/auth/staff-auth-recaptcha.service';
import { MaxkampusApi } from '../core/api';
import { problemText } from '../core/problem';

/** Newsletter sign-up (double opt-in): the address counts only after the link in the confirmation e-mail. */
@Component({
  selector: 'mk-newsletter-form',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (sent()) {
      <p class="mk-success" role="status">Onay e-postası gönderildi. E-postadaki bağlantıya tıklayınca abonelik başlar.</p>
    } @else {
      <form class="nl" (ngSubmit)="submit()" [attr.aria-labelledby]="headingId()">
        <div class="nl__row">
          <label class="sr-only" [for]="inputId()">E-posta adresiniz</label>
          <input class="ds-input" [id]="inputId()" type="email" name="email" [(ngModel)]="email" autocomplete="email" placeholder="E-posta adresiniz" required>
          <button class="ds-btn ds-btn--primary" type="submit" [disabled]="busy()">{{ busy() ? 'Gönderiliyor' : 'Abone ol' }}</button>
        </div>
        <label class="ds-check nl__consent"><input type="checkbox" name="consent" [(ngModel)]="consent">
          <span>maxkampüs'ten yenilik ve yazı e-postaları almayı kabul ediyorum. İstediğim zaman ayrılabilirim.</span></label>
        @if (error()) { <p class="ds-field__error" role="alert">{{ error() }}</p> }
      </form>
    }
  `,
  styles: `
    .nl { display: flex; flex-direction: column; gap: 8px; }
    .nl__row { display: flex; flex-wrap: wrap; gap: 8px; }
    .nl__row .ds-input { flex: 1 1 220px; }
    .nl__consent { align-items: flex-start; font-size: 14px; line-height: 1.5; }
    .nl__consent input { margin-top: 2px; }
  `
})
export class NewsletterFormComponent {
  private readonly api = inject(MaxkampusApi);
  private readonly recaptcha = inject(StaffAuthRecaptchaService);
  readonly source = input('site');
  readonly headingId = input<string | null>(null);
  readonly inputId = input('newsletter-email');
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');
  email = '';
  consent = false;

  async submit(): Promise<void> {
    this.error.set('');
    if (!this.email.includes('@')) { this.error.set('Geçerli bir e-posta adresi yazın.'); return; }
    if (!this.consent) { this.error.set('E-posta almayı kabul ettiğinizi işaretleyin.'); return; }
    this.busy.set(true);
    try {
      const captchaToken = await this.recaptcha.createToken('newsletter_subscribe').catch(() => null);
      await this.api.subscribe({ email: this.email.trim(), consent: true, captchaToken, source: this.source() });
      this.sent.set(true);
    } catch (error) {
      this.error.set(problemText(error, 'Abonelik başlatılamadı. Biraz sonra yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }
}
