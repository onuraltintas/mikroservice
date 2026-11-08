import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, forkJoin, map, of, throwError } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyExamFocus, WeeklyLesson, WeeklyProposalWindow, WeeklyTempoOption } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningContext, LearningSetupView, RETIRED_PURPOSE_NOTICE, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineAnswers, StudyRoutineExamFocus, StudyRoutineView, StudyTempo } from '../../../core/services/coaching-study-routine.service';
import { DEFAULT_MAX_DAY_MINUTES } from './weekly-calendar';
import { WizardHoursStepComponent } from './wizard-hours-step.component';
import { WizardCoursesStepComponent } from './wizard-courses-step.component';
import { WizardSummaryStepComponent, dayMonth } from './wizard-summary-step.component';

export type WizardStep = 'hours' | 'courses' | 'focus' | 'tempo' | 'summary';
const STEP_TITLES: Record<WizardStep, string> = { hours: 'Saatlerin', courses: 'Derslerin', focus: 'Sınav ağırlığı', tempo: 'Tempo', summary: 'Özet' };
const STAFF_STEP_TITLES: Record<WizardStep, string> = { hours: 'Saatleri', courses: 'Dersleri', focus: 'Sınav ağırlığı', tempo: 'Tempo', summary: 'Özet' };
const TO_ROUTINE_FOCUS: Record<WeeklyExamFocus, StudyRoutineExamFocus> = { tyt: 'Tyt', balanced: 'Balanced', ayt: 'Ayt' };
const FROM_ROUTINE_FOCUS: Record<StudyRoutineExamFocus, WeeklyExamFocus> = { Tyt: 'tyt', Balanced: 'balanced', Ayt: 'ayt' };
export const TEMPO_TEXT: Record<StudyTempo, { name: string; meaning: string }> = {
  Relaxed: { name: 'Rahat', meaning: 'Konular sınava kadar yayılır.' },
  Balanced: { name: 'Dengeli', meaning: 'Sona tekrar için zaman kalır.' },
  Intense: { name: 'Yoğun', meaning: 'Konuları erken bitirip tekrara geçersin.' }
};

/**
 * Paket H4: the plan wizard is one flow — 1. Saatlerin, 2. Derslerin, 3. Sınav ağırlığı (YKS), 4. Tempo, 5. Özet — with "Adım 2 / 5"
 * and Geri / Devam on every step. A student with an active plan is first asked what to change (Saatlerim / Derslerim / Tempo, or the
 * whole plan again). The teacher view (staffStudentId) has the same steps: the routine and the courses are read only, and the plan
 * is created as the student's draft, as before.
 */
@Component({
  selector: 'app-student-study-plan-assistant', standalone: true,
  imports: [CommonModule, RouterLink, WizardHoursStepComponent, WizardCoursesStepComponent, WizardSummaryStepComponent],
  templateUrl: './student-study-plan-assistant.component.html', styleUrl: './student-study-plan-assistant.component.scss'
})
export class StudentStudyPlanAssistantComponent implements OnInit {
  @Input() staffStudentId: string | null = null;
  @Output() planCreated = new EventEmitter<string>();
  @ViewChild(WizardHoursStepComponent) private hoursStep?: WizardHoursStepComponent;
  @ViewChild(WizardCoursesStepComponent) private coursesStep?: WizardCoursesStepComponent;
  @ViewChild(WizardSummaryStepComponent) private summaryStep?: WizardSummaryStepComponent;
  @ViewChild('stepTitle') private stepTitle?: ElementRef<HTMLElement>;
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly learningSetup = inject(CoachingLearningSetupService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly routines = inject(CoachingStudyRoutineService);
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);

