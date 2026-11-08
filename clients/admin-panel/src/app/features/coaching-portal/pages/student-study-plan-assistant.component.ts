import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyCourseRank } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningContext, RETIRED_PURPOSE_NOTICE, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';
import { DEFAULT_MAX_DAY_MINUTES, clock, slotError, WeeklySlot, Weekday, weekDays } from './weekly-calendar';
import { StudyPlanWizardLesson, createProposalFromWindows, lessonsWithoutTime, noTimeNotice, rankedWizardLessons } from './study-plan-wizard';
import { CoachingStudyPlanningService, StudyAvailability, StudyWindow } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';

@Component({
  selector: 'app-student-study-plan-assistant', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StudentWeeklyOutcomePlanComponent, StudentAutomaticPlanComponent, StudentStudyAvailabilityComponent], templateUrl: './student-study-plan-assistant.component.html',
  styleUrl: './student-study-plan-assistant.component.scss'
})
export class StudentStudyPlanAssistantComponent implements OnInit {
  @Input() staffStudentId: string | null = null;
  @Output() planCreated = new EventEmitter<string>();
  @ViewChild(StudentWeeklyOutcomePlanComponent) private annualPlan?: StudentWeeklyOutcomePlanComponent;
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly learningSetup = inject(CoachingLearningSetupService);
  private readonly planning = inject(CoachingStudyPlanningService);
  @ViewChild(StudentStudyAvailabilityComponent) private hoursEditor?: StudentStudyAvailabilityComponent;
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  @ViewChild('proposalTitle') private proposalTitle?: ElementRef<HTMLElement>;
  @ViewChild('proposeButton') private proposeButton?: ElementRef<HTMLElement>;
  @ViewChild('savedNotice') private savedNotice?: ElementRef<HTMLElement>;
  readonly messages = signal<{ role: 'assistant'; content: string }[]>([{ role: 'assistant', content: 'Merhaba! Çalışma saatlerinden sana haftalık düzen önerisi hazırlayayım; kaydetmeden önce inceleyebilirsin.' }]);
  readonly proposal = signal<WeeklySlot[]>([]);
  /** Paket G #41: the chosen courses left without an hour by the last proposal (and the saved week), the most important first. */
  readonly unplaced = signal<StudyPlanWizardLesson[]>([]);
  readonly noTimeNotice = noTimeNotice;
  private courseRanks: WeeklyCourseRank[] = [];
  /** Review of PR #129 (K5): the student's daily limit from the server (the teacher view reads the student's). */
  private maxDayMinutes = DEFAULT_MAX_DAY_MINUTES;
  readonly selectedDays = signal<Weekday[]>([]);
  readonly startTime = signal('18:00');
  readonly minutesPerDay = signal<30 | 60 | 90 | 120>(60);
  readonly annualReview = signal(false);
  /** E6 #29: the availability saved in Çalışma tercihlerim (one record); the wizard asks only when there is none. */
  readonly availability = signal<StudyAvailability | null>(null);
  readonly availabilityLoading = signal(false);
  /** E6 review: a failed load (not 404) is retried, never answered with the questions (a save would only get a 409). */
  readonly availabilityLoadError = signal('');
  /** Değiştir: the same availability editor as Çalışma tercihlerim, inside the wizard. */
  readonly editingAvailability = signal(false);
  readonly savingAvailability = signal(false);
  /** Roadmap O1: the topic-based plan is part of the one plan wizard. */
  readonly topicPlanOpen = signal(false);
  readonly busy = signal(true);
  readonly error = signal('');
  readonly saved = signal(false);
  readonly lessons = signal<{ id: string; name: string; examCode: string | null; topicCount?: number }[]>([]);
  readonly slots = signal<WeeklySlot[]>([]);
  readonly learningContext = signal<LearningContext | null>(null);
  /** KM8: the saved study purpose (School, General, Undecided) is retired; the student has to choose an exam first. */
  readonly purposeRetired = signal(false);
  /** Paket D review (L6): the saved exam does not fit the grade; topic saves still work, the student is asked to choose again. */
  readonly purposeNotForEducation = signal(false);
  readonly retiredPurposeNotice = RETIRED_PURPOSE_NOTICE;
  /** Paket H1 (P8): the topics marked "Çalıştım" in Başlangıç durumum (edited there, not in the wizard). */
  readonly studiedTopicCount = computed(() => (this.learningContext()?.topicStates ?? []).filter(x => x.declaredStatus === 'Studied').length);
  readonly learningError = signal('');
  /** Review of PR #129: the study preferences were read (a student without them is sent to Çalışma tercihlerim). */
  readonly learningLoaded = signal(false);
  version: number | null = null;
  timeZoneId = 'Europe/Istanbul';
  readonly weekDays = weekDays;
  readonly clock = clock;

