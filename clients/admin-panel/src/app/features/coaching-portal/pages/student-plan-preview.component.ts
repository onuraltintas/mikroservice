import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyDraftService, WeeklyLesson } from '../../../core/services/coaching-weekly-draft.service';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { createProposalFromWindows } from './study-plan-wizard';
import { WeeklySlot, clock, weekDays } from './weekly-calendar';

/**
 * E6 #29 (live test 06.11): "Plan önizlemesi" in Çalışma tercihlerim. It shows the draft itself: the weekly lessons made
 * from the saved availability with the plan wizard's rules, and once they are saved, the wizard's annual preview, which
 * creates the draft that is published on Planım ("Planı yayımla"). Nothing is saved without the student's click.
 */
@Component({
  selector: 'app-student-plan-preview', standalone: true,
  imports: [CommonModule, RouterLink, StudentWeeklyOutcomePlanComponent],
  templateUrl: './student-plan-preview.component.html', styleUrl: './student-study-plan-assistant.component.scss'
})
export class StudentPlanPreviewComponent implements OnInit {
  /** The chosen courses (Derslerim); the weekly lessons are made for these only. */
  @Input() lessonIds: string[] = [];
  /** No availability yet: the setup page opens Müsaitliğim. */
  @Output() editAvailability = new EventEmitter<void>();
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly destroy = inject(DestroyRef);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly loadFailed = signal(false);
  readonly availability = signal<StudyAvailability | null>(null);
  readonly slots = signal<WeeklySlot[]>([]);
  readonly proposal = signal<WeeklySlot[]>([]);
  private lessons: WeeklyLesson[] = [];
  private version: number | null = null;
  readonly clock = clock;

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true); this.error.set(''); this.loadFailed.set(false);
    forkJoin({
      page: this.weekly.get(),
      availability: this.planning.getAvailability().pipe(catchError(e => e?.status === 404 ? of(null) : throwError(() => e)))
    }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false))).subscribe({
      next: ({ page, availability }) => {
        this.lessons = page.lessons; this.version = page.draft?.version ?? null;
        this.slots.set(page.draft?.slots ?? []); this.availability.set(availability ?? null);
        const chosen = new Set(this.lessonIds.map(id => id.toLowerCase()));
        const lessons = this.lessons.filter(lesson => chosen.has(lesson.id.toLowerCase()));
        this.proposal.set(this.slots().length ? [] : createProposalFromWindows(availability?.windows ?? [], lessons, []));
      },
      error: () => { this.loadFailed.set(true); this.error.set('Plan önizlemesi hazırlanamadı. Yeniden deneyebilirsin.'); }
    });
  }

  hasAvailability() { return !!this.availability()?.windows.length; }
  availabilityLines() {
    const order = weekDays.map(day => day.value as string);
    return [...(this.availability()?.windows ?? [])].sort((a, b) => order.indexOf(a.day) - order.indexOf(b.day) || a.startMinute - b.startMinute)
      .map(window => `${this.dayName(window.day)} ${clock(window.startMinute)}–${window.endMinute === 1440 ? '24:00' : clock(window.endMinute)}`);
  }
  dayName(day: string) { return weekDays.find(x => x.value === day)?.label ?? day; }

  /** Saves the previewed weekly lessons (the week the annual preview reads), then the draft preview opens. */
  saveWeek() {
    if (this.saving() || !this.proposal().length) return;
    this.saving.set(true); this.error.set('');
    this.weekly.save(this.version, 'Europe/Istanbul', this.proposal()).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
      next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.proposal.set([]); },
      error: e => this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'Haftalık ders düzenin kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
