import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, map, of, switchMap, throwError } from 'rxjs';
import { CoachingStudyPlanningService, WeeklyPlanSource } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyOutcomePlanService, WeeklyOutcomeRequest } from '../../../core/services/coaching-weekly-outcome-plan.service';

export const EXTENDED_PLAN_TITLE = 'Haftalık derslerimden çalışma planım';
function addDays(iso: string, days: number) { const [y, m, d] = iso.split('-').map(Number); const date = new Date(Date.UTC(y, m - 1, d)); date.setUTCDate(date.getUTCDate() + days); return date.toISOString().slice(0, 10); }
function shortDate(iso: string) { const [y, m, d] = iso.split('-'); return `${d}.${m}.${y}`; }

/**
 * User decision 15.11.2026: a plan made for a week (or up to a chosen date) is continued with one click. A new plan runs
 * from today to a week after the current end; the waiting work of the days up to the current end keeps its day and time
 * (keepUntil) and new work is added only after it.
 * The user chooses whether the topics of untouched past tasks come first or are left out this time.
 */
@Component({
  selector: 'app-plan-extend', standalone: true, changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (source?.partial && active && !readOnly) {
    <div class="publish-box" data-testid="plan-extend">
      <h3>Plan {{ date(source!.endDate) }} tarihinde bitiyor</h3>
      @if (!asking()) {
        <button type="button" data-testid="plan-extend-open" (click)="asking.set(true)" [disabled]="busy()">Gelecek haftayı ekle</button>
      } @else {
        <p>Plan {{ date(nextEnd()) }} tarihine kadar uzatılır. Tamamlanan çalışmalar korunur; {{ date(source!.endDate) }} tarihine kadar olan bekleyen
          çalışmalar gününde ve saatinde kalır, yeni çalışmalar ondan sonraki günlere eklenir.</p>
        <fieldset [disabled]="busy()"><legend>Önceki günlerden kalan, hiç başlanmamış çalışmalar</legend>
          <label class="check"><input type="radio" name="plan-extend-carry" [checked]="!leave()" (change)="leave.set(false)"> Önce onlar yerleşsin, yeni konular kalan boşluğa girsin</label>
          <label class="check"><input type="radio" name="plan-extend-carry" [checked]="leave()" (change)="leave.set(true)"> Bu sefer plana eklenmesin (sonraki eklemede yeniden sorulur)</label>
        </fieldset>
        <div class="actions">
          <button type="button" data-testid="plan-extend-confirm" (click)="extend()" [disabled]="busy()">{{ busy() ? 'Hazırlanıyor…' : 'Ekle ve yayımla' }}</button>
          <button type="button" class="secondary" (click)="asking.set(false)" [disabled]="busy()">Vazgeç</button>
        </div>
      }
      @if (error()) { <p role="alert" data-testid="plan-extend-error">{{ error() }}</p> }
    </div>
  }`
})
export class PlanExtendComponent {
  @Input() source: WeeklyPlanSource | null | undefined;
  @Input() active = false;
  @Input() readOnly = false;
  @Input() staffStudentId: string | null = null;
  /** The id of the plan now in force. */
  @Output() readonly extended = new EventEmitter<string>();
  private readonly engine = inject(CoachingWeeklyOutcomePlanService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly destroy = inject(DestroyRef);
  readonly asking = signal(false);
  readonly leave = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  private today = new Date().toISOString().slice(0, 10);

  date(iso: string) { return shortDate(iso); }
  /** A week after the plan's end; a plan that ended some time ago gets a week from yesterday. */
  nextEnd() { const end = this.source?.endDate ?? this.today; const base = end < this.today ? addDays(this.today, -1) : end; return addDays(base, 7); }

  extend() {
    if (this.busy() || !this.source) return;
    const student = this.staffStudentId ?? undefined;
    this.busy.set(true); this.error.set(null);
    this.engine.options(student).pipe(
      switchMap(options => {
        this.today = options.startDate;
        const wanted = this.nextEnd(), partial = wanted < options.endDate;
        const request: WeeklyOutcomeRequest = { startDate: options.startDate, endDate: partial ? wanted : options.endDate,
          expectedWeeklyDraftVersion: options.weeklyDraftVersion, reuseFreedSlots: true, engineVersion: 2,
          ...(partial ? { partial: true } : {}), ...(this.leave() ? { leaveUnfinished: true } : {}),
          // The rest of the current plan stays where it is; only what comes after it is new.
          ...(this.source!.endDate >= options.startDate ? { keepUntil: this.source!.endDate } : {}) };
        return this.engine.preview(request, student);
      }),
      switchMap(preview => preview.canCreate ? of(preview)
        : throwError(() => ({ error: { message: 'Bu haftalık düzenle yeni hafta hazırlanamadı. Plan sihirbazından yeniden hazırlayabilirsin.' } }))),
      switchMap(preview => this.engine.create({ title: EXTENDED_PLAN_TITLE, preview: preview.request, fingerprint: preview.fingerprint }, student)),
      switchMap(created => this.planning.publish(created.id, created.version, this.staffStudentId).pipe(map(() => created.id))),
      takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))
    ).subscribe({
      next: id => { this.asking.set(false); this.extended.emit(id); },
      error: e => this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'Gelecek hafta eklenemedi. Yeniden deneyebilirsin.')
    });
  }
}
