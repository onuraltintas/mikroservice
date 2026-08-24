import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingCapabilities,
  SpeedReadingExerciseType
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
          Mevcut hızlı okuma veritabanı korunarak içerik kataloğu bu servisten okunuyor. Yazma ve program yönetimi ekranları, veri sahipliği ve geri dönüş kontrolleri tamamlandıktan sonra ayrı yetkilerle açılacaktır.
        </div>

        <div class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Egzersiz türleri</h2>
              <p class="text-sm text-gray-500 dark:text-gray-400">Mevcut hızlı okuma kataloğundan salt-okunur görünüm.</p>
            </div>
            <span class="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600 dark:bg-gray-700 dark:text-gray-300">
              {{ exerciseTypes().length }} tür
            </span>
          </div>

          @if (exerciseTypes().length) {
            <div class="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              @for (type of exerciseTypes(); track type.id) {
                <article class="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                  <div class="flex items-center gap-3">
                    <span class="h-3 w-3 rounded-full" [style.backgroundColor]="type.colorCode"></span>
                    <h3 class="font-medium text-gray-900 dark:text-white">{{ type.displayName || type.name }}</h3>
                  </div>
                  <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">{{ type.engineType || 'Genel egzersiz' }}</p>
                  <p class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300">{{ type.description }}</p>
                </article>
              }
            </div>
          } @else if (!loading()) {
            <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">Katalogda gösterilecek aktif egzersiz türü bulunamadı.</p>
          }
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
  readonly exerciseTypes = signal<SpeedReadingExerciseType[]>([]);

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      capabilities: this.service.getCapabilities(),
      exerciseTypes: this.service.getExerciseTypes()
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.capabilities.set(value.capabilities);
        this.exerciseTypes.set(value.exerciseTypes.items);
      },
      error: () => this.error.set('Hızlı okuma servis bilgisi yüklenemedi.')
    });
  }
}
