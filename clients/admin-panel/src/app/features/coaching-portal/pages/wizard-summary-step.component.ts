import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
import { CoachingLearningSetupService, LearningContext, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, StudyTopic } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyDraftService, WeeklyExamFocus, WeeklyProposal, WeeklyProposalMode, WeeklyProposalWindow } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingWeeklyOutcomePlanService, WeeklyOutcomeOptions, WeeklyOutcomePreview, WeeklyOutcomeRequest } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { learningUpdate } from './wizard-courses-step.component';
import { DEFAULT_MAX_DAY_MINUTES, WeeklySlot, clock, slotError, weekDays } from './weekly-calendar';

/** Paket H1 (P10): the server's code for weekly lessons that are no longer chosen. */
const LESSONS_NOT_CHOSEN = 'StudyPlanning.LessonsNotChosen';
/** Paket H4 (K3): the most topics the student may move forward at once (the server refuses more). */
export const MAX_FOCUS_TOPICS = 50;
export const PLAN_TITLE = 'Haftalık derslerimden çalışma planım';
const longDate = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', timeZone: 'UTC' });
/** "12 Mayıs". */
export function dayMonth(iso: string | null | undefined) { if (!iso) return ''; const [y, m, d] = iso.split('-').map(Number); return longDate.format(new Date(Date.UTC(y, m - 1, d))); }

interface FocusTopic { topicId: string; topicName: string; lessonName: string }
interface PublishIssue { message: string; draftExists?: boolean; stale?: boolean; createdId?: string }

/**
 * Paket H4, step 5 "Özet". The server proposes the week (Fill when the weekly draft has cards, Replace when it is empty, with the
 * tempo's finishBy); once the student keeps it, the yearly preview runs (engine 2, freed hours reused, ending on finishBy) and shows
 * per course when it finishes, its weekly hours, the spaced reviews and the topics left out, with one-click fixes. "Planımı yayımla"
 * creates the plan from the checked preview and publishes it; a teacher creates the draft only, as before.
 */
