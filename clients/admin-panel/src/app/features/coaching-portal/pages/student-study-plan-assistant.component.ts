import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyExamFocus, WeeklyLesson, WeeklyProposalMode, WeeklyProposalWindow, WeeklyTempoOption } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningContext, LearningSetupView, RETIRED_PURPOSE_NOTICE, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineExamFocus, StudyRoutineView, StudyTempo } from '../../../core/services/coaching-study-routine.service';
import { CoachingPlanPreferencesService, SPACED_REVIEW_DAYS, StudyPlanPreferences } from '../../../core/services/coaching-plan-preferences.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { DEFAULT_MAX_DAY_MINUTES } from './weekly-calendar';
import { WizardHoursStepComponent } from './wizard-hours-step.component';
import { WizardCoursesStepComponent } from './wizard-courses-step.component';
import { SummaryChoices, WizardSummaryStepComponent, dayMonth } from './wizard-summary-step.component';

export type WizardStep = 'hours' | 'courses' | 'focus' | 'tempo' | 'summary';
const STEP_TITLES: Record<WizardStep, string> = { hours: 'Saatlerin', courses: 'Derslerin', focus: 'Sınav ağırlığı', tempo: 'Tempo', summary: 'Özet' };
const STAFF_STEP_TITLES: Record<WizardStep, string> = { hours: 'Öğrencinin saatleri', courses: 'Öğrencinin dersleri', focus: 'Sınav ağırlığı', tempo: 'Tempo', summary: 'Özet' };
const TO_ROUTINE_FOCUS: Record<WeeklyExamFocus, StudyRoutineExamFocus> = { tyt: 'Tyt', balanced: 'Balanced', ayt: 'Ayt' };
const FROM_ROUTINE_FOCUS: Record<StudyRoutineExamFocus, WeeklyExamFocus> = { Tyt: 'tyt', Balanced: 'balanced', Ayt: 'ayt' };
export const TEMPO_TEXT: Record<StudyTempo, { name: string; meaning: string }> = {
  Relaxed: { name: 'Rahat', meaning: 'Konular sınava kadar yayılır.' },
  Balanced: { name: 'Dengeli', meaning: 'Sona tekrar için zaman kalır.' },
  Intense: { name: 'Yoğun', meaning: 'Konuları erken bitirip tekrara geçersin.' }
};
const dayNumber = (iso: string) => { const [y, m, d] = iso.split('-').map(Number); return Date.UTC(y, m - 1, d) / 86400000; };

