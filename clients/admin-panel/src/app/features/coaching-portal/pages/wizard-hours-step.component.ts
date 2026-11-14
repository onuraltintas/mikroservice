import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { StudyAvailability, StudyDay, StudyWindow } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineAnswers, StudyRoutineExamFocus, StudyRoutineView, StudySchoolShift, StudyTempo, StudyWeekendDays } from '../../../core/services/coaching-study-routine.service';
import { WeeklyProposalWindow } from '../../../core/services/coaching-weekly-draft.service';
import {
  RoutineQuestion, SHIFT_CHOICES, WEEKDAY_CHOICES, WEEKEND_CHOICES, defaultRoutineAnswers, hoursText, minuteChoices, questionForMessage, questionIssue,
  routineQuestions, routineSummary, routineWindows, time, timeChoices, windowLines, withShift
} from './study-routine';
import { Weekday, weekDays } from './weekly-calendar';

type HoursMode = 'summary' | 'availability' | 'questions' | 'staff-questions';

/**
 * Paket H4, step 1 "Saatlerin". A routine that still matches the free hours is summarised ("Doğru, devam" / "Değiştir"); free hours
 * without a matching routine are listed ("Bu saatlerle devam" / "Soruları cevapla"); otherwise the H3 questions are asked, one per
 * screen, and saved with PUT routine. A teacher reads the routine or the free hours, or answers the quick questions for a proposal.
 */
@Component({
  selector: 'app-wizard-hours-step', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './wizard-hours-step.component.html', styleUrl: './student-study-plan-assistant.component.scss'
})
export class WizardHoursStepComponent implements OnInit {
  @Input() staffStudentId: string | null = null;
  @Input() routine: StudyRoutineView | null = null;
  @Input() availability: StudyAvailability | null = null;
  /** "Okula gitmiyorum" is offered to every student but a middle-school one (graduates, open education). */
  @Input() allowNoSchool = true;
  /** YKS: a YKS student starts with three hours a weekday. */
  @Input() yks = false;
  /** Review of PR 135 (S6): the wizard's current exam focus and tempo, kept in the routine when the answers are saved. */
  @Input() examFocus: StudyRoutineExamFocus | null = null;
  @Input() tempo: StudyTempo | null = null;
  /** While the wizard reads the tempo options, the summary's buttons wait. */
  @Input() disabled = false;
  /** K5: the most one day may hold (480, or 600 for a graduate), from the weekly draft page. */
  @Input() limit = 480;
  /** Edit mode and "Saat ekle": the questions open at once, with the saved answers. */
  @Input() startWithQuestions = false;
  @Input() canGoBack = false;
  /**
   * Paket I6 (user decision 14.11.2026), "Saat ekle": the minutes to add to each weekday so that the courses fit. The questions open
   * on the weekday time with the sum chosen (never past the day limit); the student may change it. Nothing is saved until "Devam".
   */
  @Input() addWeekdayMinutes = 0;
  readonly added = signal<{ from: number; to: number; weekendMinutes: number } | null>(null);
  private applyExtra() {
    const extra = this.addWeekdayMinutes, from = this.answers().weekdayMinutes;
    if (!(extra > 0) || from === null) return;
    const to = Math.min(this.limit, from + extra);
    if (to > from) { this.answers.update(a => ({ ...a, weekdayMinutes: to })); this.dirty.set(true); }
    // What the day limit keeps out of the weekdays is named for the weekend (five weekdays' worth).
    this.added.set({ from, to: Math.max(from, to), weekendMinutes: (from + extra - Math.max(from, to)) * 5 });
    const at = this.questions().indexOf('weekday'); if (at >= 0) { this.index.set(at); this.openedAt = at; }
  }
  /** The question "Saat ekle" opened on: "Geri" there leaves the questions (back to the summary) instead of walking the earlier ones. */
  private openedAt = 0;
  /** "Diğer soruları da gör": the questions from the first one (for example to study later in the evening). */
  fromTheStart() { if (this.saving()) return; this.openedAt = 0; this.index.set(0); this.issue.set(null); this.focusQuestion(); }
  showFromTheStart() { return this.openedAt > 0 && this.index() === this.openedAt; }
  addedText() {
    const a = this.added(); if (!a) return '';
    const weekend = a.weekendMinutes > 0 ? ` Kalan yaklaşık ${hoursText(a.weekendMinutes)} için hafta sonuna süre ekleyebilirsin.` : '';
    return a.to > a.from ? `Derslerine yetişmen için günlük süreni ${hoursText(a.from)} yerine ${hoursText(a.to)} olarak seçtim; istersen değiştir.${weekend}`
      : `Hafta içi günlük süren zaten en yüksek değerde.${weekend}`;
  }
  @Output() done = new EventEmitter<WeeklyProposalWindow[] | null>();
  @Output() routineSaved = new EventEmitter<StudyRoutineView>();
  @Output() reload = new EventEmitter<void>();
  @Output() back = new EventEmitter<void>();
  @ViewChild('questionTitle') private questionTitle?: ElementRef<HTMLElement>;
  private readonly routines = inject(CoachingStudyRoutineService);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  readonly mode = signal<HoursMode>('questions');
  /** Where "Geri" on the first question leads (the summary the questions were opened from). */
  private openedFrom: HoursMode | null = null;
  readonly answers = signal<StudyRoutineAnswers>(defaultRoutineAnswers(false));
  readonly index = signal(0);
  readonly questions = computed(() => routineQuestions(this.answers()));
  readonly question = computed(() => this.questions()[Math.min(this.index(), this.questions().length - 1)]);
  readonly issue = signal<{ question: RoutineQuestion; message: string } | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly preview = computed(() => routineWindows(this.answers()));
  readonly shifts = computed(() => SHIFT_CHOICES.filter(x => x.value !== 'NoSchool' || this.allowNoSchool || this.answers().schoolShift === 'NoSchool'));
  readonly weekendChoices = WEEKEND_CHOICES;
  readonly weekdayChoices = WEEKDAY_CHOICES;
  readonly time = time;
  readonly hoursText = hoursText;
  readonly windowLines = windowLines;
  readonly weekDays = weekDays;
  // The teacher's quick questions (never saved for the student).
  readonly staffDays = signal<Weekday[]>([]);
  readonly staffStart = signal('18:00');
  readonly staffMinutes = signal<60 | 90 | 120>(60);
  readonly staffError = signal('');

