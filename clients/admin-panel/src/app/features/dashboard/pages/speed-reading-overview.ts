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
          <h1 class="text-2xl font-bold text-(--ui-text)">Hızlı Okuma Servis Özeti</h1>
          <p class="text-sm text-(--ui-text-muted)">Servis durumu, entegrasyonlar ve içerik sayıları.</p>
        </div>
        <button type="button" (click)="load()" [disabled]="loading()"
          class="rounded-lg bg-(--ui-brand) px-4 py-2 text-sm font-medium text-(--ui-brand-contrast) disabled:opacity-50">
          {{ loading() ? 'Yükleniyor…' : 'Yenile' }}
        </button>
      </div>

      @if (error()) {
        <div class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div>
      }

      @if (loading() && !capabilities()) {
        <div class="rounded-xl bg-(--ui-surface) p-6 shadow-sm">Servis bilgisi yükleniyor…</div>
      } @else if (capabilities(); as data) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
            <p class="text-sm text-(--ui-text-muted)">Çalışma modu</p>
            <p class="mt-2 text-2xl font-bold text-(--ui-text)">{{ data.mode }}</p>
          </article>
          <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
            <p class="text-sm text-(--ui-text-muted)">Koçluk entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-(--ui-success)]="data.coachingIntegrationEnabled" [class.text-(--ui-text-muted)]="!data.coachingIntegrationEnabled">{{ data.coachingIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
          <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
            <p class="text-sm text-(--ui-text-muted)">Bildirim entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-(--ui-success)]="data.notificationIntegrationEnabled" [class.text-(--ui-text-muted)]="!data.notificationIntegrationEnabled">{{ data.notificationIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
          <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
            <p class="text-sm text-(--ui-text-muted)">Abonelik entegrasyonu</p>
            <p class="mt-2 text-2xl font-bold" [class.text-(--ui-success)]="data.subscriptionIntegrationEnabled" [class.text-(--ui-text-muted)]="!data.subscriptionIntegrationEnabled">{{ data.subscriptionIntegrationEnabled ? 'Açık' : 'Kapalı' }}</p>
          </article>
        </div>

        <div>
          <h2 class="text-lg font-semibold text-(--ui-text)">Yönetim alanları</h2>
          <p class="mt-1 text-sm text-(--ui-text-muted)">Kayıtları görüntülemek veya değiştirmek için ilgili yönetim sayfasını açın.</p>
        </div>

        <div class="grid grid-cols-1 gap-4 lg:grid-cols-3">
          @if (canManageContent()) {
            <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-(--ui-brand-soft) p-2 text-(--ui-brand-text)"><mat-icon>menu_book</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/catalog" class="rounded-lg bg-(--ui-brand) px-3 py-2 text-sm font-medium text-(--ui-brand-contrast)">İçeriği yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-(--ui-text)">Egzersiz ve okuma içeriği</h3>
              <p class="mt-1 text-sm text-(--ui-text-muted)">Egzersiz türleri, egzersizler, okuma metinleri ve anlama soruları.</p>
              <dl class="mt-4 grid grid-cols-3 gap-2 text-center">
                <div class="rounded-lg bg-(--ui-surface-muted) p-3"><dt class="text-xs text-(--ui-text-muted)">Tür</dt><dd class="mt-1 text-xl font-bold">{{ counts().exerciseTypes }}</dd></div>
                <div class="rounded-lg bg-(--ui-surface-muted) p-3"><dt class="text-xs text-(--ui-text-muted)">Egzersiz</dt><dd class="mt-1 text-xl font-bold">{{ counts().exercises }}</dd></div>
                <div class="rounded-lg bg-(--ui-surface-muted) p-3"><dt class="text-xs text-(--ui-text-muted)">Metin</dt><dd class="mt-1 text-xl font-bold">{{ counts().readingTexts }}</dd></div>
              </dl>
            </article>
          }

          @if (canManagePrograms()) {
            <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-(--ui-brand-soft) p-2 text-(--ui-brand-text)"><mat-icon>account_tree</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/programs" class="rounded-lg bg-(--ui-brand) px-3 py-2 text-sm font-medium text-(--ui-brand-contrast)">Programları yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-(--ui-text)">Programlar ve öğrenme yolları</h3>
              <p class="mt-1 text-sm text-(--ui-text-muted)">Program şablonları, öğrenme yolu düğümleri, içerikler ve ön koşullar.</p>
              <dl class="mt-4 grid grid-cols-2 gap-2 text-center">
                <div class="rounded-lg bg-(--ui-surface-muted) p-3"><dt class="text-xs text-(--ui-text-muted)">Program</dt><dd class="mt-1 text-xl font-bold">{{ counts().programs }}</dd></div>
                <div class="rounded-lg bg-(--ui-surface-muted) p-3"><dt class="text-xs text-(--ui-text-muted)">Öğrenme yolu</dt><dd class="mt-1 text-xl font-bold">{{ counts().learningPaths }}</dd></div>
              </dl>
            </article>
          }

          @if (canManageGamification()) {
            <article class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 shadow-sm">
              <div class="flex items-start justify-between gap-3">
                <div class="rounded-lg bg-(--ui-warning-soft) p-2 text-(--ui-warning)"><mat-icon>emoji_events</mat-icon></div>
                <a routerLink="/dashboard/speed-reading/achievements" class="rounded-lg bg-(--ui-brand) px-3 py-2 text-sm font-medium text-(--ui-brand-contrast)">Kazanımları yönet</a>
              </div>
              <h3 class="mt-4 font-semibold text-(--ui-text)">Başarılar ve rozetler</h3>
              <p class="mt-1 text-sm text-(--ui-text-muted)">Kazanım kriterleri, rozetler, seviyeler ve XP ödülleri.</p>
              <dl class="mt-4"><div class="rounded-lg bg-(--ui-surface-muted) p-3 text-center"><dt class="text-xs text-(--ui-text-muted)">Kazanım</dt><dd class="mt-1 text-xl font-bold">{{ counts().achievements }}</dd></div></dl>
            </article>
          }
        </div>

        @if (!canManageContent() && !canManagePrograms() && !canManageGamification()) {
          <div class="rounded-xl border border-(--ui-border) bg-(--ui-surface) p-5 text-sm text-(--ui-text-muted) shadow-sm">Yönetim kartları, hesabınıza tanımlanan hızlı okuma içerik, program veya oyunlaştırma yetkilerine göre gösterilir.</div>
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
