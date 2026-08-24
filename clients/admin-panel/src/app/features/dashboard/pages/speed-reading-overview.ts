import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingCapabilities
} from '../../../core/services/speed-reading-admin.service';

@Component({
  selector: 'app-speed-reading-overview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Hızlı Okuma</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Bağımsız hızlı okuma servisinin yönetim görünümü.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          {{ loading() ? 'Yükleniyor…' : 'Yenile' }}
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (loading() && !capabilities()) {
        <div class="rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">Servis bilgisi yükleniyor…</div>
      } @else if (capabilities(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Çalışma modu</p>
            <p class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ data.mode }}</p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Koçluk entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.coachingIntegrationEnabled" [class.text-gray-500]="!data.coachingIntegrationEnabled">
              {{ data.coachingIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Bildirim entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.notificationIntegrationEnabled" [class.text-gray-500]="!data.notificationIntegrationEnabled">
              {{ data.notificationIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
          <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Abonelik entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.subscriptionIntegrationEnabled" [class.text-gray-500]="!data.subscriptionIntegrationEnabled">
              {{ data.subscriptionIntegrationEnabled ? 'Açık' : 'Kapalı' }}
            </p>
          </div>
        </div>

        <div class="rounded-xl border border-indigo-100 bg-indigo-50 p-5 text-sm text-indigo-900 dark:border-indigo-900/50 dark:bg-indigo-950/30 dark:text-indigo-200">
          İçerik, program, ilerleme ve rapor ekranları servis sözleşmeleri tamamlandıkça bu menü altında ayrı yetki kontrolleriyle açılacaktır. Bu ekran servis modunu ve entegrasyon sınırını doğrular.
        </div>
      }
    </section>
  `
})
export class SpeedReadingOverviewComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly capabilities = signal<SpeedReadingCapabilities | null>(null);

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.service.getCapabilities().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => this.capabilities.set(value),
      error: () => this.error.set('Hızlı okuma servis bilgisi yüklenemedi.')
    });
  }
}
