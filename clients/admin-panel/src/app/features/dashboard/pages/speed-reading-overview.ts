import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin, of } from 'rxjs';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { AuthService } from '../../../core/auth/auth.service';
import { SpeedReadingAdminService, SpeedReadingCapabilities } from '../../../core/services/speed-reading-admin.service';

@Component({
  selector: 'app-speed-reading-overview',
  standalone: true,
  imports: [CommonModule, MatIconModule, RouterLink],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Hızlı Okuma Servis Özeti</h1>
          <p class="text-sm text-gray-500 dark:text-gray-400">Servis durumu, entegrasyonlar ve içerik sayıları.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          {{ loading() ? 'Yükleniyor…' : 'Yenile' }}
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>
      }

      @if (loading() && !capabilities()) {
        <div class="rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">Servis bilgisi yükleniyor…</div>
      } @else if (capabilities(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Çalışma modu</p>
            <p class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{{ data.mode }}</p>
          </article>
          <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Koçluk entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.coachingIntegrationEnabled" [class.text-gray-500]="!data.coachingIntegrationEnabled">{{ data.coachingIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
          <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Bildirim entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.notificationIntegrationEnabled" [class.text-gray-500]="!data.notificationIntegrationEnabled">{{ data.notificationIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
          <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p class="text-sm text-gray-500 dark:text-gray-400">Abonelik entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-emerald-600]="data.subscriptionIntegrationEnabled" [class.text-gray-500]="!data.subscriptionIntegrationEnabled">{{ data.subscriptionIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
        </div>

        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Yönetim alanları</h2>
          <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Kayıtları görüntülemek veya değiştirmek için ilgili yönetim sayfasını açın.</p>
        </div>

        <div class="grid grid-cols-1 gap-4 lg:grid-cols-3">
          @if (canManageContent()) {
            <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-blue-100 p-2 text-blue-700 dark:bg-blue-950 dark:text-blue-300"><mat-icon>menu_book</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/catalog" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">İçeriği yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-gray-900 dark:text-white">Egzersiz ve okuma içeriği</h3>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Egzersiz türleri, egzersizler, okuma metinleri ve anlama soruları.</p>
              <dl class="mt-4 grid grid-cols-3 gap-2 text-center">
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><dt class="text-xs text-gray-500">Tür</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().exerciseTypes }}</dd></div>
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><dt class="text-xs text-gray-500">Egzersiz</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().exercises }}</dd></div>
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><dt class="text-xs text-gray-500">Metin</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().readingTexts }}</dd></div>
              </dl>
            </article>
          }

          @if (canManagePrograms()) {
            <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-violet-100 p-2 text-violet-700 dark:bg-violet-950 dark:text-violet-300"><mat-icon>account_tree</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/programs" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Programları yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-gray-900 dark:text-white">Programlar ve öğrenme yolları</h3>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Program şablonları, öğrenme yolu düğümleri, içerikler ve ön koşullar.</p>
              <dl class="mt-4 grid grid-cols-2 gap-2 text-center">
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><dt class="text-xs text-gray-500">Program</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().programs }}</dd></div>
                <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><dt class="text-xs text-gray-500">Öğrenme yolu</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().learningPaths }}</dd></div>
              </dl>
            </article>
          }

          @if (canManageGamification()) {
            <article class="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-amber-100 p-2 text-amber-700 dark:bg-amber-950 dark:text-amber-300"><mat-icon>emoji_events</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/achievements" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Kazanımları yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-gray-900 dark:text-white">Başarılar ve rozetler</h3>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Kazanım kriterleri, rozetler, seviyeler ve XP ödülleri.</p>
              <dl class="mt-4"><div class="rounded-lg bg-gray-50 p-3 text-center dark:bg-gray-900"><dt class="text-xs text-gray-500">Kazanım</dt><dd class="mt-1 text-xl font-bold dark:text-white">{{ counts().achievements }}</dd></div></dl>
            </article>
          }
        </div>

        @if (!canManageContent() && !canManagePrograms() && !canManageGamification()) {
          <div class="rounded-xl border border-gray-200 bg-white p-5 text-sm text-gray-600 shadow-sm dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300">Yönetim kartları, hesabınıza tanımlanan hızlı okuma içerik, program veya oyunlaştırma yetkilerine göre gösterilir.</div>
        }
      }
    </section>
  `
})
export class SpeedReadingOverviewComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly authService = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly capabilities = signal<SpeedReadingCapabilities | null>(null);
  readonly counts = signal({ exerciseTypes: 0, exercises: 0, readingTexts: 0, programs: 0, learningPaths: 0, achievements: 0 });
  readonly canManageContent = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingContentManage));
  readonly canManagePrograms = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingProgramManage));
  readonly canManageGamification = computed(() => this.authService.hasPermission(ADMIN_PERMISSIONS.speedReadingGamificationManage));

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      capabilities: this.service.getCapabilities(),
      exerciseTypes: this.canManageContent() ? this.service.getExerciseTypes() : of(null),
      exercises: this.canManageContent() ? this.service.getExercises() : of(null),
      readingTexts: this.canManageContent() ? this.service.getReadingTexts() : of(null),
      programs: this.canManagePrograms() ? this.service.getProgramTemplates() : of(null),
      learningPaths: this.canManagePrograms() ? this.service.getLearningPathTemplates() : of(null),
      achievements: this.canManageGamification() ? this.service.getAchievementsForAdmin() : of(null)
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        this.capabilities.set(result.capabilities);
        this.counts.set({
          exerciseTypes: result.exerciseTypes?.totalCount ?? 0,
          exercises: result.exercises?.totalCount ?? 0,
          readingTexts: result.readingTexts?.length ?? 0,
          programs: result.programs?.length ?? 0,
          learningPaths: result.learningPaths?.length ?? 0,
          achievements: result.achievements?.totalCount ?? 0
        });
      },
      error: () => this.error.set('Hızlı okuma servis özeti yüklenemedi.')
    });
  }
}
