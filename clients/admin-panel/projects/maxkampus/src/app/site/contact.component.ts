import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StaffAuthRecaptchaService } from '../../../../staff-portal/src/app/auth/staff-auth-recaptcha.service';
import { MaxkampusApi, SiteEntry } from '../core/api';
import { fieldErrors, problemText } from '../core/problem';
import { Seo } from '../core/seo';

/** /iletisim: the contact form; messages reach the admin panel (maxkampüs › İletişim mesajları). */
@Component({
  selector: 'mk-contact',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <section class="mk-section"><div class="mk-wrap grid">
      <div>
        <h1>{{ page()?.title || 'İletişim' }}</h1>
        <p class="mk-lead">{{ page()?.subtitle || 'Kurumunuza uygun planı birlikte bulalım ya da sorunuzu yanıtlayalım.' }}</p>
        <p>{{ page()?.body || 'Mesajınıza bir iş günü içinde dönüyoruz. Hesabınız varsa sorunuzu panelden de gönderebilirsiniz.' }}</p>
      </div>
      <div class="mk-card">
        @if (sent()) {
          <div class="mk-success" role="status"><strong>Mesajınız bize ulaştı.</strong> Bir iş günü içinde e-postayla dönüyoruz.</div>
        } @else {
          <form class="form" (ngSubmit)="send()" novalidate>
            <label class="ds-field"><span class="ds-field__label">Adınız soyadınız</span>
              <input class="ds-input" name="name" [(ngModel)]="name" autocomplete="name" maxlength="120" required [attr.aria-invalid]="!!errors()['name']"></label>
            @if (errors()['name']) { <p class="ds-field__error">{{ errors()['name'] }}</p> }
            <label class="ds-field"><span class="ds-field__label">Kurumunuz (isteğe bağlı)</span>
              <input class="ds-input" name="organization" [(ngModel)]="organization" autocomplete="organization" maxlength="200"></label>
            <div class="ds-form-row">
              <label class="ds-field"><span class="ds-field__label">E-posta</span>
                <input class="ds-input" type="email" name="email" [(ngModel)]="email" autocomplete="email" required [attr.aria-invalid]="!!errors()['email']"></label>
              <label class="ds-field"><span class="ds-field__label">Telefon (isteğe bağlı)</span>
                <input class="ds-input" type="tel" name="phone" [(ngModel)]="phone" autocomplete="tel" maxlength="40"></label>
            </div>
            @if (errors()['email']) { <p class="ds-field__error">{{ errors()['email'] }}</p> }
            <label class="ds-field"><span class="ds-field__label">Mesajınız</span>
              <textarea class="ds-input textarea" name="message" [(ngModel)]="message" rows="6" maxlength="4000" required [attr.aria-invalid]="!!errors()['message']"></textarea></label>
            @if (errors()['message']) { <p class="ds-field__error">{{ errors()['message'] }}</p> }
            <label class="ds-check consent"><input type="checkbox" name="consent" [(ngModel)]="consent">
              <span><a routerLink="/yasal/kvkk" target="_blank" rel="noopener">KVKK aydınlatma metnini</a> okudum; mesajıma yanıt verilmesi için bilgilerimin kullanılmasını kabul ediyorum.</span></label>
            @if (errors()['consent']) { <p class="ds-field__error">{{ errors()['consent'] }}</p> }
            @if (error()) { <p class="ds-alert" role="alert">{{ error() }}</p> }
            <button type="submit" class="ds-btn ds-btn--primary ds-btn--large" [disabled]="busy()">{{ busy() ? 'Gönderiliyor' : 'Mesajı gönder' }}</button>
            <p class="mk-muted small">Bu form robot doğrulamasıyla korunur.</p>
          </form>
        }
      </div>
    </div></section>
  `,
  styles: `
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 40px; align-items: start; }
    h1 { margin: 0 0 12px; font-size: clamp(30px, 4vw, 40px); }
    .form { display: flex; flex-direction: column; gap: 14px; }
    .textarea { padding-top: 12px; min-height: 140px; resize: vertical; font: inherit; }
    .consent { align-items: flex-start; font-size: 14px; line-height: 1.5; } .consent input { margin-top: 2px; }
    .small { margin: 0; font-size: 13px; }
  `
})
export class ContactComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly recaptcha = inject(StaffAuthRecaptchaService);
  private readonly seo = inject(Seo);
  readonly page = signal<SiteEntry | null>(null);
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');
  readonly errors = signal<Record<string, string>>({});
  name = ''; organization = ''; email = ''; phone = ''; message = ''; consent = false;

  async ngOnInit(): Promise<void> {
    this.seo.set({ title: 'İletişim', path: '/iletisim' });
    const page = await this.api.page('iletisim').catch(() => null);
    this.page.set(page);
    if (page) this.seo.set({ title: page.seoTitle || 'İletişim', description: page.seoDescription, path: '/iletisim' });
  }

  async send(): Promise<void> {
    this.error.set('');
    const errors: Record<string, string> = {};
    if (!this.name.trim()) errors['name'] = 'Adınızı yazın.';
    if (!this.email.includes('@')) errors['email'] = 'Geçerli bir e-posta adresi yazın.';
    if (this.message.trim().length < 10) errors['message'] = 'Mesajınızı yazın (en az 10 karakter).';
    if (!this.consent) errors['consent'] = 'Aydınlatma metnini okuduğunuzu onaylayın.';
    this.errors.set(errors);
    if (Object.keys(errors).length) return;
    this.busy.set(true);
    try {
      const captchaToken = await this.recaptcha.createToken('site_contact').catch(() => null);
      await this.api.contact({ name: this.name.trim(), organization: this.organization.trim() || null, email: this.email.trim(),
        phone: this.phone.trim() || null, message: this.message.trim(), consent: true, captchaToken });
      this.sent.set(true);
    } catch (error) {
      this.errors.set(fieldErrors(error));
      this.error.set(problemText(error, 'Mesaj gönderilemedi. Biraz sonra yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }
}
