import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin, of } from 'rxjs';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { AuthService } from '../../../core/auth/auth.service';
import { SpeedReadingAdminService, SpeedReadingCapabilities } from '../../../core/services/speed-reading-admin.service';
import { AdminPart, AdminRingComponent, AdminStatComponent } from '../../../shared/components/admin-charts';

@Component({
  selector: 'app-speed-reading-overview',
  standalone: true,
  imports: [CommonModule, MatIconModule, RouterLink, AdminStatComponent, AdminRingComponent],
  template: `
    <section class="sr">
      <header class="sr__head">
        <div>
          <h1>Hızlı Okuma özeti</h1>
          <p>Servis durumu, bağlantılar ve içerik sayıları.</p>
        </div>
        <button type="button" class="sr__refresh" (click)="load()" [disabled]="loading()">{{ loading() ? 'Yükleniyor…' : 'Yenile' }}</button>
      </header>

      @if (error()) { <div class="sr__alert" role="alert">{{ error() }} Bağlantıyı kontrol edip "Yenile"ye basın.</div> }

      @if (loading() && !capabilities()) {
        <p class="muted" role="status">Servis bilgisi yükleniyor…</p>
      } @else if (capabilities(); as data) {
        <div class="sr__status">
          <article class="sr__mode">
            <span class="sr__label">Çalışma biçimi</span>
            <strong>{{ modeLabel(data.mode) }}</strong>
          </article>
          <article class="sr__links" aria-label="Doğrudan servis bağlantıları">
            <span class="sr__label">Doğrudan servis bağlantıları</span>
            <ul>
              <li [class.on]="data.coachingIntegrationEnabled"><i aria-hidden="true"></i>Koçluk<span>{{ data.coachingIntegrationEnabled ? 'Açık' : 'Kapalı' }}</span></li>
              <li [class.on]="data.notificationIntegrationEnabled"><i aria-hidden="true"></i>Bildirimler<span>{{ data.notificationIntegrationEnabled ? 'Açık' : 'Kapalı' }}</span></li>
              <li [class.on]="data.subscriptionIntegrationEnabled"><i aria-hidden="true"></i>Abonelik<span>{{ data.subscriptionIntegrationEnabled ? 'Açık' : 'Kapalı' }}</span></li>
            </ul>
            <!-- Live test 22.11.2026: "Kapalı" read as "not working". These are the old direct links; billing and notifications run
                 through the central services in the independent mode. -->
            <small class="sr__note">Bağımsız çalışmada abonelik ve bildirimler merkezi servislerden yürür; bu ayarlar eski doğrudan bağlantılar içindir.</small>
          </article>
          @if (canManageContent()) {
            <article class="sr__ring"><admin-ring title="İçerik" unit="kayıt" [parts]="contentParts()" /></article>
          }
        </div>

        <h2 class="sr__title">Yönetim alanları</h2>
        <div class="sr__areas">
          @if (canManageContent()) {
            <article class="sr__area admin-tone-success">
              <div class="sr__icon"><mat-icon aria-hidden="true">menu_book</mat-icon></div>
              <h3>Egzersiz ve okuma içeriği</h3>
              <p>Egzersiz türleri, egzersizler, okuma metinleri ve anlama soruları.</p>
              <div class="sr__stats">
                <admin-stat label="Tür" [value]="counts().exerciseTypes" tone="success" />
                <admin-stat label="Egzersiz" [value]="counts().exercises" tone="success" />
                <admin-stat label="Metin" [value]="counts().readingTexts" tone="success" />
              </div>
              <a routerLink="/dashboard/speed-reading/catalog" class="sr__go">İçeriği yönet</a>
            </article>
          }
          @if (canManagePrograms()) {
            <article class="sr__area admin-tone-info">
              <div class="sr__icon"><mat-icon aria-hidden="true">account_tree</mat-icon></div>
              <h3>Programlar ve öğrenme yolları</h3>
              <p>Program şablonları, öğrenme yolu düğümleri, içerikler ve ön koşullar.</p>
              <div class="sr__stats">
                <admin-stat label="Program" [value]="counts().programs" tone="info" />
                <admin-stat label="Öğrenme yolu" [value]="counts().learningPaths" tone="info" />
              </div>
              <a routerLink="/dashboard/speed-reading/programs" class="sr__go">Programları yönet</a>
            </article>
          }
          @if (canManageGamification()) {
            <article class="sr__area admin-tone-honey">
              <div class="sr__icon"><mat-icon aria-hidden="true">emoji_events</mat-icon></div>
              <h3>Başarımlar ve rozetler</h3>
              <p>Kazanım kriterleri, rozetler, seviyeler ve XP ödülleri.</p>
              <div class="sr__stats"><admin-stat label="Kazanım" [value]="counts().achievements" tone="honey" /></div>
              <a routerLink="/dashboard/speed-reading/achievements" class="sr__go">Kazanımları yönet</a>
            </article>
          }
        </div>

        @if (!canManageContent() && !canManagePrograms() && !canManageGamification()) {
          <p class="sr__note">Yönetim kartları, hesabınıza tanımlanan Hızlı Okuma içerik, program veya oyunlaştırma yetkilerine göre gösterilir.</p>
        }
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .sr { display: grid; gap: 22px; }
    .sr__head { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .sr__head h1 { margin: 0; font-size: 1.75rem; font-weight: 700; color: var(--ds-text); }
    .sr__head p { margin: 4px 0 0; color: var(--ds-muted); }
    .sr__refresh { min-height: 42px; padding: 8px 18px; border: 1px solid var(--ds-field); border-radius: 999px; background: var(--ds-surface); color: var(--ds-text); font: inherit; font-weight: 600; cursor: pointer; }
    .sr__refresh:disabled { opacity: .6; cursor: progress; }
    .sr__alert { padding: 12px 16px; border-radius: 14px; background: var(--ds-danger-soft); color: var(--ds-danger); }
    .sr__status { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr) minmax(0, 1.6fr); gap: 16px; }
    .sr__status > article { display: grid; align-content: start; gap: 10px; padding: 18px 20px; border-radius: 18px; background: var(--ds-surface); box-shadow: var(--ds-shadow-raised, var(--ds-shadow)); }
    .sr__label { color: var(--ds-muted); font-size: .8125rem; font-weight: 600; }
    .sr__mode strong { font-size: 1.5rem; color: var(--ds-text); }
    .sr__links ul { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
    .sr__links li { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border-radius: 12px; background: var(--ds-soft); font-weight: 600; }
    .sr__links li span { margin-left: auto; color: var(--ds-muted); font-weight: 500; font-size: .875rem; }
    .sr__note { display: block; margin-top: 8px; color: var(--ds-muted); font-size: .75rem; line-height: 1.4; }
    .sr__links i { width: 10px; height: 10px; border-radius: 50%; background: var(--ds-field); }
    .sr__links li.on { background: var(--ds-success-soft); }
    .sr__links li.on i { background: var(--ds-success); box-shadow: 0 0 0 4px color-mix(in srgb, var(--ds-success) 20%, transparent); animation: sr-pulse 2.4s ease-in-out infinite; }
    .sr__links li.on span { color: var(--ds-success); }
    .sr__title { margin: 6px 0 0; font-size: 1.125rem; font-weight: 700; color: var(--ds-text); }
    .sr__areas { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; }
    .sr__area { display: grid; align-content: start; gap: 10px; padding: 20px; border-radius: 20px; background: var(--ds-surface); box-shadow: var(--ds-shadow-raised, var(--ds-shadow));
      border-top: 4px solid var(--tone); transition: transform .2s ease, box-shadow .2s ease; }
    .sr__area:hover { transform: translateY(-3px); box-shadow: var(--ds-shadow-hover, var(--ds-shadow)); }
    .sr__icon { display: grid; place-items: center; width: 42px; height: 42px; border-radius: 12px; background: var(--tone-soft); color: var(--tone); }
    .sr__area h3 { margin: 4px 0 0; font-size: 1rem; font-weight: 700; color: var(--ds-text); }
    .sr__area p { margin: 0; color: var(--ds-muted); font-size: .875rem; }
    .sr__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(80px, 1fr)); gap: 8px; }
    .sr__go { justify-self: start; display: inline-flex; align-items: center; min-height: 40px; padding: 8px 16px; border: 2px solid var(--tone); border-radius: 999px;
      color: var(--tone); font-size: .875rem; font-weight: 700; text-decoration: none; transition: background-color .2s ease, color .2s ease; }
    .sr__go:hover { background: var(--tone); color: var(--ds-surface); }
    .sr__go:focus-visible, .sr__refresh:focus-visible { outline: 3px solid var(--ds-accent); outline-offset: 2px; }
    .sr__note { margin: 0; padding: 16px; border-radius: 16px; background: var(--ds-surface); color: var(--ds-muted); }
    @keyframes sr-pulse { 0%, 100% { box-shadow: 0 0 0 3px color-mix(in srgb, var(--ds-success) 25%, transparent); } 50% { box-shadow: 0 0 0 7px color-mix(in srgb, var(--ds-success) 0%, transparent); } }
    @media (max-width: 960px) { .sr__status { grid-template-columns: minmax(0, 1fr); } }
    @media (prefers-reduced-motion: reduce) { .sr__links li.on i { animation: none; } .sr__area { transition: none; } .sr__area:hover { transform: none; } }
  `]
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

  readonly contentParts = computed<AdminPart[]>(() => [
    { label: 'Egzersiz', value: this.counts().exercises, tone: 'success' },
    { label: 'Okuma metni', value: this.counts().readingTexts, tone: 'info' },
    { label: 'Egzersiz türü', value: this.counts().exerciseTypes, tone: 'honey' }]);

  /** The service reports its mode in English; the panel says it in Turkish. */
  modeLabel(mode: string): string {
    return ({ Standalone: 'Bağımsız', Platform: 'Platform içinde' } as Record<string, string>)[mode] ?? mode;
  }

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
      error: () => this.error.set('Hızlı okuma servis özeti yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }
}