/**
 * Paket H4: the plan wizard is one flow — 1. Saatlerin, 2. Derslerin, 3. Sınav ağırlığı (YKS), 4. Tempo, 5. Özet — with "Adım 2 / 5"
 * and Geri / Devam on every step. A student with an active plan is first asked what to change (Saatlerim / Derslerim / Tempo, or the
 * whole plan again); one with a pending draft is offered that draft first. The teacher view (staffStudentId) has the same steps: the
 * routine and the courses are read only, and the plan is created as the student's draft, as before.
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
  private readonly preferences = inject(CoachingPlanPreferencesService);
  private readonly toaster = inject(ToasterService);
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);

  // What the wizard reads.
  readonly busy = signal(true);
  readonly error = signal('');
  readonly lessons = signal<WeeklyLesson[]>([]);
  maxDayMinutes = DEFAULT_MAX_DAY_MINUTES;
  readonly learningContext = signal<LearningContext | null>(null);
  readonly learningView = signal<LearningSetupView | null>(null);
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
  /** Review of PR 135 (S6): the active plan's end, for the tempo preselected when none is saved. */
  private activePlanEnd: string | null = null;
  /** Review of PR 135 (S15): a draft plan waiting to be published. */
  readonly pendingDraft = signal<{ id: string; title: string } | null>(null);
  private hoursLoaded = false;
  private plansChecked = false;
  private prefsLoaded = false;
  private stored: StudyPlanPreferences = { examFocus: null, tempo: null };
  /** Paket H1 (P8): the topics marked "Çalıştım" in Başlangıç durumum (edited there, not in the wizard). */
  readonly studiedTopicCount = computed(() => (this.learningContext()?.topicStates ?? []).filter(x => x.declaredStatus === 'Studied').length);
  /** "Okula gitmiyorum" for everyone but a middle-school student (the server stays permissive). */
  readonly allowNoSchool = computed(() => {
    const view = this.learningView();
    const middle = view?.educationStatus === 'Student' && view.gradeLevel != null && view.gradeLevel <= 8;
    return !middle && this.learningContext()?.purpose !== 'Lgs';
  });

  // The flow.
  readonly step = signal<WizardStep | 'menu' | 'draft' | null>(null);
  /** The open step (null on the edit question, the pending draft or while loading). */
  readonly currentStep = computed(() => { const step = this.step(); return step && step !== 'menu' && step !== 'draft' ? step : null; });
  readonly flow = signal<WizardStep[]>([]);
  readonly tempoSkipped = signal(false);
  readonly visibleFlow = computed(() => this.flow().filter(x => x !== 'tempo' || !this.tempoSkipped()));
  readonly stepError = signal('');
  readonly savingPreferences = signal(false);
  /** Edit mode "Saatlerim" and the summary's "Saat ekle" open the questions with the saved answers. */
  readonly hoursWithQuestions = signal(false);
  /** "Saat ekle" returns to the summary once the hours are saved (or with Geri). */
  readonly returnToSummary = signal(false);
  /** Re-review (2): "Saat ekle" put the hours step into a flow that had none; it leaves again with the detour. */
  private hoursAdded = false;
  private editing = false;
  /** A teacher's quick answers for a student without study times (sent as windows, never saved). */
  readonly staffWindows = signal<WeeklyProposalWindow[] | null>(null);
  /** Review of PR 135 (S4): the summary's choices, kept while the steps change. */
  readonly summaryChoices = new SummaryChoices();
  /** H5's "Yeniden öner" (?yeniden=oner): the first summary proposes the whole week again, after a confirmation. */
  replaceOnOpen = false;
  // Step 3 and 4.
  readonly examFocus = signal<WeeklyExamFocus>('balanced');
  readonly examFocusChoices: { value: WeeklyExamFocus; label: string; hint: string }[] = [
    { value: 'tyt', label: 'TYT ağırlıklı', hint: 'TYT derslerine daha çok saat düşer.' },
    { value: 'balanced', label: 'Dengeli', hint: 'TYT ve AYT dersleri ihtiyaca göre paylaşılır.' },
    { value: 'ayt', label: 'AYT ağırlıklı', hint: 'AYT derslerine daha çok saat düşer.' }];
  /** Review of PR 135 (S1): the weekly draft has cards (the summary proposes Fill), kept current by the summary. */
  private draftHasCards = false;
  /** The mode the tempo options were read for (the summary's finishBy belongs to it). */
  readonly tempoMode = signal<WeeklyProposalMode>('Fill');
  readonly tempoOptions = signal<WeeklyTempoOption[] | null>(null);
  readonly tempoLoading = signal(false);
  readonly tempoError = signal('');
  /** The saved tempo is no longer offered; the closest one was chosen instead. */
  readonly tempoReplaced = signal(false);
  readonly tempo = signal<StudyTempo | null>(null);
  readonly tempoText = TEMPO_TEXT;
  readonly dayMonth = dayMonth;
  readonly chosenTempo = computed(() => { const list = this.tempoOptions() ?? []; return list.find(x => x.tempo === this.tempo()) ?? list[0] ?? null; });
  readonly finishBy = computed(() => this.chosenTempo()?.finishBy ?? null);

  ngOnInit() {
    // A teacher is never asked what to change; set before the loads, which may answer at once.
    if (this.staffStudentId) this.plansChecked = true;
    this.loadPage();
    this.loadLearning();
    this.loadHours();
    this.preferences.get(this.staffStudentId).pipe(takeUntilDestroyed(this.destroy)).subscribe(prefs => {
      if (prefs) this.stored = { examFocus: prefs.examFocus ?? null, tempo: prefs.tempo ?? null };
      this.storedReviews = prefs?.spacedReviewDays ?? null; this.reviewDays.set(this.storedReviews ?? []);
      this.prefsLoaded = true; this.applyStored(); this.start();
    });
    if (!this.staffStudentId) this.checkPlans();
  }

  /** The active plan (edit mode, its end for the tempo) and a pending draft (offered first). */
  private checkPlans() {
    const none = { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
    forkJoin({
      active: this.planning.list(1, 'Active').pipe(catchError(() => of(none))),
      drafts: this.planning.list(1, 'Draft').pipe(catchError(() => of(none)))
    }).pipe(switchMap(({ active, drafts }) => {
      const plan = active.items.find(x => x.status === 'Active'), draft = drafts.items.find(x => x.status === 'Draft');
      this.hasActivePlan.set(!!plan); this.pendingDraft.set(draft ? { id: draft.id, title: draft.title } : null);
      return plan ? this.planning.get(plan.id).pipe(map(full => full.weeklySource?.endDate ?? null), catchError(() => of(null))) : of(null);
    }), takeUntilDestroyed(this.destroy)).subscribe(end => { this.activePlanEnd = end; this.plansChecked = true; this.start(); });
  }

  private loadPage() {
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.lessons.set(page.lessons); this.draftHasCards = !!page.draft?.slots.length; this.maxDayMinutes = page.maxDayMinutes ?? DEFAULT_MAX_DAY_MINUTES; this.busy.set(false);
        if (!page.lessons.length) this.error.set(this.staffStudentId ? 'Bu öğrenci henüz eğitim bağlamını tamamlamamış. Önce öğrenci kurulumunu tamamla.' : 'Önce çalışma düzeninden ders seçmelisin.');
        this.start();
      },
      error: () => { this.busy.set(false); this.error.set(this.staffStudentId ? 'Öğrencinin haftalık düzeni yüklenemedi. Yeniden deneyebilirsin.' : 'Haftalık düzenin yüklenemedi. Yeniden deneyebilirsin.'); }
    });
  }
  loadLearning() {
    (this.staffStudentId ? this.learningSetup.getForStudent(this.staffStudentId) : this.learningSetup.get()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: view => { this.applyLearning(view); this.learningLoaded.set(true); this.start(); },
      error: () => this.learningError.set(this.staffStudentId ? 'Öğrencinin çalışma tercihleri yüklenemedi.' : 'Çalışma tercihlerin yüklenemedi. Sayfayı yenileyip tekrar dene.')
    });
  }
  applyLearning(view: LearningSetupView) {
    this.learningView.set(view); this.learningContext.set(view.context);
    this.purposeRetired.set(!!view.purposeRetired || isRetiredPurpose(view.context?.purpose));
    this.purposeNotForEducation.set(!!view.purposeNotForEducation);
  }
  /** The free hours and the routine, read together; 404s mean "none yet". */
  loadHours() {
    this.availabilityLoading.set(true); this.availabilityLoadError.set('');
    const before = JSON.stringify(this.availability()?.windows ?? null);
    forkJoin({
      availability: this.planning.getAvailability(this.staffStudentId).pipe(catchError(e => e?.status === 404 ? of(null) : throwError(() => e))),
      routine: this.routines.get(this.staffStudentId)
    }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.availabilityLoading.set(false))).subscribe({
      next: ({ availability, routine }) => {
        const reloaded = this.hoursLoaded;
        this.availability.set(availability ?? null); this.routine.set(routine);
        this.hoursLoaded = true;
        if (!reloaded) { this.applyStored(); this.start(); return; }
        // Review of PR 135 (S14): other hours change the tempo options; the tempo step reads them again.
        if (JSON.stringify(availability?.windows ?? null) !== before) { this.tempoOptions.set(null); if (this.step() === 'tempo') this.loadTempo('tempo', 1); }
      },
      error: () => this.availabilityLoadError.set(this.staffStudentId ? 'Öğrencinin çalışma saatleri yüklenemedi.' : 'Çalışma saatlerin yüklenemedi.')
    });
  }
  /** S6: the saved exam focus and tempo (plan preferences, else the routine's older answers). */
  private applyStored() {
    if (!this.prefsLoaded || !this.hoursLoaded) return;
    const routine = this.routine()?.answers;
    this.stored = { examFocus: this.stored.examFocus ?? routine?.examFocus ?? null, tempo: this.stored.tempo ?? routine?.tempo ?? null };
    if (this.stored.examFocus) this.examFocus.set(FROM_ROUTINE_FOCUS[this.stored.examFocus]);
    if (this.stored.tempo) this.tempo.set(this.stored.tempo);
  }

  isYks() { return this.learningContext()?.purpose === 'Yks'; }
  /** The steps can start: preferences are usable, courses are chosen and the hours were read. */
  ready() {
    return !this.busy() && !!this.lessons().length && this.learningLoaded() && !!this.learningContext() && !this.purposeRetired() && this.hoursLoaded && this.prefsLoaded;
  }
  private fullFlow(): WizardStep[] { return ['hours', 'courses', ...(this.isYks() ? ['focus' as const] : []), 'tempo', 'summary']; }
  private mode(): WeeklyProposalMode { return this.draftHasCards ? 'Fill' : 'Replace'; }

  /** Once everything is read: a pending draft first, then the summary for ?adim=ozet / ?yeniden=oner, the edit question, or step 1. */
  private start(skipDraft = false) {
    if ((this.step() !== null && !skipDraft) || !this.ready() || !this.plansChecked) return;
    if (!skipDraft && !this.staffStudentId && this.pendingDraft()) { this.step.set('draft'); this.focusTitle(); return; }
    const query = this.route?.snapshot?.queryParamMap;
    this.replaceOnOpen = query?.get('yeniden') === 'oner';
    if (query?.get('adim') === 'ozet' || this.replaceOnOpen) { this.flow.set(this.fullFlow()); this.go('summary'); return; }
    if (!this.staffStudentId && this.hasActivePlan()) { this.toMenu(); return; }
    this.flow.set(this.fullFlow()); this.go('hours');
  }
  /** S15: "Yine de yeni plan hazırla". */
  continueWithoutDraft() { this.start(true); }

  /** "Planında neyi değiştirmek istiyorsun?": each choice opens its own steps with the current values, then the summary. */
  edit(choice: 'hours' | 'courses' | 'tempo' | 'all') {
    this.editing = choice !== 'all'; this.endHoursDetour();
    if (choice === 'all') { this.flow.set(this.fullFlow()); this.hoursWithQuestions.set(false); this.go('hours'); return; }
    if (choice === 'hours') { this.flow.set(['hours', 'summary']); this.hoursWithQuestions.set(!!this.routine()); this.go('hours'); return; }
    if (choice === 'courses') { this.flow.set(['courses', 'summary']); this.go('courses'); return; }
    this.flow.set([...(this.isYks() ? ['focus' as const] : []), 'tempo', 'summary']); this.go(this.isYks() ? 'focus' : 'tempo');
  }
  private toMenu() { this.editing = false; this.endHoursDetour(); this.hoursWithQuestions.set(false); this.step.set('menu'); this.focusTitle(); }
  /** The "Saat ekle" detour ends: no return flag, and the hours step leaves a flow it was only added to. */
  /** Paket I6: the minutes "Saat ekle" asks the hours step to add to each weekday (0: none). */
  readonly extraWeekday = signal(0);
  private endHoursDetour() {
    this.returnToSummary.set(false); this.extraWeekday.set(0);
    if (this.hoursAdded) { this.hoursAdded = false; this.flow.update(flow => flow.filter(x => x !== 'hours')); }
  }

  stepIndex() { const step = this.currentStep(); return step ? this.visibleFlow().indexOf(step) : -1; }
  progressText() { const at = this.stepIndex(); return at >= 0 ? `Adım ${at + 1} / ${this.visibleFlow().length}` : ''; }
  titleOf(step: WizardStep) { return (this.staffStudentId ? STAFF_STEP_TITLES : STEP_TITLES)[step]; }
  canGoBack() { return this.stepIndex() > 0 || (this.editing && !this.staffStudentId) || (this.currentStep() === 'hours' && this.returnToSummary()); }

  private go(step: WizardStep, direction: 1 | -1 = 1) {
    this.stepError.set('');
    // "Yeniden öner" is asked once, on the first summary.
    if (step !== 'summary') this.replaceOnOpen = false;
    // The tempo options belong to one proposal mode; read them again when the weekly draft changed.
    if (step === 'tempo' || (step === 'summary' && (!this.tempoOptions() || this.tempoMode() !== this.mode()))) { this.loadTempo(step, direction); return; }
    this.step.set(step); this.focusTitle();
  }
  /** Paket F #37 / review S11: the new step's (or the menu's) heading takes the focus. */
  private focusTitle() { afterNextRender(() => this.stepTitle?.nativeElement.focus(), { injector: this.injector }); }
  next() {
    const step = this.currentStep(); if (!step) return;
    if (step === 'hours' && this.returnToSummary()) { this.endHoursDetour(); this.hoursWithQuestions.set(false); this.go('summary'); return; }
    const flow = this.flow(), at = flow.indexOf(step);
    if (at >= 0 && at < flow.length - 1) this.go(flow[at + 1]);
  }
  previous() {
    const step = this.currentStep(); if (!step) return;
    // S3: "Saat ekle" can be left with Geri, straight back to the summary.
    if (step === 'hours' && this.returnToSummary()) { this.endHoursDetour(); this.hoursWithQuestions.set(false); this.go('summary', -1); return; }
    const flow = this.visibleFlow(), at = flow.indexOf(step);
    if (at > 0) { this.go(flow[at - 1], -1); return; }
    if (this.editing && !this.staffStudentId) this.toMenu();
  }

  // --- Step 1 ---------------------------------------------------------------------------------------------------------------
  hoursDone(windows: WeeklyProposalWindow[] | null) { if (this.staffStudentId) this.staffWindows.set(windows); this.tempoOptions.set(null); this.next(); }
  routineSaved(view: StudyRoutineView) {
    this.routine.set(view);
    // Contract item 6: PUT routine saved its focus and tempo as the plan preferences.
    this.stored = { examFocus: view.answers.examFocus ?? null, tempo: view.answers.tempo ?? null };
    // The routine wrote the free hours; they are the routine's windows now (in the free hours' own time zone).
    this.availability.set({ version: view.currentAvailabilityVersion ?? view.availabilityVersion, timeZoneId: this.availability()?.timeZoneId ?? 'Europe/Istanbul', windows: view.windows });
  }

  // --- Step 2 ---------------------------------------------------------------------------------------------------------------
  coursesSaved(view: LearningSetupView) { this.applyLearning(view); this.tempoOptions.set(null); }

  toRoutineFocus() { return TO_ROUTINE_FOCUS[this.examFocus()]; }
  /** Re-review (5): only what the student confirmed (or saved before) goes with a routine save; null keeps the saved value. */
  confirmedFocus() { return this.isYks() ? this.stored.examFocus : null; }
  confirmedTempo() { return this.stored.tempo; }
  // --- Step 3 (YKS) ---------------------------------------------------------------------------------------------------------
  setExamFocus(value: WeeklyExamFocus) { if (this.examFocus() !== value) { this.examFocus.set(value); this.tempoOptions.set(null); } }
  /** S6: the confirmed choice is kept in the plan preferences (never through the routine, so the free hours stay as they are). */
  focusNext() { this.savePreferences({ examFocus: TO_ROUTINE_FOCUS[this.examFocus()] }); }

  // --- Spaced reviews (Paket I5, user decision 14.11.2026) -------------------------------------------------------------------
  /** The student's own choice: any of 7, 30 and 45 days after a finished topic, or none. Nothing is chosen for her. */
  readonly reviewChoices = SPACED_REVIEW_DAYS;
  readonly reviewDays = signal<number[]>([]);
  /** Null while the student has never answered. */
  private storedReviews: number[] | null = null;
  toggleReview(day: number) { this.reviewDays.update(days => (days.includes(day) ? days.filter(x => x !== day) : [...days, day]).sort((a, b) => a - b)); }
  reviewText() {
    const days = this.reviewDays();
    if (!days.length) return this.staffStudentId ? 'Öğrenci aralıklı tekrar seçmedi; plana tekrar konmaz.' : 'Hiçbirini seçmezsen plana tekrar konmaz.';
    const list = days.length === 1 ? `${days[0]}` : `${days.slice(0, -1).join(', ')} ve ${days[days.length - 1]}`;
    return `${this.staffStudentId ? 'Öğrencinin bitirdiği' : 'Bitirdiğin'} her konu ${list} gün sonra 10 dakikalık kısa bir tekrarla hatırlatılır.`;
  }
  /**
   * A student's choice is saved as she makes it (also "none", so that she is not asked as if for the first time); the weekly need
   * changes with it, so the tempo options are read again. A teacher only reads the choice.
   */
  readonly savingReviews = signal(false);
  readonly reviewError = signal('');
  setReview(day: number) {
    if (this.staffStudentId || this.savingReviews()) return;
    const before = this.reviewDays(); this.toggleReview(day);
    const days = this.reviewDays(); this.savingReviews.set(true); this.reviewError.set('');
    this.preferences.saveSpacedReviews(days).pipe(catchError(e => e?.status === 409 && e?.error?.code === 'StudyPlanning.Conflict' ? this.preferences.saveSpacedReviews(days) : throwError(() => e)),
      takeUntilDestroyed(this.destroy), finalize(() => this.savingReviews.set(false))).subscribe({
      next: view => {
        this.storedReviews = view.spacedReviewDays ?? days; this.reviewDays.set(this.storedReviews); this.tempoOptions.set(null);
        if (this.step() === 'tempo') this.loadTempo('tempo', 1);
      },
      error: e => { this.reviewDays.set(before); this.reviewError.set(typeof e?.error?.message === 'string' ? e.error.message : 'Tekrar seçimin kaydedilemedi. Yeniden deneyebilirsin.'); }
    });
  }

  // --- Step 4 ---------------------------------------------------------------------------------------------------------------
  /** Tempo options for the current focus, hours and proposal mode; one option skips the step (the summary says so). */
  private loadTempo(target: WizardStep, direction: 1 | -1) {
    this.tempoLoading.set(true); this.tempoError.set('');
    if (target === 'tempo') this.step.set('tempo');
    const focus = this.isYks() ? this.examFocus() : null, mode = this.mode();
    this.weekly.tempoOptions(focus, this.staffStudentId ?? undefined, this.staffWindows() ?? undefined, mode)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.tempoLoading.set(false))).subscribe({
        next: result => {
          const options = result.options ?? [];
          this.tempoOptions.set(options); this.tempoMode.set(mode);
          if (!options.some(x => x.tempo === this.tempo())) this.tempo.set(this.preselect(options));
          this.tempoSkipped.set(options.length <= 1);
          if (target === 'tempo' && options.length <= 1) {
            // Only one tempo fits: on to the summary (or, going back, to the step before the tempo).
            const flow = this.flow(), at = flow.indexOf('tempo');
            if (direction === 1) { this.go('summary'); return; }
            if (at > 0) { this.go(flow[at - 1], -1); return; }
            this.toMenu(); return;
          }
          this.step.set(target); this.focusTitle();
        },
        error: e => {
          // S5: the error stays where the student is; the summary is not opened (or proposed) without a tempo.
          this.tempoError.set(typeof e?.error?.message === 'string' ? e.error.message : 'Tempo seçenekleri hazırlanamadı. Yeniden deneyebilirsin.');
          this.retryTarget = { target, direction };
        }
      });
  }
  private retryTarget: { target: WizardStep; direction: 1 | -1 } | null = null;
  retryTempo() { const r = this.retryTarget ?? { target: 'tempo' as WizardStep, direction: 1 as const }; this.retryTarget = null; this.loadTempo(r.target, r.direction); }
  /**
   * S6: the saved tempo, else the one ending closest to the active plan's end, else the most relaxed. A saved tempo the server no
   * longer offers (its date does not settle) falls back the same way, and the tempo step says so.
   */
  private preselect(options: WeeklyTempoOption[]): StudyTempo | null {
    this.tempoReplaced.set(false);
    if (this.stored.tempo && options.some(x => x.tempo === this.stored.tempo)) return this.stored.tempo;
    if (this.stored.tempo && options.length) this.tempoReplaced.set(true);
    const end = this.activePlanEnd;
    if (end && options.length) return [...options].sort((a, b) => Math.abs(dayNumber(a.finishBy) - dayNumber(end)) - Math.abs(dayNumber(b.finishBy) - dayNumber(end)))[0].tempo;
    return options[0]?.tempo ?? null;
  }
  tempoNote() {
    const only = this.tempoSkipped() ? this.chosenTempo() : null;
    if (!only) return '';
    return `${this.staffStudentId ? 'Öğrencinin sınavına' : 'Sınavına'} kadar tek bir tempo uygun: ${TEMPO_TEXT[only.tempo].name}, bitiş ${dayMonth(only.finishBy)}.`;
  }
  hours(minutes: number) { return (Math.round(minutes / 30) / 2).toLocaleString('tr-TR'); }
  /** Review of PR 135 (S9): why a tempo does not fit, plainly. */
  fitText(option: WeeklyTempoOption) {
    const staff = !!this.staffStudentId;
    if (option.neededWeeklyMinutes <= option.capacityWeeklyMinutes)
      return staff ? 'Öğrencinin saatleri günlere dağılınca bu tempoya yetmiyor.' : 'Saatlerin günlere dağılınca bu tempoya yetmiyor.';
    return staff ? `Öğrencinin haftada ${this.hours(option.capacityWeeklyMinutes)} saati var; bu tempo ${this.hours(option.neededWeeklyMinutes)} saat istiyor.`
      : `Haftada ${this.hours(option.capacityWeeklyMinutes)} saatin var; bu tempo ${this.hours(option.neededWeeklyMinutes)} saat istiyor.`;
  }
  tempoNext() { const chosen = this.tempo(); if (chosen) this.savePreferences({ tempo: chosen }); }

  /**
   * S6: a student's confirmed focus or tempo goes to the plan preferences; a teacher's stays on this page. PUT replaces both values,
   * so the other one is sent as it is now.
   */
  private savePreferences(patch: Partial<StudyPlanPreferences>) {
    const body = { examFocus: this.isYks() ? this.stored.examFocus : null, tempo: this.stored.tempo, ...patch };
    if (this.staffStudentId || (body.examFocus === this.stored.examFocus && body.tempo === this.stored.tempo)) { this.next(); return; }
    this.savingPreferences.set(true); this.stepError.set('');
    const update = { examFocus: body.examFocus ?? null, tempo: body.tempo ?? null };
    // A first save can race with another one (409 StudyPlanning.Conflict); it is tried once more.
    this.preferences.save(update).pipe(catchError(e => e?.status === 409 && e?.error?.code === 'StudyPlanning.Conflict' ? this.preferences.save(update) : throwError(() => e)),
      takeUntilDestroyed(this.destroy), finalize(() => this.savingPreferences.set(false))).subscribe({
      next: saved => { this.stored = { examFocus: saved.examFocus ?? null, tempo: saved.tempo ?? null }; this.next(); },
      error: e => this.stepError.set(typeof e?.error?.message === 'string' ? e.error.message : 'Seçimin kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }

  // --- Step 5 ---------------------------------------------------------------------------------------------------------------
  /** "Saat ekle": step 1's change, then straight back to the summary (Geri returns there too). */
  addHours(extraWeekdayMinutes = 0) {
    this.extraWeekday.set(Math.max(0, extraWeekdayMinutes || 0));
    if (!this.flow().includes('hours')) { this.hoursAdded = true; this.flow.update(flow => ['hours', ...flow]); }
    this.returnToSummary.set(true); this.hoursWithQuestions.set(!this.staffStudentId); this.go('hours');
  }
  /** Paket I5, "Tekrar seçimini değiştir": the step that always shows the review choice, then back to the summary. */
  changeReviews() { this.endHoursDetour(); this.editing = true; this.flow.set(['courses', 'summary']); this.go('courses'); }
  /** S1: the summary saved (or reloaded) the weekly draft; the next tempo options follow its mode. */
  draftSaved(hasCards: boolean) { this.draftHasCards = hasCards; }
  contextSaved(view: LearningSetupView) { this.applyLearning(view); }
  onPlanCreated(id: string) { this.planCreated.emit(id); }

  /** Unsaved answers, course choices or chosen topics are not dropped without asking. */
  async canLeavePage() {
    // Re-review (7): only a write keeps the student here; the summary's reads are dropped when it goes away.
    if (this.savingPreferences() || (this.hoursStep?.busy() ?? false) || (this.coursesStep?.busy() ?? false) || (this.summaryStep?.writing() ?? false)) return false;
    const unsaved = (this.hoursStep?.dirty() ?? false) || (this.coursesStep?.dirty() ?? false) || this.summaryChoices.focusTopics().length > 0;
    return !unsaved || await this.toaster.confirm('Sihirbazda kaydetmediğin seçimler var. Çıkarsan kaybolur. Çıkmak istiyor musun?', { confirmText: 'Çık', cancelText: 'Vazgeç' });
  }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
