import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CoachingLearningSetupService, LearningContext, LearningCourse, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { WeeklyLesson } from '../../../core/services/coaching-weekly-draft.service';

/** Paket H4: Öncelik Yüksek / Normal / Düşük are saved as 1 / 3 / 5; Zorluk Kolay / Normal / Zor as 2 / 3 / 4. */
export const PRIORITY_CHOICES = [{ value: 1, label: 'Yüksek' }, { value: 3, label: 'Normal' }, { value: 5, label: 'Düşük' }] as const;
export const DIFFICULTY_CHOICES = [{ value: 2, label: 'Kolay' }, { value: 3, label: 'Normal' }, { value: 4, label: 'Zor' }] as const;
/** The choice shown for a saved priority: the nearest of 1/3/5 (2 counts as Yüksek, 4 as Düşük). */
export function priorityChoice(value: number | null | undefined) { const v = value ?? 3; return v <= 2 ? 1 : v >= 4 ? 5 : 3; }
/** The choice shown for a saved difficulty: 1 is shown as Kolay, 5 as Zor; none as Normal. */
export function difficultyChoice(value: number | null | undefined) { const v = value ?? 3; return v <= 2 ? 2 : v >= 4 ? 4 : 3; }

/** The study preferences as they are, with new course choices: the same save as Çalışma tercihlerim (with the version). */
export function learningUpdate(context: LearningContext, courses: LearningCourse[]): LearningSetupUpdate {
  return { expectedVersion: context.version, academicYearStart: context.academicYearStart, purpose: context.purpose, targetExamYear: context.targetExamYear,
    primaryScoreType: context.primaryScoreType, foreignLanguage: context.foreignLanguage, targetExamSessionId: context.targetExamSessionId ?? null,
    courses, topics: context.topicStates, startingStatusReviewed: context.startingStatusReviewed };
}

interface CourseRow { lessonId: string; name: string; priority: number; difficulty: number; saved: LearningCourse }

/**
 * Paket H4, step 2 "Derslerin": the chosen courses grouped by exam (TYT, AYT, YDT…) with two plain choices each. Only a course the
 * student changes gets the new value, so a 2 or a 1 saved elsewhere stays until she picks another. A teacher only reads them.
 */
@Component({
  selector: 'app-wizard-courses-step', standalone: true, imports: [CommonModule, RouterLink],
  templateUrl: './wizard-courses-step.component.html', styleUrl: './student-study-plan-assistant.component.scss'
})
export class WizardCoursesStepComponent implements OnChanges {
  @Input() staffStudentId: string | null = null;
  @Input() context: LearningContext | null = null;
  @Input() lessons: WeeklyLesson[] = [];
  @Output() saved = new EventEmitter<LearningSetupView>();
  @Output() reload = new EventEmitter<void>();
  @Output() done = new EventEmitter<void>();
  @Output() back = new EventEmitter<void>();
  @Input() canGoBack = true;
  private readonly learning = inject(CoachingLearningSetupService);
  private readonly destroy = inject(DestroyRef);
  readonly priorities = PRIORITY_CHOICES;
  readonly difficulties = DIFFICULTY_CHOICES;
  readonly groups = signal<{ name: string; rows: CourseRow[] }[]>([]);
  readonly changed = signal<Record<string, { priority?: number; difficulty?: number }>>({});
  readonly saving = signal(false);
  readonly error = signal('');
  /** PR 128 review M2 pattern: a course the server refused gets the message in its own row. */
  readonly courseError = signal<{ lessonId: string; message: string } | null>(null);

  ngOnChanges() { this.build(); }

  private build() {
    const prefs = this.context?.coursePreferences ?? [];
    const names = new Map(this.lessons.map(x => [x.id.toLowerCase(), x]));
    const rows = prefs.map(pref => {
      const lesson = names.get(pref.lessonId.toLowerCase());
      return { lessonId: pref.lessonId, name: lesson?.name ?? 'Ders', exam: lesson?.examCode ?? '', priority: priorityChoice(pref.priority),
        difficulty: difficultyChoice(pref.selfReportedDifficulty), saved: pref, known: !!lesson };
    }).filter(x => x.known);
    const order: string[] = [];
    for (const row of rows) if (!order.includes(row.exam)) order.push(row.exam);
    this.groups.set(order.map(exam => ({ name: exam || 'Derslerin', rows: rows.filter(x => x.exam === exam) })));
    this.changed.set({});
  }

  showGroupNames() { return this.groups().length > 1; }
  rowPriority(row: CourseRow) { return this.changed()[row.lessonId]?.priority ?? row.priority; }
  rowDifficulty(row: CourseRow) { return this.changed()[row.lessonId]?.difficulty ?? row.difficulty; }
  setPriority(row: CourseRow, value: number) { this.change(row, { priority: value }); }
  setDifficulty(row: CourseRow, value: number) { this.change(row, { difficulty: value }); }
  private change(row: CourseRow, patch: { priority?: number; difficulty?: number }) {
    if (this.staffStudentId) return;
    this.changed.update(all => ({ ...all, [row.lessonId]: { ...all[row.lessonId], ...patch } }));
    this.error.set(''); this.courseError.set(null);
  }
  dirty() {
    const rows = new Map(this.groups().flatMap(g => g.rows).map(r => [r.lessonId, r]));
    return Object.entries(this.changed()).some(([id, c]) => {
      const row = rows.get(id)!;
      return (c.priority !== undefined && c.priority !== row.priority) || (c.difficulty !== undefined && c.difficulty !== row.difficulty);
    });
  }
  busy() { return this.saving(); }

  /** "Devam": saves only when something changed (with the version), then the next step. */
  next() {
    if (this.saving()) return;
    const context = this.context;
    if (this.staffStudentId || !context || !this.dirty()) { this.done.emit(); return; }
    const changes = this.changed();
    const courses = context.coursePreferences.map(course => {
      const c = changes[course.lessonId];
      if (!c) return { ...course };
      const priority = c.priority !== undefined && c.priority !== priorityChoice(course.priority) ? c.priority : course.priority;
      const difficulty = c.difficulty !== undefined && c.difficulty !== difficultyChoice(course.selfReportedDifficulty) ? c.difficulty : course.selfReportedDifficulty;
      return { ...course, priority, selfReportedDifficulty: difficulty };
    });
    this.saving.set(true); this.error.set(''); this.courseError.set(null);
    this.learning.save(learningUpdate(context, courses)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
      next: view => { this.saved.emit(view); this.done.emit(); },
      error: e => {
        const message = typeof e?.error?.message === 'string' ? e.error.message : '';
        if (e?.status === 400 && e?.error?.field === 'courses' && e?.error?.lessonId) { this.courseError.set({ lessonId: e.error.lessonId, message }); return; }
        if (e?.status === 409) { this.error.set('Derslerin başka bir yerde değişti; güncel hâlini yükledim. Seçimlerini yeniden yap.'); this.reload.emit(); return; }
        this.error.set(message || 'Derslerin kaydedilemedi. Yeniden deneyebilirsin.');
      }
    });
  }
  errorFor(row: CourseRow) { const e = this.courseError(); return e && e.lessonId.toLowerCase() === row.lessonId.toLowerCase() ? e.message : null; }
  priorityLabel(value: number) { return this.priorities.find(x => x.value === value)?.label ?? ''; }
  difficultyLabel(value: number) { return this.difficulties.find(x => x.value === value)?.label ?? ''; }
}
