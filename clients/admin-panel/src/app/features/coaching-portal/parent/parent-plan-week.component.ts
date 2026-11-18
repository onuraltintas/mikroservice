import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CoachingParentService } from './coaching-parent.service';
import { ParentPlanWeek, parentPlanDays, planClock, parentTaskState, shiftWeek, shortDate } from './parent-model';

/**
 * Live test 16.11.2026 (#49, decided by the owner): the parent reads the child's plan of a week — which day holds which
 * topic — and can change nothing. The week before and the week after are one press away.
 */
@Component({
  selector: 'cp-plan-week',
  standalone: true,
  template: `
    <section class="cp-section" aria-labelledby="cp-plan-title" data-testid="parent-plan">
      <div class="head">
        <h2 id="cp-plan-title">Haftalık plan</h2>
        @if (week(); as w) {
          <div class="nav" role="group" aria-label="Hafta seçimi">
            <button type="button" [disabled]="loading()" (click)="go(-1)" aria-label="Önceki hafta">‹</button>
            <span aria-live="polite">{{ range() }}</span>
            <button type="button" [disabled]="loading()" (click)="go(1)" aria-label="Sonraki hafta">›</button>
          </div>
        }
      </div>
      <p class="cp-muted">{{ name() }} için planlanan çalışmalar. Planı {{ name() }} ve öğretmeni düzenler; buradan değiştirilemez.</p>
      @if (error()) { <p class="cp-muted" role="alert">Plan şu an yüklenemedi.</p> }
      @else if (week(); as w) {
        @if (!w.hasActivePlan) { <p class="cp-muted" data-testid="parent-plan-none">{{ name() }} için yayında bir çalışma planı yok.</p> }
        @else if (!w.tasks.length) { <p class="cp-muted" data-testid="parent-plan-empty">Bu hafta için planlanmış çalışma yok.</p> }
        @else {
          <ol class="days">
            @for (day of days(); track day.date) {
              <li [class.today]="day.isToday">
                <h3>{{ day.label }}@if (day.isToday) { <small> · bugün</small> }</h3>
                @if (day.tasks.length) {
                  <ul>
                    @for (task of day.tasks; track $index) {
                      <li [class.done]="task.completed">
                        <span class="when">{{ clock(task.startMinute, task.minutes) }}</span>
                        <span class="what"><strong>{{ task.title }}</strong>
                          <small>{{ task.lesson || 'Çalışma' }}@if (task.questions) { · {{ task.questions }} soru }@if (task.continuation) { · yarım kalanın devamı }</small></span>
                        <span class="state" [class.late]="taskState(task) === 'Gecikti'">{{ taskState(task) }}</span>
                      </li>
                    }
                  </ul>
                } @else { <p class="cp-muted">Çalışma yok.</p> }
              </li>
            }
          </ol>
        }
      } @else { <div class="cp-skeleton" role="status" aria-label="Plan yükleniyor"></div> }
    </section>`,
  styles: [`
    .head{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:var(--coaching-space-3)}
    .nav{display:inline-flex;align-items:center;gap:var(--coaching-space-2);font-size:var(--coaching-text-sm);font-weight:650;color:var(--ui-text)}
    .nav button{width:44px;height:44px;border:1px solid var(--ui-border);border-radius:var(--coaching-radius-control);background:var(--ui-surface);color:var(--ui-text);font-size:20px;cursor:pointer}
    .nav button:disabled{opacity:.5;cursor:default}
    .days{display:grid;gap:var(--coaching-space-3);margin:0;padding:0;list-style:none}
    .days>li{display:grid;gap:var(--coaching-space-2);padding:var(--coaching-space-4);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-surface);min-width:0}
    .days>li.today{border-color:var(--ui-brand)}
    h3{margin:0;font-size:var(--coaching-text-body);font-weight:750;color:var(--ui-text)}h3 small{font-weight:600;color:var(--ui-brand)}
    ul{display:grid;gap:var(--coaching-space-2);margin:0;padding:0;list-style:none}
    ul li{display:grid;grid-template-columns:minmax(0,11rem) minmax(0,1fr) auto;gap:var(--coaching-space-3);align-items:baseline;min-width:0}
    .when{font-size:var(--coaching-text-sm);font-variant-numeric:tabular-nums;color:var(--ui-text-muted)}
    .what{display:grid;min-width:0}.what strong{font-weight:650;color:var(--ui-text);overflow-wrap:anywhere}.what small{color:var(--ui-text-muted);font-size:12px}
    .state{font-size:12px;font-weight:650;color:var(--ui-text-muted);white-space:nowrap}.done .state{color:var(--ui-success, var(--ui-brand))}.state.late{color:var(--ui-danger)}
    @media (max-width:600px){ul li{grid-template-columns:minmax(0,1fr) auto}.when{grid-column:1 / -1}}
  `]
})
export class ParentPlanWeekComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  readonly studentId = input.required<string>();
  readonly name = input.required<string>();
  readonly week = signal<ParentPlanWeek | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly days = computed(() => this.week() ? parentPlanDays(this.week()!) : []);
  readonly range = computed(() => { const w = this.week(); return w ? `${shortDate(w.weekStart)} – ${shortDate(lastDay(w.weekStart))}` : ''; });
  readonly clock = planClock;
  taskState(task: { completed: boolean; date: string }): string { return parentTaskState(task, this.week()?.today ?? ''); }

  ngOnInit(): void { this.load(); }
  go(weeks: number): void { const w = this.week(); if (w) this.load(shiftWeek(w.weekStart, weeks)); }

  private load(weekStart?: string): void {
    this.loading.set(true); this.error.set(false);
    this.service.getPlanWeek(this.studentId(), weekStart).subscribe({
      next: week => { this.week.set(week); this.loading.set(false); },
      // A week outside the readable range keeps the week on the screen.
      error: () => { this.loading.set(false); if (!this.week()) this.error.set(true); }
    });
  }
}

function lastDay(weekStart: string): string {
  return new Date(new Date(weekStart + 'T00:00:00Z').getTime() + 6 * 86_400_000).toISOString().slice(0, 10);
}
