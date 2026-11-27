import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';
import { CoachingNewsletterCaptchaService } from '../../../core/services/coaching-newsletter-captcha.service';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { IdentityService } from '../../../core/services/identity.service';
import { SiteSeoService } from '../../../core/services/site-seo.service';

@Component({
  selector: 'app-coaching-newsletter-signup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="mx-auto max-w-3xl px-4 py-10 sm:px-6" aria-labelledby="newsletter-title">
      <section class="rounded-3xl border border-[var(--ds-line)] bg-[var(--ds-surface)] p-6 shadow-sm sm:p-9">
        <p class="text-sm font-semibold text-[var(--ds-link)]">maxkoç bülteni</p>
        <h1 id="newsletter-title" class="mt-3 text-3xl font-semibold tracking-tight text-[var(--ds-text)]">Koçluk bülteni</h1>
        <p class="mt-3 text-sm leading-6 text-[var(--ds-muted)]">Koçluk, hedef belirleme ve çalışma alışkanlıklarıyla ilgili duyuruları e-posta ile alın.</p>

        @if (loadingPolicy()) {
          <p class="mt-6 rounded-xl bg-[var(--ds-bg)] p-4 text-sm text-[var(--ds-muted)]" role="status">Ortak gizlilik ve bülten onay metinleri kontrol ediliyor…</p>
        } @else if (!policyAvailable()) {
          <p class="mt-6 rounded-xl border border-(--ui-warning-soft) bg-(--ui-warning-soft) p-4 text-sm leading-6 text-(--ui-warning)" role="status">{{ policyLoadFailed() ? 'Ortak gizlilik ve bülten onay metinleri şu anda kontrol edilemiyor. Bülten kaydı güvenli şekilde kapalı; lütfen daha sonra yeniden deneyin.' : 'Ortak gizlilik ve Koçluk bülteni onay metinleri yayımlanmamış. Metinler yayımlanana kadar bülten kaydı kapalıdır.' }} <a routerLink="/legal/privacy" target="_blank" rel="noopener noreferrer" class="font-semibold underline">Gizlilik politikasını görüntüle</a>.</p>
        } @else {
          <form class="mt-6 space-y-4" #newsletterForm="ngForm" (ngSubmit)="submit()">
            <label class="block text-sm font-medium text-[var(--ds-text)]">E-posta adresi
              <input class="mt-1 block w-full rounded-xl border border-[var(--ds-field)] bg-[var(--ds-surface)] px-3 py-3 text-sm" type="email" name="email" autocomplete="email" required maxlength="320" [ngModel]="email()" (ngModelChange)="email.set($event)" />
            </label>
            <div class="newsletter-honeypot" aria-hidden="true">
              <label for="coaching-newsletter-website">Web sitesi</label>
              <input id="coaching-newsletter-website" type="text" name="website" autocomplete="off" tabindex="-1" maxlength="500" [ngModel]="honeypot()" (ngModelChange)="honeypot.set($event)" />
            </div>
            <label class="flex items-start gap-3 rounded-xl border border-[var(--ds-line)] p-4 text-sm leading-6 text-[var(--ds-text)]">
              <input class="mt-1" type="checkbox" name="consentGiven" required [ngModel]="consentGiven()" (ngModelChange)="consentGiven.set($event)" />
              <span><a routerLink="/legal/coaching-newsletter-consent" target="_blank" rel="noopener noreferrer" class="font-semibold text-[var(--ds-link)] underline">Bülten onay metni</a> kapsamında Koçluk bültenini e-posta ile almayı kabul ediyorum. <a routerLink="/legal/privacy" target="_blank" rel="noopener noreferrer" class="font-semibold text-[var(--ds-link)] underline">Ortak gizlilik politikasını</a> okudum.</span>
            </label>
            <button class="rounded-xl bg-[var(--ds-primary)] px-5 py-3 text-sm font-semibold text-[var(--ds-on-primary)] transition hover:bg-[var(--ds-primary-hover)] disabled:cursor-not-allowed disabled:opacity-50" type="submit" [disabled]="newsletterForm.invalid || submitting()">{{ submitting() ? 'Gönderiliyor…' : 'Onay bağlantısı gönder' }}</button>
          </form>
        }

        @if (message()) { <p class="mt-4 rounded-xl border border-(--ui-success-soft) bg-(--ui-success-soft) p-4 text-sm leading-6 text-(--ui-success)" role="status">{{ message() }}</p> }
        @if (error()) { <p class="mt-4 rounded-xl border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm leading-6 text-(--ui-danger)" role="alert">{{ error() }}</p> }
      </section>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100%; }
    .newsletter-honeypot { position: absolute; left: -10000px; width: 1px; height: 1px; overflow: hidden; }
  `]
})
export class CoachingNewsletterSignupComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  private readonly coaching = inject(CoachingManagementService);
  private readonly captcha = inject(CoachingNewsletterCaptchaService);
  private readonly seo = inject(SiteSeoService);
  readonly email = signal('');
  readonly consentGiven = signal(false);
  readonly honeypot = signal('');
  readonly loadingPolicy = signal(true);
  readonly policyAvailable = signal(false);
  readonly privacyPolicyVersion = signal<number | null>(null);
  readonly newsletterConsentVersion = signal<number | null>(null);
  readonly policyLoadFailed = signal(false);
  readonly submitting = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  ngOnInit(): void {
    this.seo.apply({
      title: 'Koçluk bülteni | maxkoç',
      description: 'Koçluk, hedef belirleme ve çalışma alışkanlıklarıyla ilgili maxkoç duyurularını e-posta ile alın.',
      path: '/coaching/newsletter'
    });
    forkJoin({
      privacy: this.identity.getPublicLegalPage('privacy'),
      consent: this.identity.getPublicLegalPage('coaching-newsletter-consent')
    }).subscribe({
      next: ({ privacy, consent }) => {
        const privacyAvailable = privacy.isPublished && !!privacy.content.trim() && privacy.version > 0;
        const consentAvailable = consent.isPublished && !!consent.content.trim() && consent.version > 0;
        this.policyAvailable.set(privacyAvailable && consentAvailable);
        this.privacyPolicyVersion.set(privacyAvailable ? privacy.version : null);
        this.newsletterConsentVersion.set(consentAvailable ? consent.version : null);
        this.loadingPolicy.set(false);
      },
      error: response => {
        this.policyLoadFailed.set(response.status !== 404);
        this.loadingPolicy.set(false);
      }
    });
  }

  async submit(): Promise<void> {
    const email = this.email().trim();
    const privacyPolicyVersion = this.privacyPolicyVersion();
    const newsletterConsentVersion = this.newsletterConsentVersion();
    if (!this.policyAvailable() || !privacyPolicyVersion || !newsletterConsentVersion || !email || !this.consentGiven() || this.submitting()) return;

    this.submitting.set(true);
    this.message.set('');
    this.error.set('');
    const honeypot = this.honeypot();
    try {
      const recaptchaToken = honeypot ? undefined : await this.captcha.createToken();
      const response = await firstValueFrom(this.coaching.subscribeToCoachingNewsletter({
        email,
        consentGiven: true,
        privacyPolicyVersion,
        newsletterConsentVersion,
        honeypot,
        recaptchaToken
      }));
      this.message.set(response.message || 'Adres kayıtlıysa onay bağlantısı e-posta ile gönderilecektir.');
    } catch {
      this.error.set('Güvenlik doğrulaması veya bülten isteği tamamlanamadı. Lütfen sayfayı yenileyip tekrar deneyin.');
    } finally {
      this.submitting.set(false);
    }
  }
}