  ngOnInit() {
    this.loadAvailability();
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.lessons.set(page.lessons); this.courseRanks = page.courses ?? []; this.maxDayMinutes = page.maxDayMinutes ?? DEFAULT_MAX_DAY_MINUTES;
        this.slots.set(page.draft?.slots ?? []);
        this.version = page.draft?.version ?? null;
        this.timeZoneId = page.draft?.timeZoneId ?? 'Europe/Istanbul';
        this.busy.set(false);
        if (!page.lessons.length) this.error.set(this.staffStudentId ? 'Bu öğrenci henüz eğitim bağlamını tamamlamamış. Önce öğrenci kurulumunu tamamla.' : 'Önce çalışma düzeninden ders seçmelisin.');
      },
      error: () => { this.busy.set(false); this.error.set('Haftalık düzenin yüklenemedi. Yeniden deneyebilirsin.'); }
    });
    (this.staffStudentId ? this.learningSetup.getForStudent(this.staffStudentId) : this.learningSetup.get()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: view => {
        const context = view.context; this.learningContext.set(context); this.learningLoaded.set(true);
        this.purposeRetired.set(!!view.purposeRetired || isRetiredPurpose(context?.purpose));
        this.purposeNotForEducation.set(!!view.purposeNotForEducation);
      },
      error: () => this.learningError.set(this.staffStudentId ? 'Öğrencinin çalışma tercihleri yüklenemedi.' : 'Çalışma tercihlerin yüklenemedi. Sayfayı yenileyip tekrar dene.')
    });
  }

  loadAvailability() {
    this.availabilityLoading.set(true); this.availabilityLoadError.set('');
    this.planning.getAvailability(this.staffStudentId).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.availabilityLoading.set(false))).subscribe({
      next: value => this.availability.set(value ?? null),
      error: e => {
        if (e?.status === 404) { this.availability.set(null); return; }
        this.availabilityLoadError.set(this.staffStudentId ? 'Öğrencinin çalışma saatleri yüklenemedi.' : 'Çalışma saatlerin yüklenemedi.');
      }
    });
  }

  /** E6 #29: the saved study times; empty when the student has not given any yet. */
  savedWindows(): StudyWindow[] { return this.availability()?.windows ?? []; }
  availabilityLines() {
    return [...this.savedWindows()].sort((a, b) => this.dayOrder(a.day) - this.dayOrder(b.day) || a.startMinute - b.startMinute)
      .map(window => `${this.dayName(window.day)} ${clock(window.startMinute)}–${window.endMinute === 1440 ? '24:00' : clock(window.endMinute)}`);
  }
  private dayOrder(day: string) { return this.weekDays.findIndex(x => x.value === day); }
  async changeAvailability() {
    if (this.staffStudentId) return;
    if (this.editingAvailability()) { if (await (this.hoursEditor?.canLeavePage() ?? true)) this.editingAvailability.set(false); return; }
    this.proposal.set([]); this.unplaced.set([]); this.editingAvailability.set(true);
  }
  availabilitySaved() { this.editingAvailability.set(false); this.proposal.set([]); this.unplaced.set([]); this.loadAvailability(); }
  canPropose() { return !this.busy() && !this.savingAvailability() && !this.availabilityLoading() && !this.availabilityLoadError() && !!this.lessons().length && (this.savedWindows().length > 0 || this.selectedDays().length > 0); }

  toggleDay(day: Weekday) {
    const selected = new Set(this.selectedDays());
    selected.has(day) ? selected.delete(day) : selected.add(day);
    this.selectedDays.set(this.weekDays.map(x => x.value).filter(x => selected.has(x)));
    this.saved.set(false);
  }

  setMinutesPerDay(value: number) {
    if ([30, 60, 90, 120].includes(value)) this.minutesPerDay.set(value as 30 | 60 | 90 | 120);
  }

  /**
   * E6 #29: with saved study times the proposal uses them; otherwise the student's answers (days, start, duration)
   * are saved as her availability first — the same record as Çalışma tercihlerim, never a second copy.
   * A teacher's answers only make the proposal; the student's availability is hers.
   */
  createProposal() {
    if (!this.canPropose() || this.editingAvailability()) return;
    if (this.savedWindows().length) { this.propose(this.savedWindows()); return; }
    const [hours, minutes] = this.startTime().split(':').map(Number);
    const startMinute = hours * 60 + minutes;
    if (!Number.isInteger(startMinute) || startMinute % 30 !== 0 || startMinute + this.minutesPerDay() > 1440) {
      this.error.set('Başlangıç saati :00 veya :30 olmalı ve çalışma süresi gece yarısını geçmemeli.'); return;
    }
    const windows: StudyWindow[] = this.selectedDays().map(day => ({ day, startMinute, endMinute: startMinute + this.minutesPerDay() }));
    if (this.staffStudentId) { this.propose(windows); return; }
    this.savingAvailability.set(true); this.error.set('');
    this.planning.saveAvailability({ expectedVersion: this.availability()?.version ?? null, timeZoneId: 'Europe/Istanbul', windows })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.savingAvailability.set(false))).subscribe({
        next: saved => { this.availability.set(saved); this.propose(saved.windows); },
        error: e => {
          this.error.set(`Çalışma saatlerin kaydedilmedi. ${typeof e?.error?.message === 'string' ? e.error.message : 'Yeniden deneyebilirsin.'}`);
          // E6 review: another session saved study times; load them so the next attempt uses the current record.
          if (e?.status === 409) this.loadAvailability();
        }
      });
  }

  private propose(windows: StudyWindow[]) {
    const selected = new Set((this.learningContext()?.coursePreferences ?? []).map(x => x.lessonId.toLowerCase()));
    const lessons = rankedWizardLessons(this.lessons().filter(x => selected.has(x.id.toLowerCase())), this.courseRanks);
    const proposal = createProposalFromWindows(windows, lessons, this.slots(), this.maxDayMinutes);
    // Paket G #41: the courses without time are named in a notice under the proposal (with the way to Müsaitliğim).
    const missingLessons = lessonsWithoutTime(lessons, this.slots(), proposal);
    // Review of PR 126: the courses are named once, in the notice; the chat message only points to it.
    const coverageWarning = missingLessons.length ? ' Saat kalmayan dersleri önerinin altında görebilirsin.' : '';
    const dayCount = new Set(windows.map(window => window.day)).size;
    this.proposal.set(proposal); this.unplaced.set(missingLessons);
    this.error.set(''); this.saved.set(false);
    // Paket F #37: bring the new proposal into view and move the focus to it.
    if (proposal.length) afterNextRender(() => {
      const title = this.proposalTitle?.nativeElement;
      title?.scrollIntoView?.({ block: 'start' }); title?.focus({ preventScroll: true });
    }, { injector: this.injector });
    this.messages.update(items => [...items, { role: 'assistant', content: proposal.length
      ? `${dayCount} gün için ${proposal.length} ders saati önerdim. Mevcut taslağındaki dolu saatleri korudum; öneriyi kaydetmeden önce inceleyebilirsin.${coverageWarning}`
      : `Çalışma saatlerinde boş yer bulamadım. Çalışma saatlerini değiştirip yeniden deneyebilirsin.${coverageWarning}` }]);
  }

  applyProposal() {
    if (this.busy() || !this.proposal().length) return;
    const merged = [...this.slots(), ...this.proposal()];
    for (const slot of this.proposal()) {
      const issue = slotError(slot, merged, { max: this.maxDayMinutes, saved: this.slots(), staff: !!this.staffStudentId });
      if (issue) { this.error.set(issue); return; }
    }
    this.busy.set(true); this.error.set('');
    this.weekly.save(this.version, this.timeZoneId, merged, this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.proposal.set([]); this.saved.set(true); this.focusAfter('saved'); },
      error: e => this.error.set(e?.error?.message ?? 'Haftalık taslak kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }

  /** "Öneriyi kaldır": the proposal goes away and the focus returns to "Haftalık öneri oluştur". */
  removeProposal() { this.proposal.set([]); this.unplaced.set([]); this.focusAfter('propose'); }

  /** Review of PR 122: when the proposal block disappears the focus moves on, not to the page. */
  private focusAfter(target: 'saved' | 'propose') {
    afterNextRender(() => (target === 'saved' ? this.savedNotice : this.proposeButton)?.nativeElement.focus(), { injector: this.injector });
  }

  continueToAnnualReview() {
    if (this.busy() || !this.slots().length || this.proposal().length) return;
    this.annualReview.set(true);
  }

  dayName(day: string) { return this.weekDays.find(x => x.value === day)?.label ?? day; }
  @ViewChild(StudentAutomaticPlanComponent) private topicPlan?: StudentAutomaticPlanComponent;
  async canLeavePage() { return this.annualReview() ? (await this.annualPlan?.canLeavePage() ?? true) : !this.busy() && !this.savingAvailability() && (await this.hoursEditor?.canLeavePage() ?? true) && (await this.topicPlan?.canLeavePage() ?? true); }
  onPlanCreated(id: string) { this.planCreated.emit(id); }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
