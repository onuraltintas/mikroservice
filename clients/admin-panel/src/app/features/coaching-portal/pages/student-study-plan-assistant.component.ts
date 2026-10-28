import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService, LearningCatalog, LearningContext, TopicDeclaration } from '../../../core/services/coaching-learning-setup.service';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';
import { clock, slotError, WeeklySlot, Weekday, weekDays } from './weekly-calendar';
import { createStudyPlanProposal } from './study-plan-wizard';

@Component({
  selector: 'app-student-study-plan-assistant', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StudentWeeklyOutcomePlanComponent, StudentAutomaticPlanComponent], templateUrl: './student-study-plan-assistant.component.html',
  styleUrl: './student-study-plan-assistant.component.scss'
})
export class StudentStudyPlanAssistantComponent implements OnInit {
  @Input() staffStudentId: string | null = null;
  @Output() planCreated = new EventEmitter<string>();
  @ViewChild(StudentWeeklyOutcomePlanComponent) private annualPlan?: StudentWeeklyOutcomePlanComponent;
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly learningSetup = inject(CoachingLearningSetupService);
  private readonly destroy = inject(DestroyRef);
  readonly messages = signal<{ role: 'assistant'; content: string }[]>([{ role: 'assistant', content: 'Merhaba! Birkaç kısa adımda uygun günlerini ve çalışma süreni seçelim. Sana haftalık taslak önerisi hazırlayayım; kaydetmeden önce inceleyebilirsin.' }]);
  readonly proposal = signal<WeeklySlot[]>([]);
  readonly selectedDays = signal<Weekday[]>([]);
  readonly startTime = signal('18:00');
  readonly minutesPerDay = signal<30 | 60 | 90 | 120>(60);
  readonly annualReview = signal(false);
  /** Roadmap O1: the topic-based plan is part of the one plan wizard. */
  readonly topicPlanOpen = signal(false);
  readonly busy = signal(true);
  readonly error = signal('');
  readonly saved = signal(false);
  readonly lessons = signal<{ id: string; name: string; examCode: string | null }[]>([]);
  readonly slots = signal<WeeklySlot[]>([]);
  readonly learningContext = signal<LearningContext | null>(null);
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
        this.topicStatuses.set(new Map((context?.topicStates ?? []).map(x => [x.topicId, x.declaredStatus])));
        if (context) this.loadLearningCatalog();
      },
      error: () => this.topicError.set('Konu durumları yüklenemedi. Çalışma düzeni kurulumunu kontrol et.')
    });
  }

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
      startingStatusReviewed: context.startingStatusReviewed };
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

  createProposal() {
    if (this.busy() || !this.selectedDays().length || !this.lessons().length) return;
    if (this.topicDirty()) { this.topicError.set('Öneri oluşturmadan önce konu durumlarını kaydet.'); return; }
    const [hours, minutes] = this.startTime().split(':').map(Number);
    const startMinute = hours * 60 + minutes;
    const selected = new Set((this.learningContext()?.coursePreferences ?? []).map(x => x.lessonId.toLowerCase()));
    const lessons = this.lessons().filter(x => selected.has(x.id.toLowerCase()));
    const proposal = createStudyPlanProposal({ days: this.selectedDays(), startMinute, minutesPerDay: this.minutesPerDay() }, lessons, this.slots());
    const coveredLessons = new Set([...this.slots(), ...proposal].map(slot => slot.lessonId.toLowerCase()));
    const missingLessons = lessons.filter(lesson => !coveredLessons.has(lesson.id.toLowerCase()));
    const coverageWarning = missingLessons.length
      ? ` ${missingLessons.map(lesson => lesson.name).join(', ')} için henüz ders saati yok. Bu dersleri de plana almak için ek gün veya saat seçip yeni öneri oluştur.`
      : '';
    this.proposal.set(proposal);
    this.error.set(''); this.saved.set(false);
    this.messages.update(items => [...items, { role: 'assistant', content: proposal.length
      ? `${this.selectedDays().length} gün için ${proposal.length} ders saati önerdim. Mevcut taslağındaki dolu saatleri korudum; öneriyi kaydetmeden önce inceleyebilirsin.${coverageWarning}`
      : `Seçtiğin saatlerde boş yer bulamadım. Başlangıç saatini veya günlerini değiştirip yeniden deneyebilirsin.${coverageWarning}` }]);
  }

  applyProposal() {
    if (this.busy() || !this.proposal().length) return;
    const merged = [...this.slots(), ...this.proposal()];
    for (const slot of this.proposal()) {
      const issue = slotError(slot, merged);
      if (issue) { this.error.set(issue); return; }
    }
    this.busy.set(true); this.error.set('');
    this.weekly.save(this.version, this.timeZoneId, merged, this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.proposal.set([]); this.saved.set(true); },
      error: e => this.error.set(e?.error?.message ?? 'Haftalık taslak kaydedilemedi. Yeniden deneyebilirsin.')
    });
  }

  continueToAnnualReview() {
    if (this.busy() || !this.slots().length || this.proposal().length) return;
    if (this.topicDirty()) { this.topicError.set('Yıllık önizlemeye geçmeden önce konu durumlarını kaydet.'); return; }
    this.annualReview.set(true);
  }

  dayName(day: string) { return this.weekDays.find(x => x.value === day)?.label ?? day; }
  ceil(value: number) { return Math.ceil(value); }
  @ViewChild(StudentAutomaticPlanComponent) private topicPlan?: StudentAutomaticPlanComponent;
  async canLeavePage() { return this.annualReview() ? (await this.annualPlan?.canLeavePage() ?? true) : !this.busy() && (await this.topicPlan?.canLeavePage() ?? true); }
  onPlanCreated(id: string) { this.planCreated.emit(id); }

  private readonly feedback = coachingFeedback(undefined, this.error);
}
