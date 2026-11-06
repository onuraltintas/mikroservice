import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CoachingLearningSetupService, ExamLearningPurpose, LearningCatalog, LearningCourse, LearningExamSession, LearningSetupImpact, LearningSetupUpdate, LearningSetupView, LearningTopic, RETIRED_PURPOSE_NOTICE, TopicDeclaration, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';

type SetupField = 'purpose' | 'academicYear' | 'examYear' | 'language' | 'yokdilField' | 'courses';
/** E4 #26: the confirmation after a save (it stays when the page moves on to the next section). */
interface SetupSaved { text: string; removed: string[]; diploma: number | null; teacher: boolean }
const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
import { OABT_FIELDS, YOKDIL_FIELDS } from '../../../core/models/practice-exam';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { environment } from '../../../../environments/environment';
import { CoachingStudyPlanningService, PublishedStudyOutcome, StudyTopic } from '../../../core/services/coaching-study-planning.service';

@Component({
  selector: 'app-student-learning-setup', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StudentStudyAvailabilityComponent],
  templateUrl: './student-learning-setup.component.html', styleUrl: './student-study-plans.component.scss'
})
export class StudentLearningSetupComponent implements OnInit {
  private readonly toaster = coachingToaster();
  private readonly service = inject(CoachingLearningSetupService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
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
  readonly steps = [{ id: 'Purpose', label: 'Çalışma amacım' },
    { id: 'Availability', label: 'Müsaitliğim' }, { id: 'Courses', label: 'Derslerim' }, { id: 'Starting', label: 'Başlangıç durumum' }, { id: 'Preview', label: 'Plan önizlemesi' }];
  // KM8 (01.11.2026): only exam purposes; the catalog keeps only exam lessons. KM1 added AGS (+ÖABT), YDS and YÖKDİL
  // (the language exams are prepared in English only).
  readonly purposes: { value: ExamLearningPurpose; label: string }[] = [
    { value: 'Lgs', label: 'LGS hazırlığı' }, { value: 'Yks', label: 'YKS hazırlığı' },
    { value: 'Ags', label: 'AGS / ÖABT hazırlığı' }, { value: 'Yds', label: 'YDS hazırlığı' }, { value: 'Yokdil', label: 'YÖKDİL hazırlığı' }];
  readonly retiredPurposeNotice = RETIRED_PURPOSE_NOTICE;
  /** Paket D (#8): only the exams that fit the grade on the profile (every exam when the server does not say). */
  purposeChoices() { const allowed = this.state()?.allowedPurposes; return allowed ? this.purposes.filter(x => allowed.includes(x.value)) : this.purposes; }
  readonly oabtFields = Object.entries(OABT_FIELDS);
  readonly yokdilFields = Object.entries(YOKDIL_FIELDS);
  private static readonly examCodes: Record<ExamLearningPurpose, string> = { Lgs: 'LGS', Yks: 'TYT', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YOKDIL' };
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  academicYearStart = new Date().getMonth() >= 8 ? new Date().getFullYear() : new Date().getFullYear() - 1;
  purpose: ExamLearningPurpose | '' = ''; targetExamYear: number | null = null; primaryScoreType = 'TYT'; foreignLanguage = '';
  targetExamSessionId: string | null = null; oabtField = ''; yokdilField = '';
  courses: LearningCourse[] = []; topics: LearningTopic[] = []; startingStatusReviewed = false;
  readonly expandedTopicId = signal<string | null>(null);
  readonly topicOutcomes = signal<Record<string, PublishedStudyOutcome[]>>({});
  readonly topicOutcomeLoading = signal<string | null>(null);
  readonly topicOutcomeErrors = signal<Record<string, boolean>>({});
  selectedLesson = ''; topicPage = 1; dirty = false;
  async ngOnInit() { await this.load(); }
  async load() {
    if (this.loading() || this.saving()) return;
    if (this.dirty && !await this.toaster.confirm('Kaydedilmemiş seçimlerden vazgeçip sunucudaki bilgileri yükleyelim mi?')) return;
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
      this.courses = c.coursePreferences.map(x => ({ ...x })); this.topics = c.topicStates.map(x => ({ ...x })); this.startingStatusReviewed = c.startingStatusReviewed;
    }
    this.dirty = false;
  }
  async goTo(step: string) {
    if (this.saving() || this.loading() || this.catalogLoading()) return;
    if (!await this.canLeaveChildren()) return;
    if (this.dirty) { this.error.set('Adım değiştirmeden önce seçimlerini kaydet veya yeniden yükle.'); return; }
    this.step.set(step); this.error.set(null); this.fieldErrors.set({}); this.cancelImpact();
    if (step === 'Courses' || step === 'Starting') this.loadCatalog();
  }
  changed() { this.dirty = true; }
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
  private refuse(errors: Partial<Record<SetupField, string>>): boolean {
    this.error.set(null); this.saved.set(null); this.fieldErrors.set(errors); this.cancelImpact();
    const messages = Object.values(errors);
    if (!messages.length) return false;
    this.error.set(`Kaydedilmedi. ${messages.join(' ')}`);
    return true;
  }

  /**
   * E4 #26: a new exam, field or language may remove parts of the student's own target (PR 114) and the diploma grade; the server
   * says which before saving and the student confirms in the page. A teacher's goal is kept (the confirmation shows the mismatch).
   */
  savePurpose() {
    if (this.loading() || this.saving() || !this.ready()) return;
    const errors: Partial<Record<SetupField, string>> = {};
    if (!this.purpose) errors.purpose = 'Hazırlandığın sınavı seç.';
    if (!Number.isInteger(this.academicYearStart) || this.academicYearStart < 2000 || this.academicYearStart > 2200)
      errors.academicYear = 'Eğitim yılı başlangıcı 2000 ile 2200 arasında olmalı.';
    else if (this.examPurpose() && (!Number.isInteger(this.targetExamYear) || this.targetExamYear! < this.academicYearStart || this.targetExamYear! > this.academicYearStart + 5))
      errors.examYear = `Hedef sınav yılı ${this.academicYearStart} ile ${this.academicYearStart + 5} arasında olmalı.`;
    if (this.purpose === 'Yks' && this.primaryScoreType === 'DIL' && !this.foreignLanguage) errors.language = 'Yabancı dili seç.';
    if (this.purpose === 'Yokdil' && !this.yokdilField) errors.yokdilField = 'YÖKDİL alanını seç.';
    if (this.refuse(errors)) return;
    const previous = this.state()?.context; const scope = this.request([], [], false);
    const sameScope = previous && !this.state()?.missingFields.some(x => x === 'Purpose' || x === 'Catalog')
      && previous.academicYearStart === this.academicYearStart && previous.purpose === this.purpose
      && previous.primaryScoreType === scope.primaryScoreType && previous.foreignLanguage === scope.foreignLanguage;
    const request = this.request(sameScope ? this.courses : [], sameScope ? this.topics : [], !!sameScope && this.startingStatusReviewed);
    const scopeChanged = !!previous && (previous.purpose !== scope.purpose || (previous.primaryScoreType ?? null) !== scope.primaryScoreType
      || (previous.foreignLanguage ?? null) !== scope.foreignLanguage);
    if (!scopeChanged) { this.persist(request, 'Availability', 'Çalışma amacın kaydedildi.'); return; }
    this.saving.set(true);
    this.service.impact({ purpose: scope.purpose, primaryScoreType: scope.primaryScoreType, foreignLanguage: scope.foreignLanguage })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: impact => {
          this.saving.set(false);
          const lines = this.impactLines(impact, previous!.purpose === scope.purpose);
          if (lines.length) { this.pendingPurpose = { request, impact }; this.impactAsk.set(lines); }
          else this.persist(request, 'Availability', 'Çalışma amacın kaydedildi.', impact);
        },
        error: e => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Değişikliğin hedefine etkisi kontrol edilemedi. Bağlantını kontrol edip yeniden dene.'); }
      });
  }
  confirmImpact() {
    const pending = this.pendingPurpose; if (!pending) return;
    this.cancelImpact(); this.persist(pending.request, 'Availability', 'Çalışma amacın kaydedildi.', pending.impact);
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
      next: catalog => { this.catalog.set(catalog); this.topicPage = page; }, error: () => this.catalogError.set('Ders kataloğu yüklenemedi. Tekrar dene.')
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
  included(id: string) { return this.courses.some(x => x.lessonId === id); }
  lessonName(id: string) { return this.catalog()?.lessons.find(x => x.id === id)?.name ?? 'Ders'; }
  toggleCourse(id: string) {
    if (this.included(id)) {
      this.courses = this.courses.filter(x => x.lessonId !== id);
      // Topic ownership is checked again on the server; clear declarations on scope removal.
      this.topics = [];
    } else this.courses = [...this.courses, { lessonId: id, priority: 3, selfReportedDifficulty: null }];
    this.startingStatusReviewed = false; this.changed();
  }
  saveCourses() {
    const wrong = this.courses.find(x => !Number.isInteger(Number(x.priority)) || x.priority < 1 || x.priority > 5
      || x.selfReportedDifficulty !== null && x.selfReportedDifficulty !== undefined && String(x.selfReportedDifficulty) !== ''
        && (!Number.isInteger(Number(x.selfReportedDifficulty)) || x.selfReportedDifficulty < 1 || x.selfReportedDifficulty > 5));
    if (this.refuse(!this.courses.length ? { courses: 'En az bir ders seç.' }
      : wrong ? { courses: `${this.lessonName(wrong.lessonId)}: öncelik ve zorluk 1 ile 5 arasında bir tam sayı olmalı.` } : {})) return;
    this.persist(this.request(), 'Starting', 'Derslerin kaydedildi.');
  }
  declaration(id: string) { return this.topics.find(x => x.topicId === id)?.declaredStatus ?? ''; }
  setDeclaration(id: string, status: TopicDeclaration | '') {
    this.topics = this.topics.filter(x => x.topicId !== id); if (status) this.topics.push({ topicId: id, declaredStatus: status }); this.changed();
  }
  finishStarting() {
    if (this.refuse(this.startingStatusReviewed ? {} : { courses: 'Başlangıç bilgilerini gözden geçirdiğini onayla.' })) return;
    this.persist(this.request(), 'Preview', 'Başlangıç durumun kaydedildi.');
  }
  private async canLeaveChildren() { return await this.hours?.canLeavePage() ?? true; }
  async canLeavePage() { return !this.saving() && await this.canLeaveChildren() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş kurulum seçimlerinden vazgeçilsin mi?')); }
  private readonly feedback = coachingFeedback(this.savedText, this.error);
}