@Component({
  selector: 'app-wizard-summary-step', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './wizard-summary-step.component.html', styleUrl: './student-study-plan-assistant.component.scss'
})
export class WizardSummaryStepComponent implements OnInit {
  @Input() staffStudentId: string | null = null;
  @Input() examFocus: WeeklyExamFocus | null = null;
  @Input() finishBy: string | null = null;
  /** A teacher's quick answers for a student without study times (a proposal only, never saved). */
  @Input() windows: WeeklyProposalWindow[] | null = null;
  /** "Sınavına kadar tek tempo var…" when the tempo step was skipped. */
  @Input() tempoNote = '';
  @Input() context: LearningContext | null = null;
  @Input() canGoBack = true;
  @Output() addHours = new EventEmitter<void>();
  @Output() planCreated = new EventEmitter<string>();
  @Output() contextSaved = new EventEmitter<LearningSetupView>();
  @Output() back = new EventEmitter<void>();
  @ViewChild('proposalTitle') private proposalTitle?: ElementRef<HTMLElement>;
  @ViewChild('previewTitle') private previewTitle?: ElementRef<HTMLElement>;
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly engine = inject(CoachingWeeklyOutcomePlanService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly learning = inject(CoachingLearningSetupService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly injector = inject(Injector);
  readonly clock = clock;
  readonly dayMonth = dayMonth;
  // The weekly draft.
  readonly slots = signal<WeeklySlot[]>([]);
  private version: number | null = null;
  private timeZoneId = 'Europe/Istanbul';
  private maxDayMinutes = DEFAULT_MAX_DAY_MINUTES;
  /** Fill when the draft had cards as the step opened, Replace when it was empty; "Bu dersi öne al" proposes the same way again. */
  private initialMode: WeeklyProposalMode = 'Replace';
  readonly loading = signal(true);
  readonly proposing = signal(false);
  readonly applying = signal(false);
  readonly proposal = signal<WeeklyProposal | null>(null);
  readonly error = signal('');
  // The yearly preview.
  readonly options = signal<WeeklyOutcomeOptions | null>(null);
  readonly preview = signal<WeeklyOutcomePreview | null>(null);
  readonly previewing = signal(false);
  readonly previewError = signal('');
  readonly notChosen = signal<{ lessonId: string; lessonName: string }[]>([]);
  readonly prioritizeWhenShort = signal(false);
  readonly savingPriority = signal<string | null>(null);
  // "Şu konuları öne al".
  readonly focusTopics = signal<FocusTopic[]>([]);
  readonly pickerOpen = signal(false);
  readonly pickerLesson = signal('');
  readonly pickerSearch = signal('');
  readonly pickerTopics = signal<StudyTopic[]>([]);
  readonly pickerTotal = signal(0);
  readonly pickerLoading = signal(false);
  readonly pickerError = signal('');
  private pickerPage = 1;
  readonly filteredTopics = computed(() => {
    const term = this.pickerSearch().trim().toLocaleLowerCase('tr-TR');
    return this.pickerTopics().filter(x => !term || x.name.toLocaleLowerCase('tr-TR').includes(term) || (x.unitName ?? '').toLocaleLowerCase('tr-TR').includes(term));
  });
  // Publishing.
  readonly publishing = signal(false);
  readonly publishIssue = signal<PublishIssue | null>(null);

  ngOnInit() { this.loadDraft(true); }

  busy() { return this.loading() || this.proposing() || this.applying() || this.previewing() || this.publishing() || !!this.savingPriority(); }

  private loadDraft(propose: boolean) {
    this.loading.set(true); this.error.set('');
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false))).subscribe({
      next: page => {
        this.slots.set(page.draft?.slots ?? []); this.version = page.draft?.version ?? null;
        this.timeZoneId = page.draft?.timeZoneId ?? 'Europe/Istanbul'; this.maxDayMinutes = page.maxDayMinutes ?? DEFAULT_MAX_DAY_MINUTES;
        if (propose) { this.initialMode = this.slots().length ? 'Fill' : 'Replace'; this.propose(this.initialMode); }
      },
      error: () => this.error.set(this.staffStudentId ? 'Öğrencinin haftalık düzeni yüklenemedi. Yeniden deneyebilirsin.' : 'Haftalık düzenin yüklenemedi. Yeniden deneyebilirsin.')
    });
  }
  retry() { this.publishIssue.set(null); this.preview.set(null); this.proposal.set(null); this.loadDraft(true); }

  /** The server's proposal; an empty one with saved cards goes straight on to the yearly preview. */
  propose(mode: WeeklyProposalMode) {
    this.proposing.set(true); this.error.set(''); this.preview.set(null); this.publishIssue.set(null);
    this.weekly.proposal(mode, this.examFocus, this.staffStudentId ?? undefined, this.windows ?? undefined, this.finishBy)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.proposing.set(false))).subscribe({
        next: view => {
          this.proposal.set(view);
          if (!view.slots.length && this.slots().length) { this.runPreview(); return; }
          // Paket F #37: the new content takes the focus.
          this.focus(() => this.proposalTitle);
        },
        error: e => this.error.set(typeof e?.error?.message === 'string' ? e.error.message : 'Haftalık öneri hazırlanamadı. Yeniden deneyebilirsin.')
      });
  }
  /** "Haftanı baştan öner": a whole new week; the saved one is replaced only when it is kept. */
  async replaceWeek() {
    if (this.busy() || !this.slots().length) return;
    if (!await this.toaster.confirm(this.staffStudentId
      ? 'Öğrencinin mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.'
      : 'Mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.')) return;
    this.propose('Replace');
  }

  /** The week the summary shows: the saved cards (kept in Fill mode) and the proposed ones, day by day. */
  weekView() {
    const view = this.proposal(), saved = view?.mode === 'Replace' ? [] : this.slots(), proposed = view?.slots ?? [];
    const all = [...saved.map(slot => ({ slot, isNew: false })), ...proposed.map(slot => ({ slot, isNew: true }))];
    return weekDays.map(day => ({ label: day.label, items: all.filter(x => x.slot.day === day.value).sort((a, b) => a.slot.startMinute - b.slot.startMinute) }))
      .filter(day => day.items.length);
  }
  hours(minutes: number) { return (Math.round(minutes / 30) / 2).toLocaleString('tr-TR'); }
  /** What the saved week covers of each course's need, before the proposal is kept. */
  savedCoveredMinutes(view: WeeklyProposal) {
    return view.lessons.reduce((sum, lesson) => sum + Math.min(lesson.neededMinutes, this.slots()
      .filter(slot => slot.lessonId.toLowerCase() === lesson.lessonId.toLowerCase()).reduce((total, slot) => total + slot.durationMinutes, 0)), 0);
  }
  lessonsWithoutTime(view: WeeklyProposal) { return view.lessons.filter(x => x.noTime).map(x => x.lessonName).join(', '); }
  emptyText(view: WeeklyProposal) {
    const staff = !!this.staffStudentId;
    if (!view.lessons.length) return staff ? 'Öğrenci henüz çalışacağı dersleri seçmemiş; öneri seçilen derslere göre hazırlanır.' : 'Önce Derslerim\'de çalışacağın dersleri seç; öneri seçtiğin derslere göre hazırlanır.';
    if (view.shortageMinutes === 0) return staff ? 'Öğrencinin haftalık düzeni derslerine yetiyor; eklenecek saat yok.' : 'Haftalık düzenin derslerine yetiyor; eklenecek saat yok.';
    return staff ? 'Öğrencinin çalışma saatlerinde boş yer bulamadım.' : 'Çalışma saatlerinde boş yer bulamadım. Saat ekleyip yeniden deneyebilirsin.';
  }
  /** The week can go on to the yearly preview: new cards to keep, or saved cards without new ones. */
  canKeepWeek() { const view = this.proposal(); return !!view && (view.slots.length > 0 || this.slots().length > 0); }

  /** "Bu haftayla devam et": the proposal is saved into the weekly draft (H2 version check, H1 day limits), then the yearly preview. */
  keepWeek() {
    const view = this.proposal();
    if (this.busy() || !view) return;
    if (!view.slots.length) { if (this.slots().length) this.runPreview(); return; }
    if ((view.draftVersion ?? null) !== this.version) { this.draftChanged(); return; }
    const replace = view.mode === 'Replace', next = replace ? view.slots : [...this.slots(), ...view.slots];
    for (const slot of view.slots) {
      const issue = slotError(slot, next, { max: this.maxDayMinutes, saved: replace ? [] : this.slots(), staff: !!this.staffStudentId });
      if (issue) { this.error.set(issue); return; }
    }
    this.applying.set(true); this.error.set('');
    this.weekly.save(this.version, this.timeZoneId, next, this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.applying.set(false))).subscribe({
      next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.proposal.set({ ...view, mode: 'Fill', slots: [] }); this.runPreview(); },
      error: e => { if (e?.status === 409) this.draftChanged(); else this.error.set(e?.error?.message ?? 'Haftalık düzen kaydedilemedi. Yeniden deneyebilirsin.'); }
    });
  }
  private draftChanged() {
    this.proposal.set(null);
    this.error.set(this.staffStudentId ? 'Öğrencinin haftalık düzeni başka bir yerde değişti; öneriyi yeniledim.' : 'Haftalık düzenin başka bir yerde değişti; öneriyi yeniledim.');
    const message = this.error();
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.slots.set(page.draft?.slots ?? []); this.version = page.draft?.version ?? null; this.timeZoneId = page.draft?.timeZoneId ?? 'Europe/Istanbul';
        this.propose(this.slots().length ? 'Fill' : 'Replace'); this.error.set(message);
      }
    });
  }

  // --- The yearly preview -----------------------------------------------------------------------------------------------------
  private request(options: WeeklyOutcomeOptions): WeeklyOutcomeRequest {
    // The tempo's finish date ends the plan; without one (or before the start) the plan ends on the server's default.
    const end = this.finishBy && this.finishBy >= options.startDate ? this.finishBy : options.endDate;
    const topics = this.focusTopics().map(x => x.topicId);
    return { startDate: options.startDate, endDate: end, expectedWeeklyDraftVersion: options.weeklyDraftVersion, reuseFreedSlots: true, engineVersion: 2,
      prioritizeWhenShort: this.prioritizeWhenShort(), ...(topics.length ? { focusTopicIds: topics } : {}) };
  }
  runPreview() {
    const student = this.staffStudentId ?? undefined;
    this.previewing.set(true); this.previewError.set(''); this.notChosen.set([]); this.preview.set(null); this.publishIssue.set(null);
    this.engine.options(student).pipe(takeUntilDestroyed(this.destroy), switchMap(options => { this.options.set(options); return this.engine.preview(this.request(options), student); }),
      finalize(() => this.previewing.set(false))).subscribe({
      next: preview => { this.preview.set(preview); this.focus(() => this.previewTitle); },
      error: e => {
        this.notChosen.set(e?.status === 409 && e?.error?.code === LESSONS_NOT_CHOSEN ? e.error.lessons ?? [] : []);
        this.previewError.set(typeof e?.error?.message === 'string' ? e.error.message : 'Yıllık plan hesaplanamadı. Yeniden deneyebilirsin.');
      }
    });
  }
  /** Review of PR 129: lessons no longer chosen leave the weekly draft after a confirmation, then the preview runs again. */
  async removeNotChosen() {
    const lessons = this.notChosen(); if (!lessons.length || this.busy()) return;
    const names = lessons.map(x => x.lessonName), list = names.length > 1 ? `${names.slice(0, -1).join(', ')} ve ${names.at(-1)} dersleri` : `${names[0]} dersi`;
    if (!await this.toaster.confirm(this.staffStudentId ? `${list} öğrencinin haftalık düzeninden kaldırılacak. Devam edilsin mi?` : `${list} haftalık düzeninden kaldırılacak. Devam edelim mi?`,
      { confirmText: 'Kaldır', cancelText: 'Vazgeç' })) return;
    const ids = new Set(lessons.map(x => x.lessonId.toLowerCase()));
    this.applying.set(true);
    this.weekly.save(this.version, this.timeZoneId, this.slots().filter(x => !ids.has(x.lessonId.toLowerCase())), this.staffStudentId ?? undefined)
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.applying.set(false))).subscribe({
        next: draft => { this.version = draft.version; this.slots.set(draft.slots); this.runPreview(); },
        error: e => { if (e?.status === 409) this.draftChanged(); else this.previewError.set(e?.error?.message ?? 'Haftalık düzen kaydedilemedi. Yeniden deneyebilirsin.'); }
      });
  }

  lessons() { return this.preview()?.schedule.lessons ?? []; }
  late(lesson: { remainingMinutes: number; lessonId: string }) { return lesson.remainingMinutes > 0 || this.leftOut(lesson.lessonId).length > 0; }
  leftOut(lessonId: string) { return (this.preview()?.leftOutTopics ?? []).filter(x => x.lessonId.toLowerCase() === lessonId.toLowerCase()); }
  leftOutNames(lessonId: string) { const topics = this.leftOut(lessonId); return topics.slice(0, 3).map(x => x.topicName).join(', ') + (topics.length > 3 ? '…' : ''); }
  spacedCount(lessonId: string) { return (this.preview()?.schedule.tasks ?? []).filter(x => x.lessonId === lessonId && x.kind === 'Spaced').length; }
  anyLate() { return this.lessons().some(x => this.late(x)) || !!this.preview()?.unscheduledLessons?.length; }
  unscheduledNames() { return (this.preview()?.unscheduledLessons ?? []).map(x => x.lessonName).join(', '); }

  /** "Önemli konulara odaklan": the important topics first, what does not fit is left out. */
  focusImportant() { if (this.busy()) return; this.prioritizeWhenShort.set(true); this.runPreview(); }
  /** "Bu dersi öne al": the course's priority becomes Yüksek, then the week is proposed and previewed again. */
  raiseCourse(lessonId: string) {
    const context = this.context;
    if (this.busy() || this.staffStudentId || !context) return;
    const courses = context.coursePreferences.map(x => x.lessonId.toLowerCase() === lessonId.toLowerCase() ? { ...x, priority: 1 } : { ...x });
    this.savingPriority.set(lessonId); this.previewError.set('');
    this.learning.save(learningUpdate(context, courses)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.savingPriority.set(null))).subscribe({
      next: view => { this.context = view.context; this.contextSaved.emit(view); this.propose(this.initialMode === 'Replace' ? 'Replace' : 'Fill'); },
      error: e => this.previewError.set(e?.status === 409 ? 'Derslerin başka bir yerde değişti; sayfayı yenileyip yeniden dene.'
        : typeof e?.error?.message === 'string' ? e.error.message : 'Dersin öne alınamadı. Yeniden deneyebilirsin.')
    });
  }

  // --- "Şu konuları öne al" -------------------------------------------------------------------------------------------------
  openPicker() {
    this.pickerOpen.set(true);
    if (!this.pickerLesson() && this.lessons().length) this.chooseLesson(this.lessons()[0].lessonId);
  }
  chooseLesson(lessonId: string) {
    this.pickerLesson.set(lessonId); this.pickerTopics.set([]); this.pickerTotal.set(0); this.pickerPage = 1; this.loadTopics();
  }
  loadTopics() {
    const lessonId = this.pickerLesson(); if (!lessonId) return;
    this.pickerLoading.set(true); this.pickerError.set('');
    (this.staffStudentId ? this.learning.catalogForStudent(this.staffStudentId, lessonId, this.pickerPage) : this.learning.catalog(lessonId, this.pickerPage))
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.pickerLoading.set(false))).subscribe({
        next: catalog => { this.pickerTopics.update(list => [...list, ...catalog.topics.items.filter(x => !list.some(y => y.id === x.id))]); this.pickerTotal.set(catalog.topics.totalCount); },
        error: () => this.pickerError.set('Konular yüklenemedi. Yeniden deneyebilirsin.')
      });
  }
  moreTopics() { if (this.pickerLoading() || this.pickerTopics().length >= this.pickerTotal()) return; this.pickerPage++; this.loadTopics(); }
  chosen(topicId: string) { return this.focusTopics().some(x => x.topicId === topicId); }
  addTopic(topic: StudyTopic) {
    if (this.chosen(topic.id) || this.focusTopics().length >= MAX_FOCUS_TOPICS) return;
    const lesson = this.lessons().find(x => x.lessonId === this.pickerLesson());
    this.focusTopics.update(list => [...list, { topicId: topic.id, topicName: topic.name, lessonName: lesson?.lessonName ?? topic.lessonName }]);
  }
  removeTopic(topicId: string) { this.focusTopics.update(list => list.filter(x => x.topicId !== topicId)); }
  readonly maxTopics = MAX_FOCUS_TOPICS;
  /** The chosen topics differ from the ones the shown preview was made with. */
  topicsChanged() {
    const sent = this.preview()?.request.focusTopicIds ?? [];
    const now = this.focusTopics().map(x => x.topicId);
    return sent.length !== now.length || now.some(id => !sent.includes(id));
  }

  // --- Publish ----------------------------------------------------------------------------------------------------------------
  canPublish() { const p = this.preview(); return !!p?.canCreate && !this.busy() && !this.topicsChanged(); }
  /**
   * Student: confirm, create the plan from the checked preview (its fingerprint), publish it with its version, then Planım.
   * Teacher: create the draft only, as the teacher flow always did.
   */
  async publish() {
    const p = this.preview();
    if (!p || !this.canPublish()) return;
    const staff = !!this.staffStudentId;
    if (!staff && !await this.toaster.confirm('Yayımlarsan bugünden sonraki çalışmaların bu plana göre değişir. Tamamladığın çalışmalar korunur.',
      { confirmText: 'Yayımla', cancelText: 'Vazgeç' })) return;
    this.publishing.set(true); this.publishIssue.set(null);
    this.engine.create({ title: PLAN_TITLE, preview: p.request, fingerprint: p.fingerprint }, this.staffStudentId ?? undefined)
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: created => {
          if (staff) { this.publishing.set(false); this.planCreated.emit(created.id); return; }
          this.planning.publish(created.id, created.version).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.publishing.set(false))).subscribe({
            next: () => {
              this.toaster.success('Planın yayımlandı. Bugünden sonraki çalışmaların yeni planına göre.');
              void this.router.navigate(['/coaching-portal/plan'], { queryParams: { plan: created.id } });
            },
            error: e => this.publishIssue.set({ createdId: created.id,
              message: `Planın taslak olarak kaydedildi ama yayımlanamadı. ${typeof e?.error?.message === 'string' ? e.error.message : 'Planım\'dan yeniden deneyebilirsin.'}` })
          });
        },
        error: e => {
          this.publishing.set(false);
          const message = typeof e?.error?.message === 'string' ? e.error.message : '';
          if (e?.status === 409 && /mevcut (plan )?taslağ/i.test(message)) {
            this.publishIssue.set({ draftExists: true, message: staff ? 'Öğrencinin bekleyen bir plan taslağı var. Önce o taslağın yayımlanması ya da kaldırılması gerekiyor.'
              : 'Bekleyen bir plan taslağın var. Önce Planım\'da o taslağı yayımla ya da kaldır.' });
            return;
          }
          if (e?.status === 409) { this.publishIssue.set({ stale: true, message: message || 'Plan hesaplandıktan sonra bilgilerin değişti. Yeniden hesapla.' }); return; }
          this.publishIssue.set({ message: message || 'Plan kaydedilemedi. Yeniden deneyebilirsin.' });
        }
      });
  }

  private focus(target: () => ElementRef<HTMLElement> | undefined) {
    afterNextRender(() => { const el = target()?.nativeElement; el?.scrollIntoView?.({ block: 'start' }); el?.focus({ preventScroll: true }); }, { injector: this.injector });
  }
}
