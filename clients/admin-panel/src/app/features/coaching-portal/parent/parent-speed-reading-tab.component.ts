import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { trNumber, trPercent } from '../../../../../../shared/utils/tr-number';
import { CsCardComponent, CsProgressBarComponent, CsStatusChipComponent } from '../ui';
import { CoachingParentService, ParentSpeedReadingSubscription } from './coaching-parent.service';
import { CoachingDatePipe } from '../coaching-labels';
import { possessive } from './parent-model';
import { ParentBar, ParentReadingTrendComponent, ParentWeekBarsComponent } from './parent-charts.component';
import { ParentSpeedReadingOverview, firstName, shortDate } from './parent-model';

/** V2 sends "active", "completed" or "inactive". */
const programStatus: Record<string, string> = { active: 'Devam ediyor', completed: 'Tamamlandı', inactive: 'Ara verildi' };

/**
 * V4 (01.11.2026): the Hızlı okuma tab of a child — program progress, reading speed and comprehension, this week and the
 * last eight weeks, streak and badges, and the latest measured results. Read-only; every number comes from the overview.
 */
@Component({
  selector: 'app-parent-speed-reading-tab',
  standalone: true,
  imports: [CsCardComponent, CsProgressBarComponent, CsStatusChipComponent, ParentReadingTrendComponent, ParentWeekBarsComponent, CoachingDatePipe],
  template: `
    @if (forbidden()) { <p class="cp-notice" role="status">{{ name() }} hızlı okuma ilerlemesini şu an seninle paylaşmıyor ya da veli bağlantısı artık geçerli değil. Çocuklarım sayfasından durumu görebilirsin.</p> }
    @else if (error()) { <p class="cp-alert" role="alert">Hızlı okuma ilerlemesi şu an görüntülenemiyor. Biraz sonra yeniden dene.</p> }
    @else if (view(); as v) {
      <div class="cp-top">
        <cs-card tone="raised" class="cp-program" data-testid="reading-program">
          <h2>Program</h2>
          @if (v.program; as program) {
            <p class="cp-big"><strong>{{ program.currentDay }}/{{ program.totalDays }}. gün</strong>
              <cs-status-chip [label]="status(program.status)" [tone]="isCompleted(program.status) ? 'success' : 'brand'" /></p>
            <p class="cp-line">{{ program.name }}, program seviyesi {{ program.level }}</p>
            <cs-progress-bar [value]="program.completedDays" [max]="program.totalDays" [label]="'Programda tamamlanan gün: ' + program.completedDays + '/' + program.totalDays" />
            <p class="cp-muted">Tamamlanan görev: <strong>{{ program.completedTasks }}/{{ program.totalTasks }}</strong>, tamamlanan gün: {{ program.completedDays }}</p>
          } @else {
            <p class="cp-line">{{ name() }} henüz bir hızlı okuma programına başlamadı.</p>
          }
          @if (v.lastActivityAt) { <p class="cp-muted">Son çalışma: {{ day(v.lastActivityAt) }}</p> }
        </cs-card>
        <cs-card class="cp-stats" data-testid="reading-week">
          <h2>Bu hafta</h2>
          <dl class="cp-stat-grid">
            <div><dt>Okuma süresi</dt><dd>{{ v.thisWeek.minutes }} dk</dd></div>
            <div><dt>Çalıştığı gün</dt><dd>{{ v.thisWeek.activeDays }} gün</dd></div>
            <div><dt>Alıştırma</dt><dd>{{ v.thisWeek.exercisesCompleted }}</dd></div>
          </dl>
        </cs-card>
      </div>

      <section class="cp-section" aria-labelledby="cp-reading-title" data-testid="reading-trend">
        <h2 id="cp-reading-title">Okuma hızı ve anlama</h2>
        @if (v.reading.trend.length) {
          <dl class="cp-stat-grid">
            <div><dt>Son hız</dt><dd>{{ v.reading.latestWpm ?? '—' }} <small>kelime/dk</small></dd></div>
            @if (speedChange() !== null) { <div><dt>İlk ölçüme göre</dt><dd>{{ speedChange()! >= 0 ? '+' : '−' }}{{ abs(speedChange()!) }} <small>kelime/dk</small></dd></div> }
            <div><dt>Son anlama</dt><dd>{{ pct(v.reading.latestComprehension) }}</dd></div>
          </dl>
          <div class="cp-card"><cp-reading-trend [points]="v.reading.trend" [childName]="name()" /></div>
        } @else {
          <p class="cp-empty">Henüz ölçülen bir okuma sonucu yok. {{ name() }} ölçümlü bir okuma alıştırması yaptığında hızı ve anlaması burada görünür.</p>
        }
      </section>

      <section class="cp-section" aria-labelledby="cp-reading-weeks-title" data-testid="reading-weeks">
        <h2 id="cp-reading-weeks-title">Son 8 hafta</h2>
        <p class="cp-muted">Her hafta okumaya ayrılan süre ve çalışılan gün sayısı.</p>
        @if (weekBars().length) { <cp-week-bars [bars]="weekBars()" [label]="'Son ' + weekBars().length + ' hafta okuma süresi'" /> }
        @else { <p class="cp-muted">Henüz haftalık bir çalışma yok.</p> }
      </section>

      <section class="cp-section" aria-labelledby="cp-reading-badges-title">
        <h2 id="cp-reading-badges-title">Seri ve rozetler</h2>
        @if (v.gamification; as game) {
          <dl class="cp-stat-grid">
            <div><dt>Çalışma serisi</dt><dd>{{ game.streakDays }} gün</dd></div>
            <div><dt>XP seviyesi</dt><dd>{{ game.level }}</dd></div>
            <div><dt>Puan (XP)</dt><dd>{{ format(game.xp) }}</dd></div>
            <div><dt>Rozet</dt><dd>{{ game.badgesEarned }}</dd></div>
          </dl>
          @if (game.recentBadges.length) {
            <ul class="cp-chips" aria-label="Son kazanılan rozetler">@for (badge of game.recentBadges; track $index) { <li><strong>{{ badge.name }}</strong> <small>{{ day(badge.earnedAt) }}</small></li> }</ul>
          } @else { <p class="cp-muted">{{ name() }} henüz rozet kazanmadı.</p> }
        } @else {
          <p class="cp-empty">Henüz XP ya da rozet yok. {{ name() }} alıştırma yaptıkça serisi ve rozetleri burada görünür.</p>
        }
      </section>

      <section class="cp-section" aria-labelledby="cp-reading-results-title">
        <h2 id="cp-reading-results-title">Son ölçülen sonuçlar</h2>
        <ul class="cp-rows">
          @for (result of v.recentResults; track $index) {
            <li><span class="cp-row-main"><strong>{{ result.exerciseName }}</strong><small>{{ result.exerciseType ? result.exerciseType + ', ' : '' }}{{ day(result.date) }}</small></span>
              <span class="cp-row-side">@if (result.wpm !== null) { <strong>{{ result.wpm }} kelime/dk</strong> }@if (result.comprehension !== null) { <span>anlama {{ pct(result.comprehension) }}</span> }@if (result.wpm === null && result.score !== null) { <strong>{{ format(result.score) }} puan</strong> }</span></li>
          } @empty { <li class="cp-muted">Henüz ölçülen sonuç yok. Öğrenme ve tekrar alıştırmaları ölçüme sayılmaz.</li> }
        </ul>
      </section>

      <section class="cp-section" aria-labelledby="cp-reading-payments-title" data-testid="parent-reading-subscription">
        <h2 id="cp-reading-payments-title">Ödemeler</h2>
        <div class="cp-card">
          <h3>Hızlı Okuma aboneliği</h3>
          @if (subscription(); as current) {
            @if (!current.requireSubscription) {
              <p class="cp-line">{{ name() }} Hızlı Okuma'yı şu an kullanabiliyor; bir ödeme yapılması gerekmiyor.</p>
            } @else if (current.hasAccess) {
              <p class="cp-line"><cs-status-chip label="Etkin" tone="success" />
                @if (current.accessUntil) { <span class="cp-muted">{{ current.accessUntil | coachingDate }} tarihine kadar</span> }</p>
            } @else {
              <p class="cp-line">{{ possessiveName() }} etkin bir Hızlı Okuma aboneliği yok.</p>
              <p class="cp-muted">Abonelik {{ possessiveName() }} kendi hesabından, Hızlı Okuma'daki "Aboneliğim" sayfasından alınır. Havaleyi sen de yapabilirsin: açıklamaya o sayfadaki ödeme kodunu yazman yeterli.</p>
            }
          } @else if (subscriptionError()) { <p class="cp-muted">Abonelik bilgisi yüklenemedi.</p> }
          @else { <p class="cp-muted" role="status">Abonelik bilgisi yükleniyor…</p> }
        </div>
      </section>
    } @else { <div class="cp-skeleton" role="status" aria-label="Hızlı okuma ilerlemesi yükleniyor"></div> }`
})
export class ParentSpeedReadingTabComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  readonly studentId = input.required<string>();
  readonly childName = input('');
  readonly name = computed(() => firstName(this.childName()));
  readonly view = signal<ParentSpeedReadingOverview | null>(null);
  readonly possessiveName = computed(() => possessive(this.name()));
  readonly subscription = signal<ParentSpeedReadingSubscription | null>(null);
  readonly subscriptionError = signal(false);
  readonly error = signal(false);
  /** 403 ParentReadForbidden: the child hid speed reading or the link is no longer valid. */
  readonly forbidden = signal(false);
  readonly speedChange = computed(() => { const r = this.view()?.reading; return r && r.latestWpm !== null && r.firstWpm !== null && r.trend.length > 1 ? r.latestWpm - r.firstWpm : null; });
  readonly weekBars = computed<ParentBar[]>(() => {
    const weeks = this.view()?.weeks ?? []; const most = Math.max(0, ...weeks.map(w => w.minutes));
    return weeks.map((week, index) => ({ label: index === weeks.length - 1 ? 'Bu hafta' : shortDate(week.weekStart), value: week.minutes, max: most,
      text: `${week.minutes} dk`, detail: `${week.activeDays} gün` }));
  });

  ngOnInit(): void {
    this.service.getSpeedReadingSubscription(this.studentId()).subscribe({
      next: value => this.subscription.set(value), error: () => this.subscriptionError.set(true) });
    this.service.getSpeedReadingOverview(this.studentId()).subscribe({ next: view => this.view.set(normalize(view)), error: (response: { status?: number }) => response?.status === 403 ? this.forbidden.set(true) : this.error.set(true) });
  }

  status(value: string) { return programStatus[value?.toLowerCase()] ?? 'Devam ediyor'; }
  isCompleted(value: string) { return value?.toLowerCase() === 'completed'; }
  pct(value: number | null) { return trPercent(value, 1); }
  day(iso: string) { return shortDate(iso); }
  format(value: number) { return trNumber(value); }
  abs(value: number) { return Math.abs(value); }
}

/** Missing sections become empty ones, so a partial answer never breaks the page. */
function normalize(view: ParentSpeedReadingOverview): ParentSpeedReadingOverview {
  const reading: Partial<ParentSpeedReadingOverview['reading']> = view.reading ?? {};
  const game: Partial<NonNullable<ParentSpeedReadingOverview['gamification']>> = view.gamification ?? {};
  return { ...view, program: view.program ?? null,
    thisWeek: view.thisWeek ?? { minutes: 0, activeDays: 0, exercisesCompleted: 0 }, weeks: view.weeks ?? [],
    reading: { latestWpm: reading.latestWpm ?? null, firstWpm: reading.firstWpm ?? null, latestComprehension: reading.latestComprehension ?? null, trend: reading.trend ?? [] },
    gamification: view.gamification ? { level: game.level ?? 1, xp: game.xp ?? 0, streakDays: game.streakDays ?? 0, badgesEarned: game.badgesEarned ?? 0, recentBadges: game.recentBadges ?? [] } : null,
    recentResults: view.recentResults ?? [] };
}
