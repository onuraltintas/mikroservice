import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { CoachingLearningSetupService, ExamLearningPurpose, LearningCatalog, LearningCourse, LearningLesson, LearningExamSession, LearningSetupImpact, LearningSetupUpdate, LearningSetupView, LearningTopic, RETIRED_PURPOSE_NOTICE, TopicDeclaration, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';

type SetupField = 'purpose' | 'examYear' | 'language' | 'yokdilField' | 'courses';
/** E4 #26: the confirmation after a save (it stays when the page moves on to the next section). */
interface SetupSaved { text: string; removed: string[]; diploma: number | null; teacher: boolean }
const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
import { OABT_FIELDS, YOKDIL_FIELDS } from '../../../core/models/practice-exam';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { StudentPlanPreviewComponent } from './student-plan-preview.component';
import { environment } from '../../../../environments/environment';
import { CoachingStudyPlanningService, PublishedStudyOutcome, StudyTopic } from '../../../core/services/coaching-study-planning.service';
import { PURPOSES, YKS_FIELDS } from '../onboarding/onboarding-model';
import { currentAcademicYear } from '../coaching-education';

/** F2 #28: the course groups in exam order (TYT before AYT); any other exam code follows. */
const LESSON_GROUPS: Record<string, { order: number; label: string }> = {
  LGS: { order: 0, label: 'LGS' }, TYT: { order: 1, label: 'TYT' }, AYT: { order: 2, label: 'AYT' }, YDT: { order: 3, label: 'YDT' },
  AGS: { order: 4, label: 'AGS' }, OABT: { order: 5, label: 'ÖABT' }, YDS: { order: 6, label: 'YDS' }, YOKDIL: { order: 7, label: 'YÖKDİL' } };

@Component({
  selector: 'app-student-learning-setup', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StudentStudyAvailabilityComponent, StudentPlanPreviewComponent],
  templateUrl: './student-learning-setup.component.html', styleUrl: './student-study-plans.component.scss'
})
export class StudentLearningSetupComponent implements OnInit {
  private readonly toaster = coachingToaster();
  private readonly service = inject(CoachingLearningSetupService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly apiUrl = environment.apiUrl;
  readonly state = signal<LearningSetupView | null>(null); readonly step = signal('Purpose');
  readonly loading = signal(false); readonly saving = signal(false); readonly ready = signal(false);
  readonly error = signal<string | null>(null); readonly catalogError = signal<string | null>(null); readonly catalogLoading = signal(false);
  readonly catalog = signal<LearningCatalog | null>(null);
  /** E4 #24: the fields the last save attempt refused (shown next to each field and in the summary at the top). */
  readonly fieldErrors = signal<Partial<Record<SetupField, string>>>({});
  /** E4 #26: what a new exam or field removes, confirmed in the page before saving. */
  readonly impactAsk = signal<string[] | null>(null);
  private pendingPurpose: { request: LearningSetupUpdate; impact: LearningSetupImpact } | null = null;
  readonly saved = signal<SetupSaved | null>(null);
  readonly savedText = computed(() => this.saved()?.text ?? null);
  // E3 #14: no "Eğitim bilgilerim" step: the education is edited on Profilim (a missing one sends the student there).
  // F2 #27: the wizard's name for the exam step ("Sınavın").
  readonly steps = [{ id: 'Purpose', label: 'Sınavım' },
    { id: 'Availability', label: 'Müsaitliğim' }, { id: 'Courses', label: 'Derslerim' }, { id: 'Starting', label: 'Başlangıç durumum' }, { id: 'Preview', label: 'Plan önizlemesi' }];
  // KM8 (01.11.2026): only exam purposes; the catalog keeps only exam lessons. KM1 added AGS (+ÖABT), YDS and YÖKDİL
  // (the language exams are prepared in English only).
  // F2 #27: the wizard's names for the exams and the YKS score types.
  readonly purposes = PURPOSES as { value: ExamLearningPurpose; label: string }[];
  readonly yksFields = YKS_FIELDS;
  readonly retiredPurposeNotice = RETIRED_PURPOSE_NOTICE;
  /** Paket D (#8): only the exams that fit the grade on the profile (every exam when the server does not say). */
  purposeChoices() { const allowed = this.state()?.allowedPurposes; return allowed ? this.purposes.filter(x => allowed.includes(x.value)) : this.purposes; }
  readonly oabtFields = Object.entries(OABT_FIELDS);
  readonly yokdilFields = Object.entries(YOKDIL_FIELDS);
  private static readonly examCodes: Record<ExamLearningPurpose, string> = { Lgs: 'LGS', Yks: 'TYT', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YOKDIL' };
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  /** The saved school year (courses and topics are saved in it). F2 review: Sınavım is saved in the current one (`examYearBase`). */
  academicYearStart = currentAcademicYear();
  /** F2 review: a saved year without a session (an older choice) stays selectable while the exam is the same. */
  private legacyYear: { purpose: string; year: number } | null = null;
  /** The school year the exam is chosen in now (not shown; the wizard's rule, from 1 September on). */
  examYearBase() { return currentAcademicYear(); }
  purpose: ExamLearningPurpose | '' = ''; targetExamYear: number | null = null; primaryScoreType = 'TYT'; foreignLanguage = '';
  targetExamSessionId: string | null = null; oabtField = ''; yokdilField = '';
  courses: LearningCourse[] = []; topics: LearningTopic[] = []; startingStatusReviewed = false;
  readonly expandedTopicId = signal<string | null>(null);
  readonly topicOutcomes = signal<Record<string, PublishedStudyOutcome[]>>({});
  readonly topicOutcomeLoading = signal<string | null>(null);
  readonly topicOutcomeErrors = signal<Record<string, boolean>>({});
  selectedLesson = ''; topicPage = 1; dirty = false;
  async ngOnInit() { await this.load(); }
  /** E6 #29: the saved courses (Derslerim) the plan preview uses. */
  readonly courseIds = computed(() => (this.state()?.context?.coursePreferences ?? []).map(x => x.lessonId));
  async load() {
    if (this.loading() || this.saving()) return;
    if (this.dirty && !await this.toaster.confirm('Kaydedilmemiş seçimlerden vazgeçip sunucudaki bilgileri yükleyelim mi?')) return;
    // F2 (E4 b): reloading drops a confirmation that is still open (once the student agreed to drop the changes).
    this.cancelImpact();
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    this.service.get().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: state => { this.apply(state); this.ready.set(true); this.step.set(state.nextStep === 'Catalog' ? 'Courses' : state.nextStep); if (this.step() === 'Courses' || this.step() === 'Starting') this.loadCatalog(); },
      error: e => { if (e?.status === 404) this.step.set('Profile'); this.error.set(e?.status === 404 ? 'Koçluk profilin henüz hazırlanmadı. Profilim sayfasından profilini oluşturabilirsin.' : 'Kurulum bilgileri yüklenemedi. Yeniden dene.'); }
    });
  }
  private apply(state: LearningSetupView) {
    this.state.set(state); const c = state.context;
    if (c) {
      // KM8: a retired purpose (School, General, Undecided) is not preselected; the student chooses an exam.
      this.academicYearStart = c.academicYearStart; this.purpose = isRetiredPurpose(c.purpose) || state.purposeNotForEducation ? '' : c.purpose as ExamLearningPurpose; this.targetExamYear = c.targetExamYear;
      this.primaryScoreType = c.purpose === 'Yks' ? c.primaryScoreType ?? 'TYT' : 'TYT'; this.foreignLanguage = c.purpose === 'Yks' ? c.foreignLanguage ?? '' : '';
      this.oabtField = c.purpose === 'Ags' ? c.primaryScoreType ?? '' : ''; this.yokdilField = c.purpose === 'Yokdil' ? c.primaryScoreType ?? '' : '';
      this.targetExamSessionId = c.targetExamSessionId ?? null;
      this.legacyYear = !c.targetExamSessionId && c.targetExamYear ? { purpose: c.purpose, year: c.targetExamYear } : null;
      this.courses = c.coursePreferences.map(x => ({ ...x })); this.topics = c.topicStates.map(x => ({ ...x })); this.startingStatusReviewed = c.startingStatusReviewed;
    }
    this.dirty = false; this.suggested.set(false);
  }
  async goTo(step: string) {
    if (this.saving() || this.loading() || this.catalogLoading()) return;
    if (!await this.canLeaveChildren()) return;
    if (this.dirty) { this.error.set('Adım değiştirmeden önce seçimlerini kaydet veya yeniden yükle.'); return; }
    // F2 #28: suggested courses that were not saved are dropped when the student moves on.
    if (this.suggested()) { this.courses = (this.state()?.context?.coursePreferences ?? []).map(x => ({ ...x })); this.suggested.set(false); }
    this.step.set(step); this.error.set(null); this.fieldErrors.set({}); this.cancelImpact();
    if (step === 'Courses' || step === 'Starting') this.loadCatalog();
  }
  /** E4 review: any change drops a pending confirmation, so "Evet, kaydet" never saves a choice that was not shown. */
  changed() { this.dirty = true; this.cancelImpact(); }
  examPurpose() { return !!this.purpose; }
  /** KM8: the saved purpose can no longer be chosen; only Profile and Purpose stay open until an exam is saved. */
  purposeRetired() { const state = this.state(); return !!state?.purposeRetired || isRetiredPurpose(state?.context?.purpose); }
  /** KM1: upcoming sessions of the purpose's exam; the student picks one only when there are several. */
  sessionChoices(): LearningExamSession[] {
    const code = this.purpose ? StudentLearningSetupComponent.examCodes[this.purpose] : null;
    return code ? (this.state()?.examSessions ?? []).filter(x => x.examCode === code) : [];
  }
  selectSession(id: string | null) {
    this.targetExamSessionId = id; const session = this.sessionChoices().find(x => x.id === id);
    if (session) this.targetExamYear = Number(session.date.slice(0, 4));
    this.changed();
  }
  /** F2 #27: the wizard's question: the session when the exam has dated sessions, the year otherwise. */
  examQuestion() { return this.sessionChoices().length ? 'Hangi oturuma hazırlanıyorsun?' : 'Hangi yıl sınava gireceksin?'; }
  examChoices(): { value: string; label: string }[] {
    const sessions = this.sessionChoices();
    if (sessions.length) {
      const legacy = this.legacy();
      return [...sessions.map(x => ({ value: `session:${x.id}`, label: this.sessionLabel(x) })),
        ...(legacy ? [{ value: `year:${legacy}`, label: `${legacy} · oturum seçmedim` }] : [])];
    }
    const base = this.examYearBase();
    const years = new Set([1, 2, 3, 4, 5].map(offset => base + offset));
    if (this.targetExamYear) years.add(this.targetExamYear);
    return [...years].sort((a, b) => a - b).map(year => ({ value: `year:${year}`, label: String(year) }));
  }
  examChoice() {
    const sessions = this.sessionChoices();
    if (sessions.length) return sessions.some(x => x.id === this.targetExamSessionId) ? `session:${this.targetExamSessionId}`
      : this.legacy() !== null && this.targetExamYear === this.legacy() ? `year:${this.targetExamYear}` : '';
    return this.targetExamYear ? `year:${this.targetExamYear}` : '';
  }
  private legacy() { const legacy = this.legacyYear; return legacy && legacy.purpose === this.purpose ? legacy.year : null; }
  pickExam(value: string) {
    if (value.startsWith('session:')) { this.selectSession(value.slice(8)); return; }
    this.targetExamSessionId = null; this.targetExamYear = Number(value.slice(5)) || null; this.changed();
  }
  sessionLabel(session: LearningExamSession) {
    const day = (iso: string) => iso.split('-').reverse().join('.');
    const window = session.applicationStart && session.applicationEnd
      ? ` · başvuru ${day(session.applicationStart).slice(0, 5)}–${day(session.applicationEnd)}` : '';
    return `${session.label} · ${day(session.date)}${window}`;
  }
  fieldError(name: SetupField) { return this.fieldErrors()[name] ?? null; }
  invalid(name: SetupField) { return this.fieldErrors()[name] ? 'true' : null; }
  describedBy(name: SetupField) { return this.fieldErrors()[name] ? `setup-${name}-error` : null; }
  hasFieldErrors() { return Object.keys(this.fieldErrors()).length > 0; }
  /** E4 #24: every attempt clears the previous message; refused fields are named next to the field and in the summary. */
  private refuse(section: string, errors: Partial<Record<SetupField, string>>): boolean {
    this.error.set(null); this.saved.set(null); this.fieldErrors.set(errors); this.cancelImpact();
    const messages = Object.values(errors);
    if (!messages.length) { this.wrongCourse.set(null); return false; }
    this.error.set(`${section} ${messages.join(' ')}`);
    // E4 review: the focus goes to the first refused field.
    setTimeout(() => (this.host.nativeElement.querySelector('[aria-invalid="true"]') as HTMLElement | null)?.focus());
    return true;
  }
  /** The course whose priority or difficulty was refused. */
  readonly wrongCourse = signal<string | null>(null);
  courseInvalid(lessonId: string) { return this.wrongCourse() === lessonId ? 'true' : null; }
  courseDescribedBy(lessonId: string) { return this.wrongCourse() === lessonId ? 'setup-courses-error' : null; }