  // What the wizard reads.
  readonly busy = signal(true);
  readonly error = signal('');
  readonly lessons = signal<WeeklyLesson[]>([]);
  maxDayMinutes = DEFAULT_MAX_DAY_MINUTES;
  readonly learningContext = signal<LearningContext | null>(null);
  readonly learningLoaded = signal(false);
  readonly learningError = signal('');
  readonly purposeRetired = signal(false);
  readonly purposeNotForEducation = signal(false);
  readonly retiredPurposeNotice = RETIRED_PURPOSE_NOTICE;
  readonly availability = signal<StudyAvailability | null>(null);
  readonly routine = signal<StudyRoutineView | null>(null);
  readonly availabilityLoading = signal(false);
  /** E6 review: a failed load (not 404) is retried, never answered with the questions (a save would only get a 409). */
  readonly availabilityLoadError = signal('');
  readonly hasActivePlan = signal(false);
  private hoursLoaded = false;
  /** The summary proposes Fill when the weekly draft has cards, Replace when empty; the tempo options are asked the same way. */
  private draftHasCards = false;
  replaceOnOpen = false;
  private planChecked = false;
  /** Paket H1 (P8): the topics marked "Çalıştım" in Başlangıç durumum (edited there, not in the wizard). */
  readonly studiedTopicCount = computed(() => (this.learningContext()?.topicStates ?? []).filter(x => x.declaredStatus === 'Studied').length);

  // The flow.
  readonly step = signal<WizardStep | 'menu' | null>(null);
  readonly flow = signal<WizardStep[]>([]);
  readonly tempoSkipped = signal(false);
  /** The open step (null on the edit question or while loading). */
  readonly currentStep = computed(() => { const step = this.step(); return step && step !== 'menu' ? step : null; });
  readonly visibleFlow = computed(() => this.flow().filter(x => x !== 'tempo' || !this.tempoSkipped()));
  readonly stepError = signal('');
  readonly savingRoutine = signal(false);
  /** Edit mode "Saatlerim" and the summary's "Saat ekle" open the questions with the saved answers. */
  readonly hoursWithQuestions = signal(false);
  /** "Saat ekle" returns to the summary once the hours are saved. */
  private returnToSummary = false;
  private editing = false;
  /** A teacher's quick answers for a student without study times (sent as windows, never saved). */
  readonly staffWindows = signal<WeeklyProposalWindow[] | null>(null);
  // Step 3 and 4.
  readonly examFocus = signal<WeeklyExamFocus>('balanced');
  readonly examFocusChoices: { value: WeeklyExamFocus; label: string; hint: string }[] = [
    { value: 'tyt', label: 'TYT ağırlıklı', hint: 'TYT derslerine daha çok saat düşer.' },
    { value: 'balanced', label: 'Dengeli', hint: 'TYT ve AYT dersleri ihtiyaca göre paylaşılır.' },
    { value: 'ayt', label: 'AYT ağırlıklı', hint: 'AYT derslerine daha çok saat düşer.' }];
  readonly tempoOptions = signal<WeeklyTempoOption[] | null>(null);
  readonly tempoLoading = signal(false);
  readonly tempoError = signal('');
  readonly tempo = signal<StudyTempo | null>(null);
  readonly tempoText = TEMPO_TEXT;
  readonly dayMonth = dayMonth;
  readonly chosenTempo = computed(() => { const list = this.tempoOptions() ?? []; return list.find(x => x.tempo === this.tempo()) ?? list[0] ?? null; });
  readonly finishBy = computed(() => this.chosenTempo()?.finishBy ?? null);

  ngOnInit() {
    // A teacher is never asked what to change; set before the loads, which may answer at once.
    if (this.staffStudentId) this.planChecked = true;
    this.loadPage();
    this.loadLearning();
    this.loadHours();
    if (!this.staffStudentId) this.planning.list(1, 'Active').pipe(takeUntilDestroyed(this.destroy), map(page => page.items.some(x => x.status === 'Active')), catchError(() => of(false))).subscribe(active => {
      this.hasActivePlan.set(active); this.planChecked = true; this.start();
    });
  }

