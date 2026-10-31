import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CoachingWeeklyReviewService, WeeklyHome } from '../../../core/services/coaching-weekly-review.service';
import { StudentMotivation } from '../../../../../shared/coaching-motivation.component';
import { CsCardComponent, CsProgressBarComponent, CsStatusChipComponent } from '../ui';
import { CoachingGrowthWeekService } from './coaching-growth-week.service';
import { WeekBar, badgeProgress, formatDay, hours, weekBars } from './growth-model';

/**
 * T6 (31.10.2026): Gelişimim › Haftam at a glance — study time and finished tasks of the last weeks, this week's review
 * and open decisions, and how close the next badges are.
 */
@Component({
  selector: 'app-student-week-summary',
  standalone: true,
  imports: [RouterLink, CsCardComponent, CsProgressBarComponent, CsStatusChipComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cs-growth-week' },
  template: `
    <div class="cs-growth-pair">
      <cs-card tone="raised" class="cs-week-bars">
        <div class="cs-growth-card-head"><h2>Çalışma süren</h2>@if (thisWeek(); as week) { <cs-status-chip [label]="week.tasks + ' görev bitti'" tone="success" /> }</div>
        @if (weeksError()) { <p class="cs-growth-alert" role="alert">Haftalık çalışma süren yüklenemedi.</p> }
        @else if (!bars().length) { <div class="cs-growth-skeleton" role="status" aria-label="Haftaların yükleniyor"></div> }
        @else if (!hasStudy()) { <p class="cs-growth-muted">Son haftalarda kaydedilmiş çalışma yok. Planındaki görevleri bitirdikçe burada birikir.</p> }
        @else {
          <p class="cs-growth-lead"><strong>{{ total() }}</strong> <span>son {{ bars().length }} haftada</span></p>
          <ol class="cs-week-chart" aria-label="Haftalara göre çalışma süresi">
            @for (bar of bars(); track bar.label; let last = $last) {
              <li data-testid="week-bar" [class.is-now]="last">
                <span class="cs-week-value">{{ bar.minutesText }}</span>
                <span class="cs-week-column" aria-hidden="true"><span [style.height.%]="bar.minutesPercent || 2"></span></span>
                <span class="cs-week-label">{{ bar.label }}</span>
                <span class="cs-week-tasks">{{ taskText(bar) }}</span>
              </li>
            }
          </ol>
        }
      </cs-card>
      <cs-card class="cs-week-review">
        <h2>Haftalık değerlendirme</h2>
        @if (home(); as review) {
          @if (review.needsReflection) {
            <a class="cs-growth-callout" routerLink="." fragment="haftalik-degerlendirme">Bu haftayı değerlendir<small>Neler iyi gitti, neye odaklanacaksın? 2 dakikanı alır.</small></a>
          } @else { <p class="cs-growth-muted">Bu haftanın değerlendirmesi tamam.</p> }
          <h3>Açık kararların @if (review.openDecisions) { <cs-status-chip [label]="'' + review.openDecisions" tone="brand" /> }</h3>
          @for (decision of review.decisions; track decision.id) {
            <p class="cs-decision" [class.is-late]="decision.isOverdue"><strong>{{ decision.title }}</strong><small>{{ decision.isOverdue ? 'Süresi geçti · ' : '' }}{{ day(decision.dueOn) }}</small></p>
          } @empty { <p class="cs-growth-muted">Açık kararın yok.</p> }
        } @else if (homeError()) { <p class="cs-growth-alert" role="alert">Haftalık değerlendirme yüklenemedi.</p> }
      </cs-card>
    </div>
    @if (badges().length) {
      <cs-card class="cs-badges">
        <div class="cs-growth-card-head"><h2>Rozetlerin</h2>@if (motivation(); as m) { <span class="cs-growth-muted">{{ m.currentStreak }} günlük seri</span> }</div>
        <ul>
          @for (badge of badges(); track badge.code) {
            <li [class.is-earned]="badge.earned">
              <span class="cs-badge-text">{{ badge.text }}</span>
              <cs-progress-bar [value]="badge.progress" [max]="badge.target" [label]="badge.text" [tone]="badge.earned ? 'success' : 'accent'" />
            </li>
          }
        </ul>
      </cs-card>
    }`
})
export class StudentWeekSummaryComponent implements OnInit {
  private readonly weeks = inject(CoachingGrowthWeekService);
  private readonly review = inject(CoachingWeeklyReviewService);
  private readonly destroyRef = inject(DestroyRef);
  readonly bars = signal<WeekBar[]>([]);
  readonly weeksError = signal(false);
  readonly home = signal<WeeklyHome | null>(null);
  readonly homeError = signal(false);
  readonly motivation = signal<StudentMotivation | null>(null);
  readonly badges = computed(() => badgeProgress(this.motivation()?.badges ?? []));
  readonly thisWeek = computed(() => this.bars().at(-1) ?? null);
  readonly hasStudy = computed(() => this.bars().some(x => x.minutes > 0 || x.tasks > 0));
  readonly total = computed(() => hours(this.bars().reduce((sum, x) => sum + x.minutes, 0)));

  ngOnInit() {
    this.weeks.weeks(6).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.bars.set(weekBars(rows)), error: () => this.weeksError.set(true) });
    this.review.home().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: home => this.home.set(home), error: () => this.homeError.set(true) });
    this.weeks.motivation().pipe(catchError(() => of(null)), takeUntilDestroyed(this.destroyRef)).subscribe(value => this.motivation.set(value));
  }

  taskText(bar: WeekBar) { return bar.scheduled ? `${bar.tasks}/${bar.scheduled} görev` : `${bar.tasks} görev`; }
  day(value: string) { return formatDay(`${value.slice(0, 10)}T12:00:00Z`); }
}
