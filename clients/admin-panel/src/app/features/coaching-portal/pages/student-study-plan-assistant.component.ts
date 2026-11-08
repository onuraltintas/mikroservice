import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyExamFocus, WeeklyProposal, WeeklyProposalMode } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningCatalog, LearningContext, RETIRED_PURPOSE_NOTICE, TopicDeclaration, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';
import { clock, slotError, WeeklySlot, Weekday, weekDays } from './weekly-calendar';
import { ToasterService } from '../../../core/services/toaster.service';
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
  readonly messages = signal<{ role: 'assistant'; content: string }[]>([{ role: 'assistant', content: 'Merhaba! Çalışma saatlerinden sana haftalık ders düzeni önerisi hazırlayayım; kaydetmeden önce inceleyebilirsin.' }]);
  readonly proposal = signal<WeeklySlot[]>([]);
  /** Paket H2: the server's proposal with each lesson's weekly hours and what the chosen lessons still lack. */
  readonly proposalView = signal<WeeklyProposal | null>(null);
  readonly proposing = signal(false);
  /** Paket H2 (K6): a YKS student's choice for this period; kept here until it has its own place in her preferences. */
  readonly examFocus = signal<WeeklyExamFocus>('balanced');
  readonly examFocusChoices: { value: WeeklyExamFocus; label: string }[] = [
    { value: 'tyt', label: 'TYT ağırlıklı' }, { value: 'balanced', label: 'Dengeli' }, { value: 'ayt', label: 'AYT ağırlıklı' }];
  private readonly toaster = inject(ToasterService);
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
  readonly catalog = signal<LearningCatalog | null>(null);
  readonly topicLessonId = signal('');
  readonly topicLoading = signal(false);
  readonly topicSaving = signal(false);
  readonly topicError = signal('');
  readonly topicSaved = signal(false);
  readonly topicStatuses = signal<Map<string, TopicDeclaration>>(new Map());
  readonly topicPage = signal(1);
  version: number | null = null;
  timeZoneId = 'Europe/Istanbul';
  readonly weekDays = weekDays;
  readonly clock = clock;

  ngOnInit() {
    this.loadAvailability();
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.lessons.set(page.lessons);
        this.slots.set(page.draft?.slots ?? []);
        this.version = page.draft?.version ?? null;
        this.timeZoneId = page.draft?.timeZoneId ?? 'Europe/Istanbul';
        this.busy.set(false);
        if (!page.lessons.length) this.error.set(this.staffStudentId ? 'Bu öğrenci henüz eğitim bağlamını tamamlamamış. Önce öğrenci kurulumunu tamamla.' : 'Önce çalışma düzeninden ders seçmelisin.');
      },
      error: () => { this.busy.set(false); this.error.set('Haftalık ders düzenin yüklenemedi. Yeniden deneyebilirsin.'); }
    });
    (this.staffStudentId ? this.learningSetup.getForStudent(this.staffStudentId) : this.learningSetup.get()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: view => {
        const context = view.context; this.learningContext.set(context);
        this.purposeRetired.set(!!view.purposeRetired || isRetiredPurpose(context?.purpose));
        this.purposeNotForEducation.set(!!view.purposeNotForEducation);
        this.topicStatuses.set(new Map((context?.topicStates ?? []).map(x => [x.topicId, x.declaredStatus])));
        if (context && !this.purposeRetired()) this.loadLearningCatalog();
      },
      error: () => this.topicError.set('Konu durumları yüklenemedi. Çalışma düzeni kurulumunu kontrol et.')
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
    this.clearProposal(); this.editingAvailability.set(true);
  }
  availabilitySaved() { this.editingAvailability.set(false); this.clearProposal(); this.loadAvailability(); }
  /** Paket H2: the proposal is made from saved study times; a teacher cannot give them for the student. */
  canPropose() { return !this.busy() && !this.proposing() && !this.savingAvailability() && !this.availabilityLoading() && !this.availabilityLoadError() && !!this.lessons().length
    && (this.savedWindows().length > 0 || !this.staffStudentId && this.selectedDays().length > 0); }
  isYks() { return this.learningContext()?.purpose === 'Yks'; }

  availableTopicLessons() {
    const context = this.learningContext(); const catalog = this.catalog();
    if (!context || !catalog) return [];
    const selected = new Set(context.coursePreferences.map(x => x.lessonId.toLowerCase()));
    return catalog.lessons.filter(x => selected.has(x.id.toLowerCase()));
  }

  private loadLearningCatalog() {
    this.topicLoading.set(true); this.topicError.set('');
    (this.staffStudentId ? this.learningSetup.catalogForStudent(this.staffStudentId, null, 1) : this.learningSetup.catalog(null, 1)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.topicLoading.set(false))).subscribe({
      next: value => {
        this.catalog.set(value);
        const selected = this.learningContext()?.coursePreferences.find(x => value.lessons.some(l => l.id.toLowerCase() === x.lessonId.toLowerCase()));
        if (selected) { this.topicLessonId.set(selected.lessonId); this.topicLoading.set(false); this.loadTopicCatalog(1); }
      },
      error: () => this.topicError.set('Ders kataloğu yüklenemedi. Çalışma düzeni kurulumunu kontrol et.')
    });
  }

  loadTopicCatalog(page = 1) {
    const lessonId = this.topicLessonId(); if (!lessonId || this.topicLoading()) return;
    this.topicLoading.set(true); this.topicError.set('');
    (this.staffStudentId ? this.learningSetup.catalogForStudent(this.staffStudentId, lessonId, page) : this.learningSetup.catalog(lessonId, page)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.topicLoading.set(false))).subscribe({
      next: value => { this.catalog.set(value); this.topicPage.set(page); },
      error: () => this.topicError.set('Ders konuları yüklenemedi. Tekrar deneyebilirsin.')
    });
  }

  selectTopicLesson(lessonId: string) {
    this.topicLessonId.set(lessonId); this.catalog.set(null); this.topicPage.set(1); this.loadTopicCatalog(1);
  }

  topicStatus(topicId: string): TopicDeclaration | '' { return this.topicStatuses().get(topicId) ?? ''; }
  topicDirty() {
    const saved = this.learningContext()?.topicStates ?? []; const current = this.topicStatuses();
    return saved.length !== current.size || saved.some(x => current.get(x.topicId) !== x.declaredStatus);
  }
  setTopicStatus(topicId: string, status: TopicDeclaration | '') {
    const next = new Map(this.topicStatuses());
    if (status) next.set(topicId, status); else next.delete(topicId);
    this.topicStatuses.set(next); this.topicSaved.set(false); this.topicError.set('');
  }

  saveTopicStatuses() {
    const context = this.learningContext(); if (!context || this.topicSaving()) return;
    const current = new Map(this.topicStatuses());
    const request = { expectedVersion: context.version, academicYearStart: context.academicYearStart, purpose: context.purpose,
      targetExamYear: context.targetExamYear, primaryScoreType: context.primaryScoreType, foreignLanguage: context.foreignLanguage,
      courses: context.coursePreferences, topics: [...current].map(([topicId, declaredStatus]) => ({ topicId, declaredStatus })),
      startingStatusReviewed: context.startingStatusReviewed, targetExamSessionId: context.targetExamSessionId ?? null };
    this.topicSaving.set(true); this.topicError.set('');
    this.learningSetup.save(request).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.topicSaving.set(false))).subscribe({
      next: view => {
        const updated = view.context; this.learningContext.set(updated);
        this.topicStatuses.set(new Map((updated?.topicStates ?? []).map(x => [x.topicId, x.declaredStatus])));
        this.topicSaved.set(true);
      },
      error: e => this.topicError.set(e?.status === 409 ? 'Kurulum başka bir oturumda değişti. Sayfayı yenileyip tekrar dene.' : e?.error?.message ?? 'Konu durumları kaydedilemedi.')
    });
  }

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
   * Paket H2: the proposal itself comes from the server (Fill: the saved cards stay, only free time is used).
   */
  createProposal() {
    if (!this.canPropose() || this.editingAvailability()) return;
    if (this.topicDirty()) { this.topicError.set('Öneri oluşturmadan önce konu durumlarını kaydet.'); return; }
    if (this.savedWindows().length) { this.requestProposal('Fill'); return; }
    if (this.staffStudentId) return;
    const [hours, minutes] = this.startTime().split(':').map(Number);
    const startMinute = hours * 60 + minutes;
    if (!Number.isInteger(startMinute) || startMinute % 30 !== 0 || startMinute + this.minutesPerDay() > 1440) {
      this.error.set('Başlangıç saati :00 veya :30 olmalı ve çalışma süresi gece yarısını geçmemeli.'); return;
    }
    const windows: StudyWindow[] = this.selectedDays().map(day => ({ day, startMinute, endMinute: startMinute + this.minutesPerDay() }));
    this.savingAvailability.set(true); this.error.set('');
    this.planning.saveAvailability({ expectedVersion: this.availability()?.version ?? null, timeZoneId: 'Europe/Istanbul', windows })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.savingAvailability.set(false))).subscribe({
        next: saved => { this.availability.set(saved); this.requestProposal('Fill'); },
        error: e => {
          this.error.set(`Çalışma saatlerin kaydedilmedi. ${typeof e?.error?.message === 'string' ? e.error.message : 'Yeniden deneyebilirsin.'}`);
          // E6 review: another session saved study times; load them so the next attempt uses the current record.
          if (e?.status === 409) this.loadAvailability();
        }
      });
  }

  /** Paket H2: "Haftanı baştan öner": a whole new week; the saved one is replaced only when the proposal is applied. */
  async replaceWeek() {
    if (!this.canPropose() || this.editingAvailability() || !this.slots().length || !this.savedWindows().length) return;
    if (this.topicDirty()) { this.topicError.set('Öneri oluşturmadan önce konu durumlarını kaydet.'); return; }
    if (!await this.toaster.confirm(this.staffStudentId
      ? 'Öğrencinin mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.'
      : 'Mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.')) return;
    this.requestProposal('Replace');
  }

  private requestProposal(mode: WeeklyProposalMode) {
    this.proposing.set(true); this.error.set(''); this.saved.set(false);
    this.weekly.proposal(mode, this.isYks() ? this.examFocus() : null, this.staffStudentId ?? undefined)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.proposing.set(false))).subscribe({
        next: view => this.showProposal(view),
        error: e => this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'Haftalık öneri hazırlanamadı. Yeniden deneyebilirsin.')
      });
  }

  private showProposal(view: WeeklyProposal) {
    this.proposalView.set(view); this.proposal.set(view.slots);
    // Paket F #37: bring the new proposal into view and move the focus to it.
    afterNextRender(() => {
      const title = this.proposalTitle?.nativeElement;
      title?.scrollIntoView?.({ block: 'start' }); title?.focus({ preventScroll: true });
    }, { injector: this.injector });
    const dayCount = new Set(view.slots.map(slot => slot.day)).size;
    const short = view.shortageMinutes > 0 ? (this.staffStudentId ? ' Seçilen saatler öğrencinin bütün derslerine yetmiyor; ayrıntıyı önerinin altında görebilirsin.'
      : ' Saatlerin bütün derslerine yetmiyor; ayrıntıyı önerinin altında görebilirsin.') : '';
    const kept = view.mode === 'Fill' ? ' Mevcut taslağındaki dolu saatleri korudum;' : ' Haftanı baştan hazırladım;';
    this.messages.update(items => [...items, { role: 'assistant', content: view.slots.length
      ? `${dayCount} gün için ${view.slots.length} ders saati önerdim. Her ders bir saat; aralarda mola var.${kept} öneriyi kaydetmeden önce inceleyebilirsin.${short}`
      : `Çalışma saatlerinde boş yer bulamadım. Çalışma saatlerini değiştirip yeniden deneyebilirsin.${short}` }]);
  }

  /** The proposal grouped by day, Monday first. */
  proposalDays() {
    const slots = this.proposal();
    return this.weekDays.map(day => ({ label: day.label, slots: slots.filter(x => x.day === day.value).sort((a, b) => a.startMinute - b.startMinute) }))
      .filter(day => day.slots.length);
  }
  /** "haftada 1,5 saat". */
  hours(minutes: number) { return (Math.round(minutes / 30) / 2).toLocaleString('tr-TR'); }
  plannedMinutes(view: WeeklyProposal) { return view.lessons.reduce((sum, x) => sum + x.existingMinutes + x.proposedMinutes, 0); }
  lessonsWithoutTime(view: WeeklyProposal) { return view.lessons.filter(x => x.noTime).map(x => x.lessonName).join(', '); }

  applyProposal() {
    const view = this.proposalView();
    if (this.busy() || !view || !view.slots.length) return;
    // Fill adds the proposal to the saved cards; Replace saves only the proposal (the student asked for a new week).
    const replace = view.mode === 'Replace';
    const next = replace ? view.slots : [...this.slots(), ...view.slots];
    for (const slot of view.slots) {
      const issue = slotError(slot, next);
      if (issue) { this.error.set(issue); return; }
    }
    this.busy.set(true); this.error.set('');
    this.weekly.save(this.version, this.timeZoneId, next, this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.clearProposal(); this.savedReplaced.set(replace); this.saved.set(true); this.focusAfter('saved'); },
      error: e => this.error.set(e?.error?.message ?? 'Haftalık taslak kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }
  readonly savedReplaced = signal(false);

  /** "Öneriyi kaldır": the proposal goes away and the focus returns to "Haftalık öneri oluştur". */
  removeProposal() { this.clearProposal(); this.focusAfter('propose'); }
  private clearProposal() { this.proposal.set([]); this.proposalView.set(null); }

  /** Review of PR 122: when the proposal block disappears the focus moves on, not to the page. */
  private focusAfter(target: 'saved' | 'propose') {
    afterNextRender(() => (target === 'saved' ? this.savedNotice : this.proposeButton)?.nativeElement.focus(), { injector: this.injector });
  }

  continueToAnnualReview() {
    if (this.busy() || !this.slots().length || this.proposalView()) return;
    if (this.topicDirty()) { this.topicError.set('Yıllık önizlemeye geçmeden önce konu durumlarını kaydet.'); return; }
    this.annualReview.set(true);
  }

  dayName(day: string) { return this.weekDays.find(x => x.value === day)?.label ?? day; }
  ceil(value: number) { return Math.ceil(value); }
  @ViewChild(StudentAutomaticPlanComponent) private topicPlan?: StudentAutomaticPlanComponent;
  async canLeavePage() { return this.annualReview() ? (await this.annualPlan?.canLeavePage() ?? true) : !this.busy() && !this.savingAvailability() && (await this.hoursEditor?.canLeavePage() ?? true) && (await this.topicPlan?.canLeavePage() ?? true); }
  onPlanCreated(id: string) { this.planCreated.emit(id); }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
