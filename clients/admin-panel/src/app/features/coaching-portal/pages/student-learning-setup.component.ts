import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CoachingLearningSetupService, LearningCatalog, LearningCourse, LearningExamSession, LearningPurpose, LearningSetupUpdate, LearningSetupView, LearningTopic, TopicDeclaration } from '../../../core/services/coaching-learning-setup.service';
import { OABT_FIELDS, YOKDIL_FIELDS } from '../../../core/models/practice-exam';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { environment } from '../../../../environments/environment';
import { CoachingStudyPlanningService, PublishedStudyOutcome, StudyTopic } from '../../../core/services/coaching-study-planning.service';

@Component({
  selector: 'app-student-learning-setup', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CoachingProfileComponent, StudentStudyAvailabilityComponent],
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
  readonly steps = [{ id: 'Profile', label: 'Eğitim bilgilerim' }, { id: 'Purpose', label: 'Çalışma amacım' },
    { id: 'Availability', label: 'Müsaitliğim' }, { id: 'Courses', label: 'Derslerim' }, { id: 'Starting', label: 'Başlangıç durumum' }, { id: 'Preview', label: 'Plan önizlemesi' }];
  readonly purposes: { value: LearningPurpose; label: string }[] = [{ value: 'School', label: 'Okul derslerimi desteklemek' },
    { value: 'Lgs', label: 'LGS hazırlığı' }, { value: 'Yks', label: 'YKS hazırlığı' },
    // KM1 (01.11.2026): AGS (+ÖABT), YDS and YÖKDİL preparation; the language exams are prepared in English only.
    { value: 'Ags', label: 'AGS / ÖABT hazırlığı' }, { value: 'Yds', label: 'YDS hazırlığı' }, { value: 'Yokdil', label: 'YÖKDİL hazırlığı' },
    { value: 'General', label: 'Genel çalışma' }, { value: 'Undecided', label: 'Henüz karar vermedim' }];
  readonly oabtFields = Object.entries(OABT_FIELDS);
  readonly yokdilFields = Object.entries(YOKDIL_FIELDS);
  private static readonly examCodes: Partial<Record<LearningPurpose, string>> = { Lgs: 'LGS', Yks: 'TYT', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YOKDIL' };
  @ViewChild(CoachingProfileComponent) profile?: CoachingProfileComponent;
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  academicYearStart = new Date().getMonth() >= 8 ? new Date().getFullYear() : new Date().getFullYear() - 1;
  purpose: LearningPurpose = 'Undecided'; targetExamYear: number | null = null; primaryScoreType = 'TYT'; foreignLanguage = '';
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
      error: e => { if (e?.status === 404) this.step.set('Profile'); this.error.set(e?.status === 404 ? 'Koçluk profilin henüz hazırlanmadı. Eğitim bilgileri adımından profilini oluşturabilirsin.' : 'Kurulum bilgileri yüklenemedi. Yeniden dene.'); }
    });
  }
  private apply(state: LearningSetupView) {
    this.state.set(state); const c = state.context;
    if (c) {
      this.academicYearStart = c.academicYearStart; this.purpose = c.purpose; this.targetExamYear = c.targetExamYear;
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
    this.step.set(step); this.error.set(null);
    if (step === 'Courses' || step === 'Starting') this.loadCatalog();
  }
  changed() { this.dirty = true; }
  examPurpose() { return !!StudentLearningSetupComponent.examCodes[this.purpose]; }
  /** KM1: upcoming sessions of the purpose's exam; the student picks one only when there are several. */
  sessionChoices(): LearningExamSession[] {
    const code = StudentLearningSetupComponent.examCodes[this.purpose];
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
  savePurpose() {
    if (this.loading() || this.saving() || !this.ready()) return;
    if (!Number.isInteger(this.academicYearStart) || this.academicYearStart < 2000 || this.academicYearStart > 2200
      || this.examPurpose() && (!Number.isInteger(this.targetExamYear) || this.targetExamYear! < this.academicYearStart || this.targetExamYear! > this.academicYearStart + 5)
      || this.purpose === 'Yks' && this.primaryScoreType === 'DIL' && !this.foreignLanguage
      || this.purpose === 'Yokdil' && !this.yokdilField) {
      this.error.set('Eğitim yılını ve seçtiğin amaca ait sınav bilgilerini tamamla.'); return;
    }
    const previous = this.state()?.context; const scope = this.request([], [], false);
    const sameScope = previous && !this.state()?.missingFields.some(x => x === 'Purpose' || x === 'Catalog')
      && previous.academicYearStart === this.academicYearStart && previous.purpose === this.purpose
      && previous.primaryScoreType === scope.primaryScoreType && previous.foreignLanguage === scope.foreignLanguage;
    this.persist(this.request(sameScope ? this.courses : [], sameScope ? this.topics : [], !!sameScope && this.startingStatusReviewed), 'Availability');
  }
  private request(courses = this.courses, topics = this.topics, reviewed = this.startingStatusReviewed): LearningSetupUpdate {
    return { expectedVersion: this.state()?.context?.version ?? null, academicYearStart: this.academicYearStart, purpose: this.purpose,
      targetExamYear: this.examPurpose() ? this.targetExamYear : null,
      primaryScoreType: this.purpose === 'Yks' ? this.primaryScoreType : this.purpose === 'Ags' ? this.oabtField || null : this.purpose === 'Yokdil' ? this.yokdilField || null : null,
      foreignLanguage: this.purpose === 'Yks' && this.primaryScoreType === 'DIL' ? this.foreignLanguage : this.purpose === 'Yds' || this.purpose === 'Yokdil' ? 'English' : null,
      targetExamSessionId: this.sessionChoices().some(x => x.id === this.targetExamSessionId) ? this.targetExamSessionId : null,
      courses, topics, startingStatusReviewed: reviewed };
  }
  private persist(request: LearningSetupUpdate, next: string) {
    if (this.saving() || !this.ready()) return;
    this.saving.set(true); this.error.set(null);
    this.service.save(request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: state => { this.apply(state); this.step.set(next); if (next === 'Starting' || next === 'Courses') this.loadCatalog(); },
      error: e => this.error.set(e?.status === 409 ? 'Kurulum başka bir oturumda değişti. Seçimlerin korundu; güncel bilgileri yeniden yükle.' : e?.error?.message ?? 'Kurulum kaydedilemedi. Seçimlerin ekranda korundu.')
    });
  }
  async afterProfileSaved() { await this.load(); }
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
  saveCourses() { if (!this.courses.length) { this.error.set('En az bir ders seç.'); return; } this.persist(this.request(), 'Starting'); }
  declaration(id: string) { return this.topics.find(x => x.topicId === id)?.declaredStatus ?? ''; }
  setDeclaration(id: string, status: TopicDeclaration | '') {
    this.topics = this.topics.filter(x => x.topicId !== id); if (status) this.topics.push({ topicId: id, declaredStatus: status }); this.changed();
  }
  finishStarting() { if (!this.startingStatusReviewed) { this.error.set('Başlangıç bilgilerini gözden geçirdiğini onayla.'); return; } this.persist(this.request(), 'Preview'); }
  private async canLeaveChildren() { return (await this.profile?.canLeavePage() ?? true) && (await this.hours?.canLeavePage() ?? true); }
  async canLeavePage() { return !this.saving() && await this.canLeaveChildren() && (!this.dirty || await this.toaster.confirm('Kaydedilmemiş kurulum seçimlerinden vazgeçilsin mi?')); }
  private readonly feedback = coachingFeedback(undefined, this.error);
}