  ngOnInit() {
    const routine = this.routine, windows = this.availability?.windows ?? [];
    // Review of PR 135 (S7): a routine the student overrode by hand is background only; her saved hours are what counts.
    // Re-review (4): an overridden routine without saved hours shows the empty saved hours, not the routine's windows.
    if (this.staffStudentId) { this.mode.set(routine?.availabilityMatches ? 'summary' : windows.length || routine ? 'availability' : 'staff-questions'); return; }
    if (routine) this.answers.set({ ...defaultRoutineAnswers(this.yks), ...routine.answers, busyDays: [...(routine.answers.busyDays ?? [])] });
    else this.answers.set(defaultRoutineAnswers(this.yks));
    if (this.startWithQuestions) { this.openedFrom = null; this.mode.set('questions'); this.applyExtra(); return; }
    this.mode.set(routine?.availabilityMatches ? 'summary' : windows.length ? 'availability' : 'questions');
  }

  summaryLines() { return this.routine ? routineSummary(this.routine.answers, !!this.staffStudentId) : []; }
  routineWindowLines() { return windowLines(this.routine?.windows ?? []); }
  availabilityLines() { return windowLines(this.availability?.windows ?? []); }
  /** Days with a daily time but no window left (for example a course day that starts too late). */
  emptyDays(days: string[]) { return days.map(day => weekDays.find(x => x.value === day)?.label ?? day).join(', '); }

  /** "Doğru, devam" / "Bu saatlerle devam" (and the teacher's "Devam"). */
  confirm() { if (!this.disabled) this.done.emit(null); }
  /** "Değiştir" / "Soruları cevapla": the questions with the saved answers (or the usual ones). */
  change() { if (this.disabled) return; this.openedFrom = this.mode(); this.index.set(0); this.issue.set(null); this.error.set(''); this.mode.set('questions'); this.focusQuestion(); }

  // --- The questions -------------------------------------------------------------------------------------------------------
  progress() { return `Soru ${this.index() + 1} / ${this.questions().length}`; }
  /** An answer changed and was not saved yet (the wizard asks before leaving). */
  readonly dirty = signal(false);
  /** The current question's refusal, for aria-describedby / aria-invalid on its controls. */
  invalid() { return this.issue()?.question === this.question(); }
  set<K extends keyof StudyRoutineAnswers>(key: K, value: StudyRoutineAnswers[K]) {
    this.answers.update(a => ({ ...a, [key]: value })); this.dirty.set(true);
    if (this.issue()?.question === this.question()) this.issue.set(null);
  }
  setTime(key: 'schoolStart' | 'schoolEnd' | 'afterSchoolStart' | 'latestEnd' | 'morningStart' | 'dayStart' | 'dinnerStart' | 'dinnerEnd' | 'busyStart' | 'weekendStart' | 'weekendEnd', value: string | number) {
    this.set(key, Number(value));
  }
  chooseShift(shift: StudySchoolShift) { this.answers.update(a => withShift(a, shift)); this.dirty.set(true); this.issue.set(null); }
  chooseWeekend(value: StudyWeekendDays) { this.set('weekend', value); }
  toggleBusy(day: StudyDay) {
    const days = new Set(this.answers().busyDays);
    days.has(day) ? days.delete(day) : days.add(day);
    this.set('busyDays', this.weekdayChoices.map(x => x.value).filter(x => days.has(x)));
  }
  choices(field: Parameters<typeof timeChoices>[0]) {
    const list = timeChoices(field, this.answers()), value = this.answers()[field];
    // A saved answer outside today's list (an earlier answer changed) stays visible, so the select never shows a blank.
    return value !== null && !list.includes(value) ? [...list, value].sort((a, b) => a - b) : list;
  }
  minutes() { return minuteChoices(this.limit); }