  /**
   * E4 #26: a new exam, field or language may remove parts of the student's own target (PR 114) and the diploma grade; the server
   * says which before saving and the student confirms in the page. A teacher's goal is kept (the confirmation shows the mismatch).
   */
  savePurpose() {
    if (this.loading() || this.saving() || !this.ready()) return;
    const errors: Partial<Record<SetupField, string>> = {};
    if (!this.purpose) errors.purpose = 'Hazırlandığın sınavı seç.';
    else if (this.sessionChoices().length) { if (!this.examChoice()) errors.examYear = 'Hangi oturuma hazırlandığını seç.'; }
    else if (!this.targetExamYear) errors.examYear = 'Hangi yıl sınava gireceğini seç.';
    else if (!Number.isInteger(this.targetExamYear) || this.targetExamYear < this.examYearBase() || this.targetExamYear > this.examYearBase() + 5)
      errors.examYear = `Sınav yılı ${this.examYearBase()} ile ${this.examYearBase() + 5} arasında olmalı.`;
    if (this.purpose === 'Yks' && this.primaryScoreType === 'DIL' && !this.foreignLanguage) errors.language = 'Yabancı dili seç.';
    if (this.purpose === 'Yokdil' && !this.yokdilField) errors.yokdilField = 'YÖKDİL alanını seç.';
    if (this.refuse('Sınavın kaydedilmedi.', errors)) return;
    // F2 review: saved in the current school year (as the wizard does); a new school year is a new scope.
    const year = this.examYearBase();
    const previous = this.state()?.context; const scope = this.request([], [], false);
    const sameScope = previous && !this.state()?.missingFields.some(x => x === 'Purpose' || x === 'Catalog')
      && previous.academicYearStart === year && previous.purpose === this.purpose
      && previous.primaryScoreType === scope.primaryScoreType && previous.foreignLanguage === scope.foreignLanguage;
    const request = { ...this.request(sameScope ? this.courses : [], sameScope ? this.topics : [], !!sameScope && this.startingStatusReviewed), academicYearStart: year };
    const scopeChanged = !!previous && (previous.purpose !== scope.purpose || (previous.primaryScoreType ?? null) !== scope.primaryScoreType
      || (previous.foreignLanguage ?? null) !== scope.foreignLanguage);
    if (!scopeChanged) { this.persist(request, 'Availability', 'Sınavın kaydedildi.'); return; }
    this.saving.set(true);
    this.service.impact({ purpose: scope.purpose, primaryScoreType: scope.primaryScoreType, foreignLanguage: scope.foreignLanguage })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: impact => {
          this.saving.set(false);
          const lines = this.impactLines(impact, previous!.purpose === scope.purpose);
          if (lines.length) { this.pendingPurpose = { request, impact }; this.impactAsk.set(lines); }
          else this.persist(request, 'Availability', 'Sınavın kaydedildi.', impact);
        },
        error: e => {
          this.saving.set(false);
          // E4 review: an API without the check (deployed later) still saves, as before the check existed.
          if (e?.status === 404) { this.persist(request, 'Availability', 'Sınavın kaydedildi.'); return; }
          this.error.set(e?.error?.message ?? 'Değişikliğin hedefine etkisi kontrol edilemedi. Bağlantını kontrol edip yeniden dene.');
        }
      });
  }
  confirmImpact() {
    const pending = this.pendingPurpose; if (!pending) return;
    this.cancelImpact(); this.persist(pending.request, 'Availability', 'Sınavın kaydedildi.', pending.impact);
  }
  cancelImpact() { this.pendingPurpose = null; this.impactAsk.set(null); }
  private impactLines(impact: LearningSetupImpact, samePurpose: boolean): string[] {
    const lines: string[] = [];
    if (impact.removedTargets.length)
      lines.push(`Hedefin (${impact.removedTargets.join(', ')}) ${samePurpose ? 'bu puan türüne' : 'bu sınava'} uymuyor; kaydedersen kaldırılacak.`);
    if (impact.diplomaGradeRemoved !== null)
      lines.push(`Diploma notun (${decimal.format(impact.diplomaGradeRemoved)}) silinecek; diploma notu yalnız YKS'de kullanılıyor.`);
    return lines;
  }
  formatNumber(value: number) { return decimal.format(value); }
  private request(courses = this.courses, topics = this.topics, reviewed = this.startingStatusReviewed): LearningSetupUpdate {
    return { expectedVersion: this.state()?.context?.version ?? null, academicYearStart: this.academicYearStart, purpose: this.purpose as ExamLearningPurpose,
      targetExamYear: this.examPurpose() ? this.targetExamYear : null,
      primaryScoreType: this.purpose === 'Yks' ? this.primaryScoreType : this.purpose === 'Ags' ? this.oabtField || null : this.purpose === 'Yokdil' ? this.yokdilField || null : null,
      foreignLanguage: this.purpose === 'Yks' && this.primaryScoreType === 'DIL' ? this.foreignLanguage : this.purpose === 'Yds' || this.purpose === 'Yokdil' ? 'English' : null,
      targetExamSessionId: this.sessionChoices().some(x => x.id === this.targetExamSessionId) ? this.targetExamSessionId : null,
      courses, topics, startingStatusReviewed: reviewed };
  }
  private persist(request: LearningSetupUpdate, next: string, message: string, impact: LearningSetupImpact | null = null) {
    if (this.saving() || !this.ready()) return;
    this.saving.set(true); this.error.set(null); this.saved.set(null);
    this.service.save(request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: state => {
        this.apply(state); this.step.set(next); if (next === 'Starting' || next === 'Courses') this.loadCatalog();
        // E4 #26: a short confirmation that stays as the page moves on, with what was removed.
        this.saved.set({ text: message, removed: impact?.removedTargets ?? [], diploma: impact?.diplomaGradeRemoved ?? null, teacher: !!impact?.teacherGoalMismatch });
      },
      error: e => this.error.set(e?.status === 409 ? 'Kurulum başka bir oturumda değişti. Seçimlerin korundu; güncel bilgileri yeniden yükle.' : e?.error?.message ?? 'Kurulum kaydedilemedi. Seçimlerin ekranda korundu.')
    });
  }
  async afterHoursSaved() { await this.load(); }
  loadCatalog(page = 1) {
    if (this.catalogLoading()) return;
    this.catalogLoading.set(true); this.catalogError.set(null);
    this.service.catalog(this.selectedLesson || null, page).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.catalogLoading.set(false))).subscribe({
      next: catalog => { this.catalog.set(catalog); this.topicPage = page; this.suggestCourses(); }, error: () => this.catalogError.set('Dersler yüklenemedi.')
    });
  }
  topicGroups() {
    const groups = new Map<string, StudyTopic[]>();
    for (const topic of this.catalog()?.topics.items ?? []) {
      const name = topic.unitName || 'Diğer konular';
      groups.set(name, [...(groups.get(name) ?? []), topic]);
    }
    return [...groups].map(([name, topics]) => ({ name, topics }));
  }
  toggleTopicOutcomes(topicId: string) {
    if (this.expandedTopicId() === topicId) { this.expandedTopicId.set(null); return; }
    this.expandedTopicId.set(topicId);
    this.loadTopicOutcomes(topicId);
  }
  private loadTopicOutcomes(topicId: string) {
    if (this.topicOutcomes()[topicId] || this.topicOutcomeLoading() === topicId) return;
    this.topicOutcomeLoading.set(topicId);
    this.planning.getTopicOutcomes(topicId).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.topicOutcomeLoading.set(null))).subscribe({
      next: outcomes => { this.topicOutcomes.update(current => ({ ...current, [topicId]: outcomes })); this.topicOutcomeErrors.update(current => ({ ...current, [topicId]: false })); },
      error: () => this.topicOutcomeErrors.update(current => ({ ...current, [topicId]: true }))
    });
  }
  retryTopicOutcomes(topicId: string) { this.topicOutcomes.update(current => { const next = { ...current }; delete next[topicId]; return next; }); this.topicOutcomeErrors.update(current => ({ ...current, [topicId]: false })); this.loadTopicOutcomes(topicId); }
  /** F2 #28: the lessons of the exam in groups (TYT, AYT, …); `withCourses` keeps only the chosen ones (Başlangıç durumum). */
  lessonGroups(withCourses = false): { code: string; label: string; lessons: LearningLesson[] }[] {
    const groups = new Map<string, LearningLesson[]>();
    for (const lesson of this.catalog()?.lessons ?? []) {
      if (withCourses && !this.included(lesson.id)) continue;
      const code = (lesson.examCode ?? '').toUpperCase() || 'OTHER';
      groups.set(code, [...(groups.get(code) ?? []), lesson]);
    }
    const order = (code: string) => LESSON_GROUPS[code]?.order ?? 50;
    return [...groups].sort(([a], [b]) => order(a) - order(b) || a.localeCompare(b))
      .map(([code, lessons]) => ({ code, label: LESSON_GROUPS[code]?.label ?? (code === 'OTHER' ? 'Diğer dersler' : code), lessons }));
  }
  /** The lessons that can be chosen (the score type's lessons with topics). */
  private choosable() { return (this.catalog()?.lessons ?? []).filter(x => x.topicCount > 0); }
  allCoursesSelected() { const all = this.choosable(); return all.length > 0 && all.every(x => this.included(x.id)); }
  selectAllCourses() {
    const missing = this.choosable().filter(x => !this.included(x.id));
    if (!missing.length) return;
    this.courses = [...this.courses, ...missing.map(x => ({ lessonId: x.id, priority: 3, selfReportedDifficulty: null }))];
    this.startingStatusReviewed = false; this.suggested.set(false); this.changed();
  }
  /**
   * F2 #28: a student with no courses yet finds the score type's courses ticked. They are only suggestions: nothing is saved
   * until "Dersleri kaydet", and leaving the page does not ask about them.
   */
  readonly suggested = signal(false);
  private suggestCourses() {
    if (this.step() !== 'Courses' || this.courses.length || this.dirty || this.state()?.context?.coursePreferences.length) return;
    const all = this.choosable();
    if (!all.length) return;
    this.courses = all.map(x => ({ lessonId: x.id, priority: 3, selfReportedDifficulty: null }));
    this.suggested.set(true);
  }
  included(id: string) { return this.courses.some(x => x.lessonId === id); }
  lessonName(id: string) { return this.catalog()?.lessons.find(x => x.id === id)?.name ?? 'Ders'; }
  toggleCourse(id: string) {
    if (this.included(id)) {
      this.courses = this.courses.filter(x => x.lessonId !== id);
      // Topic ownership is checked again on the server; clear declarations on scope removal.
      this.topics = [];
    } else this.courses = [...this.courses, { lessonId: id, priority: 3, selfReportedDifficulty: null }];
    this.startingStatusReviewed = false; this.suggested.set(false); this.changed();
  }
  saveCourses() {
    const wrong = this.courses.find(x => !Number.isInteger(Number(x.priority)) || x.priority < 1 || x.priority > 5
      || x.selfReportedDifficulty !== null && x.selfReportedDifficulty !== undefined && String(x.selfReportedDifficulty) !== ''
        && (!Number.isInteger(Number(x.selfReportedDifficulty)) || x.selfReportedDifficulty < 1 || x.selfReportedDifficulty > 5));
    if (this.refuse('Derslerin kaydedilmedi.', !this.courses.length ? { courses: 'En az bir ders seç.' }
      : wrong ? { courses: `${this.lessonName(wrong.lessonId)}: öncelik ve zorluk 1 ile 5 arasında bir tam sayı olmalı.` } : {})) {
      this.wrongCourse.set(wrong?.lessonId ?? null); return;
    }
    this.persist(this.request(), 'Starting', 'Derslerin kaydedildi.');
  }
  declaration(id: string) { return this.topics.find(x => x.topicId === id)?.declaredStatus ?? ''; }
  setDeclaration(id: string, status: TopicDeclaration | '') {
    this.topics = this.topics.filter(x => x.topicId !== id); if (status) this.topics.push({ topicId: id, declaredStatus: status }); this.changed();
  }
  /**
   * F2 review: "Bu ünitenin hepsi" marks the whole unit, also its topics on the other pages of the lesson (read for this only;
   * the page shown stays).
   */
  async markUnit(unit: string, status: TopicDeclaration) {
    const catalog = this.catalog(); if (!catalog) return;
    let topics = catalog.topics.items;
    const size = catalog.topics.pageSize || 50; const pages = Math.ceil(catalog.topics.totalCount / size);
    if (pages > 1 && this.selectedLesson) {
      try {
        const others = await Promise.all(Array.from({ length: pages }, (_, i) => i + 1).filter(page => page !== this.topicPage)
          .map(page => firstValueFrom(this.service.catalog(this.selectedLesson, page))));
        topics = [...topics, ...others.flatMap(x => x.topics.items)];
      } catch { this.error.set('Ünitenin bütün konuları yüklenemedi. Yeniden dene.'); return; }
    }
    this.setUnit(topics.filter(x => (x.unitName || 'Diğer konular') === unit), status);
  }
  /** F2 #28: one status for each of these topics. */
  setUnit(topics: StudyTopic[], status: TopicDeclaration) {
    const ids = new Set(topics.map(x => x.id));
    this.topics = [...this.topics.filter(x => !ids.has(x.topicId)), ...topics.map(x => ({ topicId: x.id, declaredStatus: status }))];
    this.changed();
  }
  /** F2 #28: saving this step is the confirmation (no "gözden geçirdim" box); the server records the step as reviewed. */
  finishStarting() {
    this.refuse('', {});
    this.startingStatusReviewed = true;
    this.persist(this.request(), 'Preview', 'Başlangıç durumun kaydedildi.');
  }
  private async canLeaveChildren() { return await this.hours?.canLeavePage() ?? true; }
  async canLeavePage() { return !this.saving() && await this.canLeaveChildren() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş kurulum seçimlerinden vazgeçilsin mi?')); }
  private readonly feedback = coachingFeedback(this.savedText, this.error);
}
