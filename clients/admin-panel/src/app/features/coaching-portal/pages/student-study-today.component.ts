import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of, switchMap, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyTask } from '../../../core/services/coaching-study-planning.service';
import { CoachingIconComponent } from '../coaching-icon.component';

@Component({ selector: 'app-student-study-today', standalone: true, imports: [CommonModule, RouterLink, CoachingIconComponent],
  templateUrl: './student-study-today.component.html', styleUrl: './student-study-today.component.scss' })
export class StudentStudyTodayComponent implements OnInit {
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly busy = signal(false); readonly error = signal<string | null>(null);
  readonly title = signal<string | null>(null); readonly timeZone = signal(''); readonly today = signal('');
  readonly todayTasks = signal<StudyTask[]>([]); readonly overdue = signal<StudyTask[]>([]);
  readonly planId = signal<string | null>(null);
  readonly scheduledToday = signal(0); readonly completedToday = signal(0);
  readonly recordedMinutes = signal<number | null>(null);
  readonly remainingMinutes = computed(() => this.todayTasks().reduce((sum, task) => sum + task.plannedMinutes, 0));
  readonly dailyPercentage = computed(() => this.scheduledToday() ? Math.round(this.completedToday() * 100 / this.scheduledToday()) : null);
  ngOnInit() { this.load(); }
  load() {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null); this.title.set(null); this.todayTasks.set([]); this.overdue.set([]);
    this.planId.set(null); this.scheduledToday.set(0); this.completedToday.set(0); this.recordedMinutes.set(null);
    forkJoin({ plans: this.service.list(1, 'Active'), hours: this.service.getAvailability().pipe(catchError(error =>
      error?.status === 404 ? of({ timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone }) : throwError(() => error))) })
      .pipe(switchMap(({ plans, hours }) => {
        const timeZone = hours?.timeZoneId ?? Intl.DateTimeFormat().resolvedOptions().timeZone;
        this.timeZone.set(timeZone);
        const parts = new Intl.DateTimeFormat('en-US', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(Date.now()));
        const part = (type: string) => parts.find(x => x.type === type)!.value;
        this.today.set(`${part('year')}-${part('month')}-${part('day')}`);
        return plans.items.length ? this.service.get(plans.items[0].id) : of(null);
      }), takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: plan => {
        this.title.set(plan?.title ?? null);
        this.planId.set(plan?.id ?? null);
        const scheduled = (plan?.tasks ?? []).filter(task => task.plannedDate === this.today());
        const completed = scheduled.filter(task => task.isCompleted);
        this.scheduledToday.set(scheduled.length); this.completedToday.set(completed.length);
        const recorded = completed.filter(task => task.actualMinutes != null);
        this.recordedMinutes.set(recorded.length ? recorded.reduce((sum, task) => sum + task.actualMinutes!, 0) : null);
        const pending = (plan?.tasks ?? []).filter(x => !x.isCompleted).sort((a, b) => a.plannedDate.localeCompare(b.plannedDate) || a.id.localeCompare(b.id));
        this.todayTasks.set(pending.filter(x => x.plannedDate === this.today()));
        this.overdue.set(pending.filter(x => x.plannedDate < this.today()));
      }, error: () => this.error.set('Çalışma planı yüklenemedi. Mevcut planın durumu doğrulanamadı.') });
  }
}
