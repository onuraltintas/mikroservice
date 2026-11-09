import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Injector, Input, OnInit, Output, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, expand, finalize, map, of, reduce, switchMap, tap } from 'rxjs';
import { CoachingLearningSetupService, LearningCatalog, LearningContext, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, StudyTopic } from '../../../core/services/coaching-study-planning.service';
import { StudyTempo } from '../../../core/services/coaching-study-routine.service';
import { CoachingWeeklyDraftService, WeeklyExamFocus, WeeklyProposal, WeeklyProposalMode, WeeklyProposalWindow } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingWeeklyOutcomePlanService, WeeklyOutcomeOptions, WeeklyOutcomePreview, WeeklyOutcomeRequest } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { learningUpdate } from './wizard-courses-step.component';
import { DEFAULT_MAX_DAY_MINUTES, WeeklySlot, clock, slotError, weekDays } from './weekly-calendar';

/** Paket H1 (P10): the server's code for weekly lessons that are no longer chosen. */
const LESSONS_NOT_CHOSEN = 'StudyPlanning.LessonsNotChosen';
/** H4 contract item 7: the server's code for "a plan draft is already open". */
export const DRAFT_EXISTS = 'StudyPlanning.DraftExists';
export function isDraftExists(e: { status?: number; error?: { code?: string } } | null | undefined) {
  return e?.status === 409 && e.error?.code === DRAFT_EXISTS;
}
/** Paket H4 (K3): the most topics the student may move forward at once (the server refuses more). */
export const MAX_FOCUS_TOPICS = 50;
/** The most catalog pages the topic picker reads for one lesson. */
const MAX_TOPIC_PAGES = 20;
export const PLAN_TITLE = 'Haftalık derslerimden çalışma planım';
const longDate = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', timeZone: 'UTC' });
/** "12 Mayıs". */
export function dayMonth(iso: string | null | undefined) { if (!iso) return ''; const [y, m, d] = iso.split('-').map(Number); return longDate.format(new Date(Date.UTC(y, m - 1, d))); }

export interface FocusTopic { topicId: string; topicName: string; lessonName: string; lessonId?: string }
interface PublishIssue { message: string; draftExists?: boolean; stale?: boolean }

/**
 * Review of PR 135 (S4): the summary's own choices live with the wizard, so Geri, "Saat ekle" and "Bu dersi öne al" keep them
 * (the summary step is created again each time it opens).
 */
