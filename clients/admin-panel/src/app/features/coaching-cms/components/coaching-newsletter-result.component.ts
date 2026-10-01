import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';

type NewsletterAction = 'confirm' | 'unsubscribe';

@Component({
  selector: 'app-coaching-newsletter-result',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="mx-auto max-w-2xl px-4 py-12 sm:px-6" aria-labelledby="newsletter-result-title">
      <section class="rounded-3xl border border-slate-200 bg-white p-7 text-center shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-10">
        <p class="text-xs font-bold uppercase tracking-[.14em] text-indigo-700 dark:text-indigo-300">Eduİvme · Koçluk bülteni</p>
        <h1 id="newsletter-result-title" class="mt-3 text-2xl font-semibold text-slate-900 dark:text-white">{{ action() === 'confirm' ? 'E-posta adresini onayla' : 'Bülten aboneliğini iptal et' }}</h1>
        @if (!token()) {
          <p class="mt-4 text-sm leading-6 text-amber-800 dark:text-amber-200" role="status">Bağlantı eksik veya geçersiz. Lütfen e-postadaki tam bağlantıyı kullanın.</p>
        } @else if (message()) {
          <p class="mt-4 rounded-xl p-4 text-sm leading-6" [class.bg-emerald-50]="succeeded()" [class.text-emerald-900]="succeeded()" [class.dark:bg-emerald-950]="succeeded()" [class.dark:text-emerald-100]="succeeded()" [class.bg-amber-50]="!succeeded()" [class.text-amber-900]="!succeeded()" [class.dark:bg-amber-950]="!succeeded()" [class.dark:text-amber-100]="!succeeded()" role="status">{{ message() }}</p>
        } @else {
          <p class="mt-4 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ action() === 'confirm' ? 'Bülten aboneliğini başlatmak için e-posta adresinizi onaylayın.' : 'Koçluk bülteni e-postalarını artık almak istemiyorsanız aboneliğinizi iptal edin.' }}</p>
          <button class="mt-6 rounded-xl bg-indigo-700 px-5 py-3 text-sm font-semibold text-white transition hover:bg-indigo-800 disabled:opacity-50" type="button" (click)="submit()" [disabled]="busy()">{{ busy() ? 'İşleniyor…' : action() === 'confirm' ? 'E-posta adresimi onayla' : 'Aboneliği iptal et' }}</button>
        }
        @if (error()) { <p class="mt-4 text-sm text-red-700 dark:text-red-300" role="alert">{{ error() }}</p> }
        <a routerLink="/coaching" class="mt-6 inline-block text-sm font-semibold text-indigo-700 underline dark:text-indigo-300">Koçluk ana sayfasına dön</a>
      </section>
    </main>
  `,
  styles: [`:host { display: block; min-height: 100%; }`]
})
export class CoachingNewsletterResultComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly coaching = inject(CoachingManagementService);
  private readonly destroyRef = inject(DestroyRef);
  readonly action = signal<NewsletterAction>('confirm');
  readonly token = signal('');
  readonly message = signal('');
  readonly succeeded = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.action.set(data['action'] === 'unsubscribe' ? 'unsubscribe' : 'confirm');
    });
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.token.set(params.get('token') ?? '');
    });
  }

  submit(): void {
    const token = this.token();
    if (!token || this.busy() || this.message()) return;

    this.busy.set(true);
    this.error.set('');
    const request = this.action() === 'confirm'
      ? this.coaching.confirmCoachingNewsletter(token)
      : this.coaching.unsubscribeFromCoachingNewsletter(token);
    request.subscribe({
      next: result => {
        this.message.set(result.message);
        this.succeeded.set(result.success);
        this.busy.set(false);
      },
      error: () => {
        this.error.set('İşlem tamamlanamadı. Bağlantıyı kontrol edip yeniden deneyin.');
        this.busy.set(false);
      }
    });
  }
}
