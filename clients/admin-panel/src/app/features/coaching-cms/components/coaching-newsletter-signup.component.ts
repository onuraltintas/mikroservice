import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';
import { CoachingNewsletterCaptchaService } from '../../../core/services/coaching-newsletter-captcha.service';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { IdentityService } from '../../../core/services/identity.service';

@Component({
  selector: 'app-coaching-newsletter-signup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="mx-auto max-w-3xl px-4 py-10 sm:px-6" aria-labelledby="newsletter-title">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-9">
        <p class="text-xs font-bold uppercase tracking-[.14em] text-indigo-700 dark:text-indigo-300">Eduİvme · Koçluk</p>
        <h1 id="newsletter-title" class="mt-3 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">Koçluk bülteni</h1>
        <p class="mt-3 text-sm leading-6 text-slate-600 dark:text-slate-300">Koçluk, hedef belirleme ve çalışma alışkanlıklarıyla ilgili duyuruları e-posta ile alın.</p>

        @if (loadingPolicy()) {
          <p class="mt-6 rounded-xl bg-slate-50 p-4 text-sm text-slate-600 dark:bg-slate-800 dark:text-slate-300" role="status">Ortak gizlilik ve bülten onay metinleri kontrol ediliyor…</p>
        } @else if (!policyAvailable()) {
          <p class="mt-6 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100" role="status">{{ policyLoadFailed() ? 'Ortak gizlilik ve bülten onay metinleri şu anda kontrol edilemiyor. Bülten kaydı güvenli şekilde kapalı; lütfen daha sonra yeniden deneyin.' : 'Ortak gizlilik ve Koçluk bülteni onay metinleri yayımlanmamış. Metinler yayımlanana kadar bülten kaydı kapalıdır.' }} <a routerLink="/legal/privacy" target="_blank" rel="noopener noreferrer" class="font-semibold underline">Gizlilik politikasını görüntüle</a>.</p>
        } @else {
          <form class="mt-6 space-y-4" #newsletterForm="ngForm" (ngSubmit)="submit()">
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-200">E-posta adresi
              <input class="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3 py-3 text-sm dark:border-slate-700 dark:bg-slate-950" type="email" name="email" autocomplete="email" required maxlength="320" [ngModel]="email()" (ngModelChange)="email.set($event)" />
            </label>
            <div class="newsletter-honeypot" aria-hidden="true">
              <label for="coaching-newsletter-website">Web sitesi</label>
              <input id="coaching-newsletter-website" type="text" name="website" autocomplete="off" tabindex="-1" maxlength="500" [ngModel]="honeypot()" (ngModelChange)="honeypot.set($event)" />
            </div>
            <label class="flex items-start gap-3 rounded-xl border border-slate-200 p-4 text-sm leading-6 text-slate-700 dark:border-slate-700 dark:text-slate-200">
              <input class="mt-1" type="checkbox" name="consentGiven" required [ngModel]="consentGiven()" (ngModelChange)="consentGiven.set($event)" />
              <span><a routerLink="/legal/coaching-newsletter-consent" target="_blank" rel="noopener noreferrer" class="font-semibold text-indigo-700 underline dark:text-indigo-300">{{ newsletterConsentText() }}</a> <a routerLink="/legal/privacy" target="_blank" rel="noopener noreferrer" class="font-semibold text-indigo-700 underline dark:text-indigo-300">Ortak gizlilik politikasını</a> okudum. (onay metni sürüm {{ newsletterConsentVersion() }} · gizlilik sürümü {{ privacyPolicyVersion() }})</span>
            </label>
            <button class="rounded-xl bg-indigo-700 px-5 py-3 text-sm font-semibold text-white transition hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-50" type="submit" [disabled]="newsletterForm.invalid || submitting()">{{ submitting() ? 'Gönderiliyor…' : 'Onay bağlantısı gönder' }}</button>
          </form>
        }

        @if (message()) { <p class="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm leading-6 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-100" role="status">{{ message() }}</p> }
        @if (error()) { <p class="mt-4 rounded-xl border border-red-200 bg-red-50 p-4 text-sm leading-6 text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200" role="alert">{{ error() }}</p> }
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
  readonly email = signal('');
  readonly consentGiven = signal(false);
  readonly honeypot = signal('');
  readonly loadingPolicy = signal(true);
  readonly policyAvailable = signal(false);
  readonly privacyPolicyVersion = signal<number | null>(null);
  readonly newsletterConsentVersion = signal<number | null>(null);
  readonly newsletterConsentText = signal('');
  readonly policyLoadFailed = signal(false);
  readonly submitting = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  ngOnInit(): void {
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
        this.newsletterConsentText.set(consentAvailable ? consent.content.trim() : '');
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