  /** "Geri" changes the previous answer; on the first question it returns to the summary it was opened from. */
  previous() {
    if (this.saving()) return;
    this.issue.set(null); this.error.set('');
    if (this.index() > this.openedAt) { this.index.update(i => i - 1); this.focusQuestion(); return; }
    if (this.openedFrom) { this.mode.set(this.openedFrom); this.openedFrom = null; this.dirty.set(false); return; }
    this.back.emit();
  }
  canGoBackFromQuestions() { return this.index() > this.openedAt || !!this.openedFrom || this.canGoBack; }
  /** "Devam": the answer is checked as the server would; the last screen saves. */
  next() {
    if (this.saving()) return;
    const q = this.question(), message = questionIssue(q, this.answers(), this.limit);
    if (message) { this.issue.set({ question: q, message }); return; }
    this.issue.set(null);
    if (q === 'review') { this.save(); return; }
    this.index.update(i => Math.min(i + 1, this.questions().length - 1)); this.focusQuestion();
  }
  private save() {
    const routine = this.routine, a = this.answers();
    // The exam focus and tempo chosen in their own steps stay as they are.
    // Review of PR 135 (S6): the wizard's current focus and tempo, never the routine's older answers.
    const answers: StudyRoutineAnswers = { ...a, examFocus: this.examFocus, tempo: this.tempo };
    this.saving.set(true); this.error.set('');
    this.routines.save({ expectedVersion: routine?.version ?? null, expectedAvailabilityVersion: this.availability?.version ?? routine?.currentAvailabilityVersion ?? null, answers })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
        next: view => { this.dirty.set(false); this.routineSaved.emit(view); this.done.emit(null); },
        error: e => {
          const message = typeof e?.error?.message === 'string' ? e.error.message : '';
          if (e?.status === 409) { this.error.set('Çalışma saatlerin başka bir yerde değişti; güncel hâlini yeniden yükledim. Cevaplarını kontrol edip yeniden kaydet.'); this.reload.emit(); return; }
          if (e?.status === 400 && message) {
            // The server's sentence goes next to the question it is about.
            const question = questionForMessage(message, this.answers().schoolShift), at = this.questions().indexOf(question);
            if (at >= 0) { this.index.set(at); this.issue.set({ question, message }); this.focusQuestion(); return; }
          }
          this.error.set(message || 'Çalışma saatlerin kaydedilemedi. Yeniden deneyebilirsin.');
        }
      });
  }
  private focusQuestion() {
    afterNextRender(() => this.questionTitle?.nativeElement.focus(), { injector: this.injector });
  }
  busy() { return this.saving(); }

  // --- A teacher's quick questions (a proposal only) ----------------------------------------------------------------------
  toggleStaffDay(day: Weekday) {
    const days = new Set(this.staffDays());
    days.has(day) ? days.delete(day) : days.add(day);
    this.staffDays.set(this.weekDays.map(x => x.value).filter(x => days.has(x)));
  }
  setStaffMinutes(value: number) { if ([60, 90, 120].includes(value)) this.staffMinutes.set(value as 60 | 90 | 120); }
  staffNext() {
    const [hours, minutes] = this.staffStart().split(':').map(Number), start = hours * 60 + minutes;
    if (!this.staffDays().length) { this.staffError.set('Öğrencinin çalışabileceği en az bir gün seç.'); return; }
    if (!Number.isInteger(start) || start % 30 !== 0 || start + this.staffMinutes() > 1440) {
      this.staffError.set('Başlangıç saati :00 veya :30 olmalı ve çalışma süresi gece yarısını geçmemeli.'); return;
    }
    this.staffError.set('');
    const windows: StudyWindow[] = this.staffDays().map(day => ({ day, startMinute: start, endMinute: start + this.staffMinutes() }));
    this.done.emit(windows);
  }
}