export class SummaryChoices {
  readonly focusTopics = signal<FocusTopic[]>([]);
  readonly prioritizeWhenShort = signal(false);
  readonly pickerOpen = signal(false);
  readonly pickerLesson = signal('');
  readonly pickerSearch = signal('');
}

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
  /** The chosen tempo's finish date, computed for `finishByMode`. */
  @Input() finishBy: string | null = null;
  /** The proposal mode the wizard's tempo options (and so `finishBy`) were computed for. */
  @Input() finishByMode: WeeklyProposalMode = 'Fill';
  /** The chosen tempo's name: a proposal in the other mode asks that mode's options and keeps the same tempo. */
  @Input() tempo: StudyTempo | null = null;
  /** A teacher's quick answers for a student without study times (a proposal only, never saved). */
  @Input() windows: WeeklyProposalWindow[] | null = null;
  /** "Sınavına kadar tek tempo var…" when the tempo step was skipped. */
  @Input() tempoNote = '';
  @Input() context: LearningContext | null = null;
  @Input() canGoBack = true;
  /** H5's "Yeniden öner" (?yeniden=oner): open with "Haftanı baştan öner" (after the usual confirmation). */
  @Input() replaceOnOpen = false;
  @Input() choices = new SummaryChoices();
  @Output() addHours = new EventEmitter<void>();
  @Output() planCreated = new EventEmitter<string>();
  @Output() contextSaved = new EventEmitter<LearningSetupView>();
  /** Review of PR 135 (S1): the weekly draft was saved or reloaded; true when it has cards (the next proposal is Fill). */
  @Output() draftSaved = new EventEmitter<boolean>();
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
  /** The finish date the shown proposal was made for (it may come from the other mode's tempo options). */
  private usedFinishBy: string | null = null;
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
  readonly savingPriority = signal<string | null>(null);
  // "Şu konuları öne al" (the topics of the chosen lesson; all of its catalog pages).
  readonly pickerTopics = signal<StudyTopic[]>([]);
  readonly pickerLoading = signal(false);
  readonly pickerError = signal('');
  private readonly lesson$ = new Subject<string>();
  readonly filteredTopics = computed(() => {
    const term = this.choices.pickerSearch().trim().toLocaleLowerCase('tr-TR');
    return this.pickerTopics().filter(x => !term || x.name.toLocaleLowerCase('tr-TR').includes(term) || (x.unitName ?? '').toLocaleLowerCase('tr-TR').includes(term));
  });
  // Publishing.
  readonly publishing = signal(false);
  readonly publishIssue = signal<PublishIssue | null>(null);
  /** Review of PR 135 (S2): the plan created before publishing failed; "Yeniden yayımla" publishes it, never creates another. */
  readonly created = signal<{ id: string; version: number } | null>(null);

  ngOnInit() {
    // S10: one lesson's topics at a time; a late answer for a lesson no longer chosen is dropped.
    this.lesson$.pipe(switchMap(lessonId => this.allTopics(lessonId)), takeUntilDestroyed(this.destroy)).subscribe();
    this.loadDraft(true);
    if (this.choices.pickerOpen() && this.choices.pickerLesson()) this.lesson$.next(this.choices.pickerLesson());
  }

  busy() { return this.loading() || this.proposing() || this.applying() || this.previewing() || this.publishing() || !!this.savingPriority(); }
  /** Re-review (7): only a write keeps the student on the page; reads are dropped when the step goes away. */
  writing() { return this.applying() || this.publishing() || !!this.savingPriority(); }
  /** Re-review F1: chosen topics whose lesson left the weekly draft, removed before the preview, with a short note. */
  readonly droppedTopics = signal('');
  /** Something chosen here would be lost by leaving the page (the chosen topics). */
  hasChoices() { return this.choices.focusTopics().length > 0; }

  private loadDraft(propose: boolean) {
    this.loading.set(true); this.error.set('');
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false))).subscribe({
      next: page => {
        this.applyPage(page.draft); this.maxDayMinutes = page.maxDayMinutes ?? DEFAULT_MAX_DAY_MINUTES;
        if (!propose) return;
        this.initialMode = this.slots().length ? 'Fill' : 'Replace';
        if (this.replaceOnOpen && this.slots().length) { void this.openWithReplace(); return; }
        this.propose(this.initialMode);
      },
      error: () => this.error.set(this.staffStudentId ? 'Öğrencinin haftalık düzeni yüklenemedi. Yeniden deneyebilirsin.' : 'Haftalık düzenin yüklenemedi. Yeniden deneyebilirsin.')
    });
  }
  private applyPage(draft: { version: number; timeZoneId: string; slots: WeeklySlot[] } | null) {
    this.slots.set(draft?.slots ?? []); this.version = draft?.version ?? null;
    if (draft?.timeZoneId) this.timeZoneId = draft.timeZoneId;
  }
  private async openWithReplace() {
    if (await this.confirmReplace()) { this.initialMode = 'Replace'; this.propose('Replace'); } else this.propose(this.initialMode);
  }
  private confirmReplace() {
    return this.toaster.confirm(this.staffStudentId
      ? 'Öğrencinin mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.'
      : 'Mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.');
  }
  retry() { this.publishIssue.set(null); this.preview.set(null); this.proposal.set(null); this.loadDraft(true); }

  /**
   * Review of PR 135 (S1): the finish date for a proposal in this mode. The wizard's tempo options belong to one mode; a proposal in
   * the other (for example "Haftanı baştan öner" on a week with cards) asks that mode's options and keeps the same tempo.
   */
  private finishFor(mode: WeeklyProposalMode): Observable<string | null> {
    if (mode === this.finishByMode || !this.finishBy) return of(this.finishBy);
    return this.weekly.tempoOptions(this.examFocus, this.staffStudentId ?? undefined, this.windows ?? undefined, mode).pipe(
      map(result => { const list = result.options ?? []; return (list.find(x => x.tempo === this.tempo) ?? list[0])?.finishBy ?? null; }));
  }

  /** The server's proposal; an empty one with saved cards goes straight on to the yearly preview. */
  propose(mode: WeeklyProposalMode) {
    this.proposing.set(true); this.error.set(''); this.preview.set(null); this.publishIssue.set(null);
    this.finishFor(mode).pipe(
      switchMap(finishBy => { this.usedFinishBy = finishBy; return this.weekly.proposal(mode, this.examFocus, this.staffStudentId ?? undefined, this.windows ?? undefined, finishBy); }),
      takeUntilDestroyed(this.destroy), finalize(() => this.proposing.set(false))).subscribe({
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
    if (!await this.confirmReplace()) return;
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
      next: draft => {
        this.applyPage(draft); this.draftSaved.emit(draft.slots.length > 0);
        this.proposal.set({ ...view, mode: 'Fill', slots: [] }); this.runPreview();
      },
      error: e => { if (e?.status === 409) this.draftChanged(); else this.error.set(e?.error?.message ?? 'Haftalık düzen kaydedilemedi. Yeniden deneyebilirsin.'); }
    });
  }
  private draftChanged() {
    this.proposal.set(null);
    const message = this.staffStudentId ? 'Öğrencinin haftalık düzeni başka bir yerde değişti; güncel hâlini yükleyip öneriyi yeniledim.'
      : 'Haftalık düzenin başka bir yerde değişti; güncel hâlini yükleyip öneriyi yeniledim.';
    this.error.set(message);
    this.weekly.get(this.staffStudentId ?? undefined).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: page => {
        this.applyPage(page.draft); this.draftSaved.emit(this.slots().length > 0);
        this.propose(this.slots().length ? 'Fill' : 'Replace'); this.error.set(message);
      }
    });
  }

  // --- The yearly preview -----------------------------------------------------------------------------------------------------
  private request(options: WeeklyOutcomeOptions): WeeklyOutcomeRequest {
    // The tempo's finish date ends the plan; without one (or before the start) the plan ends on the server's default.
    const finishBy = this.usedFinishBy ?? this.finishBy;
    const end = finishBy && finishBy >= options.startDate ? finishBy : options.endDate;
    const topics = this.choices.focusTopics().map(x => x.topicId);
    return { startDate: options.startDate, endDate: end, expectedWeeklyDraftVersion: options.weeklyDraftVersion, reuseFreedSlots: true, engineVersion: 2,
      prioritizeWhenShort: this.choices.prioritizeWhenShort(), ...(topics.length ? { focusTopicIds: topics } : {}) };
  }
  runPreview() {
    const student = this.staffStudentId ?? undefined;
    this.pruneTopics();
    // Re-review (3): a plan created before a failed publish is kept, so "Yeniden yayımla" still publishes it (no second create).
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
        next: draft => { this.applyPage(draft); this.draftSaved.emit(draft.slots.length > 0); this.runPreview(); },
        error: e => { if (e?.status === 409) this.draftChanged(); else this.previewError.set(e?.error?.message ?? 'Haftalık düzen kaydedilemedi. Yeniden deneyebilirsin.'); }
      });
  }

  /**
   * Re-review F1: the server refuses topics of a lesson that is not in the weekly draft (for example after "Haftanı baştan öner"),
   * so those topics are dropped before the preview and the student is told which lesson they belonged to.
   */
  private pruneTopics() {
    const lessons = new Set(this.slots().map(x => x.lessonId.toLowerCase()));
    const gone = this.choices.focusTopics().filter(x => x.lessonId && !lessons.has(x.lessonId.toLowerCase()));
    if (!gone.length) return;
    this.choices.focusTopics.update(list => list.filter(x => !gone.includes(x)));
    const names = [...new Set(gone.map(x => x.lessonName))];
    const lessonText = names.length > 1 ? `${names.slice(0, -1).join(', ')} ve ${names.at(-1)} dersleri` : `${names[0]} dersi`;
    this.droppedTopics.set(`${lessonText} ${this.staffStudentId ? 'öğrencinin haftalık düzeninden' : 'haftalık düzeninden'} çıktığı için ${gone.length} konu öne alınanlardan kaldırıldı.`);
  }

  lessons() { return this.preview()?.schedule.lessons ?? []; }
  late(lesson: { remainingMinutes: number; lessonId: string }) { return lesson.remainingMinutes > 0 || this.leftOut(lesson.lessonId).length > 0; }
  leftOut(lessonId: string) { return (this.preview()?.leftOutTopics ?? []).filter(x => x.lessonId.toLowerCase() === lessonId.toLowerCase()); }
  leftOutNames(lessonId: string) { const topics = this.leftOut(lessonId); return topics.slice(0, 3).map(x => x.topicName).join(', ') + (topics.length > 3 ? '…' : ''); }
  spacedCount(lessonId: string) { return (this.preview()?.schedule.tasks ?? []).filter(x => x.lessonId === lessonId && x.kind === 'Spaced').length; }
  unscheduledNames() { return (this.preview()?.unscheduledLessons ?? []).map(x => x.lessonName).join(', '); }

  /** "Önemli konulara odaklan": the important topics first, what does not fit is left out. */
  focusImportant() { if (this.busy()) return; this.choices.prioritizeWhenShort.set(true); this.runPreview(); }
  /** Review of PR 135 (S4): its undo, "Bütün konuları planla". */
  planAllTopics() { if (this.busy()) return; this.choices.prioritizeWhenShort.set(false); this.runPreview(); }
  /** "Bu dersi öne al": the course's priority becomes Yüksek, then the week is proposed and previewed again. */
  raiseCourse(lessonId: string) {
    const context = this.context;
    if (this.busy() || this.staffStudentId || !context) return;
    const courses = context.coursePreferences.map(x => x.lessonId.toLowerCase() === lessonId.toLowerCase() ? { ...x, priority: 1 } : { ...x });
    this.savingPriority.set(lessonId); this.previewError.set('');
    this.learning.save(learningUpdate(context, courses)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.savingPriority.set(null))).subscribe({
      next: view => { this.context = view.context; this.contextSaved.emit(view); this.propose(this.initialMode); },
      error: e => this.previewError.set(e?.status === 409 ? 'Derslerin başka bir yerde değişti; sayfayı yenileyip yeniden dene.'
        : typeof e?.error?.message === 'string' ? e.error.message : 'Dersin öne alınamadı. Yeniden deneyebilirsin.')
    });
  }

  // --- "Şu konuları öne al" -------------------------------------------------------------------------------------------------
  openPicker() {
    this.choices.pickerOpen.set(true);
    const lesson = this.choices.pickerLesson() || this.lessons()[0]?.lessonId;
    if (lesson) this.chooseLesson(lesson);
  }
  chooseLesson(lessonId: string) { this.choices.pickerLesson.set(lessonId); this.lesson$.next(lessonId); }
  retryTopics() { const lesson = this.choices.pickerLesson(); if (lesson) this.lesson$.next(lesson); }
  /** S10: the catalog has no search, so every page of the lesson is read before the search filters it. */
  private allTopics(lessonId: string) {
    this.pickerTopics.set([]); this.pickerLoading.set(true); this.pickerError.set('');
    const page = (n: number) => this.staffStudentId ? this.learning.catalogForStudent(this.staffStudentId, lessonId, n) : this.learning.catalog(lessonId, n);
    return page(1).pipe(
      expand((c: LearningCatalog) => c.topics.pageNumber < MAX_TOPIC_PAGES && c.topics.pageNumber * c.topics.pageSize < c.topics.totalCount ? page(c.topics.pageNumber + 1) : EMPTY),
      reduce((all: StudyTopic[], c: LearningCatalog) => [...all, ...c.topics.items.filter(x => !all.some(y => y.id === x.id))], []),
      tap(topics => { this.pickerTopics.set(topics); this.pickerLoading.set(false); }),
      catchError(() => { this.pickerLoading.set(false); this.pickerError.set('Konular yüklenemedi. Yeniden deneyebilirsin.'); return EMPTY; }));
  }
  chosen(topicId: string) { return this.choices.focusTopics().some(x => x.topicId === topicId); }
  addTopic(topic: StudyTopic) {
    if (this.chosen(topic.id) || this.choices.focusTopics().length >= MAX_FOCUS_TOPICS) return;
    const lesson = this.lessons().find(x => x.lessonId === this.choices.pickerLesson());
    this.choices.focusTopics.update(list => [...list, { topicId: topic.id, topicName: topic.name, lessonName: lesson?.lessonName ?? topic.lessonName,
      lessonId: lesson?.lessonId ?? topic.lessonId ?? this.choices.pickerLesson() }]);
    this.droppedTopics.set('');
  }
  removeTopic(topicId: string) { this.choices.focusTopics.update(list => list.filter(x => x.topicId !== topicId)); }
  readonly maxTopics = MAX_FOCUS_TOPICS;
  /** The chosen topics differ from the ones the shown preview was made with (the server echoes them sorted). */
  topicsChanged() {
    const sent = this.preview()?.request.focusTopicIds ?? [];
    const now = this.choices.focusTopics().map(x => x.topicId);
    return sent.length !== now.length || now.some(id => !sent.includes(id));
  }

  // --- Publish ----------------------------------------------------------------------------------------------------------------
  canPublish() { const p = this.preview(); return !!p?.canCreate && !this.busy() && !this.topicsChanged() && !this.publishIssue()?.stale; }
  /**
   * Student: confirm, create the plan from the checked preview (its fingerprint), publish it with its version, then Planım.
   * Teacher: create the draft only, as the teacher flow always did. After a failed publish only the publish is tried again.
   */
  async publish() {
    const p = this.preview();
    if (!p || !this.canPublish()) return;
    const staff = !!this.staffStudentId, created = this.created();
    if (!staff && !await this.toaster.confirm('Yayımlarsan bugünden sonraki çalışmaların bu plana göre değişir. Tamamladığın çalışmalar korunur.',
      { confirmText: 'Yayımla', cancelText: 'Vazgeç' })) return;
    this.publishing.set(true); this.publishIssue.set(null);
    if (created && !staff) { this.publishCreated(created); return; }
    this.engine.create({ title: PLAN_TITLE, preview: p.request, fingerprint: p.fingerprint }, this.staffStudentId ?? undefined)
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: plan => {
          if (staff) { this.publishing.set(false); this.planCreated.emit(plan.id); return; }
          this.created.set({ id: plan.id, version: plan.version }); this.publishCreated(plan);
        },
        error: e => {
          this.publishing.set(false);
          const message = typeof e?.error?.message === 'string' ? e.error.message : '';
          if (isDraftExists(e)) {
            this.publishIssue.set({ draftExists: true, message: staff ? 'Öğrencinin bekleyen bir plan taslağı var. Önce o taslağın yayımlanması ya da kaldırılması gerekiyor.'
              : 'Bekleyen bir plan taslağın var. Önce Planım\'da o taslağı yayımla ya da kaldır.' });
            return;
          }
          if (e?.status === 409) { this.publishIssue.set({ stale: true, message: message || 'Plan hesaplandıktan sonra bilgilerin değişti. Yeniden hesapla.' }); return; }
          this.publishIssue.set({ message: message || 'Plan kaydedilemedi. Yeniden deneyebilirsin.' });
        }
      });
  }
  private publishCreated(plan: { id: string; version: number }) {
    this.planning.publish(plan.id, plan.version).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.publishing.set(false))).subscribe({
      next: () => {
        // The plan used the chosen topics: nothing is left for the leave guard to ask about.
        this.choices.focusTopics.set([]);
        this.toaster.success('Planın yayımlandı. Bugünden sonraki çalışmaların yeni planına göre.');
        void this.router.navigate(['/coaching-portal/plan'], { queryParams: { plan: plan.id } });
      },
      error: e => {
        if (e?.status === 409) { this.reloadCreated(plan.id); return; }
        this.publishIssue.set({ message: `Planın taslak olarak kaydedildi ama yayımlanamadı. ${typeof e?.error?.message === 'string' ? e.error.message : 'Yeniden yayımlamayı deneyebilirsin.'}` });
      }
    });
  }
  /**
   * Re-review (3): a publish conflict means the saved draft changed (its version); it is read again, so "Yeniden yayımla" sends the
   * current version. A draft that is already active counts as published.
   */
  private reloadCreated(id: string) {
    this.publishing.set(true);
    this.planning.get(id).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.publishing.set(false))).subscribe({
      next: plan => {
        if (plan.status === 'Active') { this.choices.focusTopics.set([]); void this.router.navigate(['/coaching-portal/plan'], { queryParams: { plan: id } }); return; }
        this.created.set({ id, version: plan.version });
        this.publishIssue.set({ message: 'Kaydedilen plan taslağın başka bir yerde değişti; güncel hâlini yükledim. Yeniden yayımlayabilirsin.' });
      },
      error: () => this.publishIssue.set({ stale: true, message: 'Kaydedilen plan taslağın okunamadı. Planım\'dan kontrol edebilirsin.' })
    });
  }

  private focus(target: () => ElementRef<HTMLElement> | undefined) {
    afterNextRender(() => { const el = target()?.nativeElement; el?.scrollIntoView?.({ block: 'start' }); el?.focus({ preventScroll: true }); }, { injector: this.injector });
  }
}