  private loadPage() {
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.lessons.set(page.lessons); this.draftHasCards = !!page.draft?.slots.length; this.maxDayMinutes = page.maxDayMinutes ?? DEFAULT_MAX_DAY_MINUTES; this.busy.set(false);
        if (!page.lessons.length) this.error.set(this.staffStudentId ? 'Bu öğrenci henüz eğitim bağlamını tamamlamamış. Önce öğrenci kurulumunu tamamla.' : 'Önce çalışma düzeninden ders seçmelisin.');
        this.start();
      },
      error: () => { this.busy.set(false); this.error.set('Haftalık düzenin yüklenemedi. Yeniden deneyebilirsin.'); }
    });
  }
  loadLearning() {
    (this.staffStudentId ? this.learningSetup.getForStudent(this.staffStudentId) : this.learningSetup.get()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: view => { this.applyLearning(view); this.learningLoaded.set(true); this.start(); },
      error: () => this.learningError.set(this.staffStudentId ? 'Öğrencinin çalışma tercihleri yüklenemedi.' : 'Çalışma tercihlerin yüklenemedi. Sayfayı yenileyip tekrar dene.')
    });
  }
  applyLearning(view: LearningSetupView) {
    this.learningContext.set(view.context);
    this.purposeRetired.set(!!view.purposeRetired || isRetiredPurpose(view.context?.purpose));
    this.purposeNotForEducation.set(!!view.purposeNotForEducation);
  }
  /** The free hours and the routine, read together; 404s mean "none yet". */
  loadHours() {
    this.availabilityLoading.set(true); this.availabilityLoadError.set('');
    forkJoin({
      availability: this.planning.getAvailability(this.staffStudentId).pipe(catchError(e => e?.status === 404 ? of(null) : throwError(() => e))),
      routine: this.routines.get(this.staffStudentId)
    }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.availabilityLoading.set(false))).subscribe({
      next: ({ availability, routine }) => {
        this.availability.set(availability ?? null); this.routine.set(routine);
        const focus = routine?.answers.examFocus;
        if (focus && !this.hoursLoaded) this.examFocus.set(FROM_ROUTINE_FOCUS[focus]);
        if (routine?.answers.tempo && !this.hoursLoaded) this.tempo.set(routine.answers.tempo);
        this.hoursLoaded = true; this.start();
      },
      error: () => this.availabilityLoadError.set(this.staffStudentId ? 'Öğrencinin çalışma saatleri yüklenemedi.' : 'Çalışma saatlerin yüklenemedi.')
    });
  }

  isYks() { return this.learningContext()?.purpose === 'Yks'; }
  /** The steps can start: preferences are usable, courses are chosen and the hours were read. */
  ready() { return !this.busy() && !!this.lessons().length && this.learningLoaded() && !!this.learningContext() && !this.purposeRetired() && this.hoursLoaded; }
  private fullFlow(): WizardStep[] { return ['hours', 'courses', ...(this.isYks() ? ['focus' as const] : []), 'tempo', 'summary']; }

  /** Once everything is read: the edit question for a student with an active plan, the summary for ?adim=ozet, else step 1. */
  private start() {
    if (this.step() !== null || !this.ready() || !this.planChecked) return;
    const query = this.route?.snapshot?.queryParamMap;
    // H5's "Yeniden öner" after the free hours changed: the summary proposes the whole week again (after a confirmation).
    this.replaceOnOpen = query?.get('yeniden') === 'oner';
    if (this.replaceOnOpen) this.draftHasCards = false;
    if (query?.get('adim') === 'ozet' || this.replaceOnOpen) { this.flow.set(this.fullFlow()); this.go('summary'); return; }
    if (!this.staffStudentId && this.hasActivePlan()) { this.step.set('menu'); return; }
    this.flow.set(this.fullFlow()); this.go('hours');
  }

  /** "Planında neyi değiştirmek istiyorsun?": each choice opens its own steps with the current values, then the summary. */
  edit(choice: 'hours' | 'courses' | 'tempo' | 'all') {
    this.editing = choice !== 'all';
    if (choice === 'all') { this.flow.set(this.fullFlow()); this.hoursWithQuestions.set(false); this.go('hours'); return; }
    if (choice === 'hours') { this.flow.set(['hours', 'summary']); this.hoursWithQuestions.set(!!this.routine()); this.go('hours'); return; }
    if (choice === 'courses') { this.flow.set(['courses', 'summary']); this.go('courses'); return; }
    this.flow.set([...(this.isYks() ? ['focus' as const] : []), 'tempo', 'summary']); this.go(this.isYks() ? 'focus' : 'tempo');
  }

  stepIndex() { const step = this.step(); return step && step !== 'menu' ? this.visibleFlow().indexOf(step) : -1; }
  progressText() { return `Adım ${this.stepIndex() + 1} / ${this.visibleFlow().length}`; }
  titleOf(step: WizardStep) { return (this.staffStudentId ? STAFF_STEP_TITLES : STEP_TITLES)[step]; }
  canGoBack() { return this.stepIndex() > 0 || (this.editing && !this.staffStudentId); }

  private go(step: WizardStep, direction: 1 | -1 = 1) {
    this.stepError.set('');
    // "Yeniden öner" is asked once, on the first summary.
    if (step !== 'summary') this.replaceOnOpen = false;
    if (step === 'tempo' || (step === 'summary' && !this.tempoOptions())) { this.loadTempo(step, direction); return; }
    this.step.set(step);
    // Paket F #37: the new step takes the focus.
    afterNextRender(() => this.stepTitle?.nativeElement.focus(), { injector: this.injector });
  }
  next() {
    const step = this.step(); if (!step || step === 'menu') return;
    if (step === 'hours' && this.returnToSummary) { this.returnToSummary = false; this.hoursWithQuestions.set(false); this.go('summary'); return; }
    const flow = this.flow(), at = flow.indexOf(step);
    if (at >= 0 && at < flow.length - 1) this.go(flow[at + 1]);
  }
  previous() {
    const step = this.step(); if (!step || step === 'menu') return;
    const flow = this.visibleFlow(), at = flow.indexOf(step);
    if (at > 0) { this.go(flow[at - 1], -1); return; }
    if (this.editing && !this.staffStudentId) { this.editing = false; this.step.set('menu'); }
  }

  // --- Step 1 ---------------------------------------------------------------------------------------------------------------
  hoursDone(windows: WeeklyProposalWindow[] | null) { if (this.staffStudentId) this.staffWindows.set(windows); this.tempoOptions.set(null); this.next(); }
  routineSaved(view: StudyRoutineView) {
    this.routine.set(view);
    // The routine wrote the free hours; they are the routine's windows now.
    this.availability.set({ version: view.currentAvailabilityVersion ?? view.availabilityVersion, timeZoneId: 'Europe/Istanbul', windows: view.windows });
  }

  // --- Step 2 ---------------------------------------------------------------------------------------------------------------
  coursesSaved(view: LearningSetupView) { this.applyLearning(view); this.tempoOptions.set(null); }

  // --- Step 3 (YKS) ---------------------------------------------------------------------------------------------------------
  setExamFocus(value: WeeklyExamFocus) { if (this.examFocus() !== value) { this.examFocus.set(value); this.tempoOptions.set(null); } }
  /** The routine keeps the choice when it still writes the free hours; otherwise it stays on this page (as in H2). */
  focusNext() {
    const routine = this.savableRoutine(), focus = TO_ROUTINE_FOCUS[this.examFocus()];
    if (!routine || routine.answers.examFocus === focus) { this.next(); return; }
    this.saveRoutine({ examFocus: focus });
  }

  // --- Step 4 ---------------------------------------------------------------------------------------------------------------
  /** Tempo options for the current focus and hours; one option skips the step (the summary says so). */
  private loadTempo(target: WizardStep, direction: 1 | -1) {
    this.tempoLoading.set(true); this.tempoError.set(''); this.step.set(target === 'tempo' ? 'tempo' : this.step());
    const focus = this.isYks() ? this.examFocus() : null;
    this.weekly.tempoOptions(focus, this.staffStudentId ?? undefined, this.staffWindows() ?? undefined, this.draftHasCards ? 'Fill' : 'Replace')
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.tempoLoading.set(false))).subscribe({
        next: result => {
          const options = result.options ?? [];
          this.tempoOptions.set(options);
          if (!options.some(x => x.tempo === this.tempo())) this.tempo.set(options[0]?.tempo ?? null);
          this.tempoSkipped.set(options.length <= 1);
          if (target === 'tempo' && options.length <= 1) {
            // Only one tempo fits: on to the summary (or, going back, to the step before the tempo).
            const flow = this.flow(), at = flow.indexOf('tempo');
            if (direction === 1) { this.go('summary'); return; }
            if (at > 0) { this.go(flow[at - 1], -1); return; }
            this.step.set('menu'); return;
          }
          this.step.set(target); afterNextRender(() => this.stepTitle?.nativeElement.focus(), { injector: this.injector });
        },
        error: e => {
          this.tempoError.set(typeof e?.error?.message === 'string' ? e.error.message : 'Tempo seçenekleri hazırlanamadı. Yeniden deneyebilirsin.');
          this.retryTarget = { target, direction }; this.step.set(target);
        }
      });
  }
  private retryTarget: { target: WizardStep; direction: 1 | -1 } | null = null;
  retryTempo() { const r = this.retryTarget ?? { target: 'tempo' as WizardStep, direction: 1 as const }; this.retryTarget = null; this.loadTempo(r.target, r.direction); }
  tempoNote() {
    const only = this.tempoSkipped() ? this.chosenTempo() : null;
    if (!only) return '';
    return `${this.staffStudentId ? 'Öğrencinin sınavına' : 'Sınavına'} kadar tek bir tempo uygun: ${TEMPO_TEXT[only.tempo].name}, bitiş ${dayMonth(only.finishBy)}.`;
  }
  hours(minutes: number) { return (Math.round(minutes / 30) / 2).toLocaleString('tr-TR'); }
  tempoNext() {
    const routine = this.savableRoutine(), chosen = this.tempo();
    if (!routine || !chosen || (routine.answers.tempo ?? 'Relaxed') === chosen) { this.next(); return; }
    this.saveRoutine({ tempo: chosen });
  }

  /** The student's routine when it still writes her free hours (a save would otherwise replace hours she edited by hand). */
  private savableRoutine() { const routine = this.routine(); return !this.staffStudentId && routine?.availabilityMatches ? routine : null; }
  private saveRoutine(patch: Partial<StudyRoutineAnswers>) {
    const routine = this.routine()!;
    this.savingRoutine.set(true); this.stepError.set('');
    this.routines.save({ expectedVersion: routine.version, expectedAvailabilityVersion: routine.currentAvailabilityVersion, answers: { ...routine.answers, ...patch } })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.savingRoutine.set(false))).subscribe({
        next: view => { this.routineSaved(view); this.next(); },
        error: e => {
          const message = typeof e?.error?.message === 'string' ? e.error.message : '';
          if (e?.status === 409) { this.stepError.set(message || 'Günlük düzenin başka bir yerde değişti; yeniden yükledim.'); this.loadHours(); return; }
          this.stepError.set(message || 'Seçimin kaydedilemedi. Yeniden deneyebilirsin.');
        }
      });
  }

  // --- Step 5 ---------------------------------------------------------------------------------------------------------------
  /** "Saat ekle": step 1's change, then straight back to the summary. */
  addHours() {
    if (!this.flow().includes('hours')) this.flow.update(flow => ['hours', ...flow]);
    this.returnToSummary = true; this.hoursWithQuestions.set(!this.staffStudentId); this.go('hours');
  }
  contextSaved(view: LearningSetupView) { this.applyLearning(view); }
  onPlanCreated(id: string) { this.planCreated.emit(id); }

  async canLeavePage() {
    return !this.savingRoutine() && !(this.hoursStep?.busy() ?? false) && !(this.coursesStep?.busy() ?? false) && !(this.summaryStep?.busy() ?? false);
  }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
