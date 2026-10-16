import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CoachingLearningSetupService, LearningCatalog, LearningCourse, LearningPurpose, LearningSetupUpdate, LearningSetupView, LearningTopic, TopicDeclaration } from '../../../core/services/coaching-learning-setup.service';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { StudentAutomaticPlanComponent } from './student-automatic-plan.component';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-student-learning-setup', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CoachingProfileComponent, StudentStudyAvailabilityComponent, StudentAutomaticPlanComponent],
  templateUrl: './student-learning-setup.component.html', styleUrl: './student-study-plans.component.scss'
})
export class StudentLearningSetupComponent implements OnInit {
  private readonly service = inject(CoachingLearningSetupService);
  private readonly destroyRef = inject(DestroyRef);
  readonly apiUrl = environment.apiUrl;
  readonly state = signal<LearningSetupView | null>(null); readonly step = signal('Purpose');
  readonly loading = signal(false); readonly saving = signal(false); readonly ready = signal(false);
  readonly error = signal<string | null>(null); readonly catalogError = signal<string | null>(null); readonly catalogLoading = signal(false);
  readonly catalog = signal<LearningCatalog | null>(null);
  readonly steps = [{ id: 'Profile', label: 'Eğitim bilgilerim' }, { id: 'Purpose', label: 'Çalışma amacım' },
    { id: 'Availability', label: 'Müsaitliğim' }, { id: 'Courses', label: 'Derslerim' }, { id: 'Starting', label: 'Başlangıç durumum' }, { id: 'Preview', label: 'Plan önizlemesi' }];
  readonly purposes: { value: LearningPurpose; label: string }[] = [{ value: 'School', label: 'Okul derslerimi desteklemek' },
    { value: 'Lgs', label: 'LGS hazırlığı' }, { value: 'Yks', label: 'YKS hazırlığı' }, { value: 'General', label: 'Genel çalışma' }, { value: 'Undecided', label: 'Henüz karar vermedim' }];
  @ViewChild(CoachingProfileComponent) profile?: CoachingProfileComponent;
  @ViewChild(StudentStudyAvailabilityComponent) hours?: StudentStudyAvailabilityComponent;
  @ViewChild(StudentAutomaticPlanComponent) preview?: StudentAutomaticPlanComponent;
  academicYearStart = new Date().getMonth() >= 8 ? new Date().getFullYear() : new Date().getFullYear() - 1;
  purpose: LearningPurpose = 'Undecided'; targetExamYear: number | null = null; primaryScoreType = 'TYT'; foreignLanguage = '';
  courses: LearningCourse[] = []; topics: LearningTopic[] = []; startingStatusReviewed = false;
  selectedLesson = ''; topicPage = 1; dirty = false;
  ngOnInit() { this.load(); }
  load() {
    if (this.loading() || this.saving()) return;
    if (this.dirty && !window.confirm('Kaydedilmemiş seçimlerden vazgeçip sunucudaki bilgileri yükleyelim mi?')) return;
    this.loading.set(true); this.ready.set(false); this.error.set(null);
    this.service.get().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: state => { this.apply(state); this.ready.set(true); this.step.set(state.nextStep === 'Catalog' ? 'Courses' : state.nextStep); if (this.step() === 'Courses' || this.step() === 'Starting') this.loadCatalog(); },
      error: e => { if (e?.status === 404) this.step.set('Profile'); this.error.set(e?.status === 404 ? 'Koçluk profiliniz henüz hazırlanmadı. Eğitim bilgileri adımından profilinizi oluşturabilirsiniz.' : 'Kurulum bilgileri yüklenemedi. Yeniden deneyin.'); }
    });
  }
  private apply(state: LearningSetupView) {
    this.state.set(state); const c = state.context;
    if (c) {
      this.academicYearStart = c.academicYearStart; this.purpose = c.purpose; this.targetExamYear = c.targetExamYear;
      this.primaryScoreType = c.primaryScoreType ?? 'TYT'; this.foreignLanguage = c.foreignLanguage ?? '';
      this.courses = c.coursePreferences.map(x => ({ ...x })); this.topics = c.topicStates.map(x => ({ ...x })); this.startingStatusReviewed = c.startingStatusReviewed;
    }
    this.dirty = false;
  }
  goTo(step: string) {
    if (this.saving() || this.loading() || this.catalogLoading()) return;
    if (!this.canLeaveChildren()) return;
    if (this.dirty) { this.error.set('Adım değiştirmeden önce seçimlerinizi kaydedin veya yeniden yükleyin.'); return; }
    this.step.set(step); this.error.set(null);
    if (step === 'Courses' || step === 'Starting') this.loadCatalog();
  }
  changed() { this.dirty = true; }
  savePurpose() {
    if (this.loading() || this.saving() || !this.ready()) return;
    if (!Number.isInteger(this.academicYearStart) || this.academicYearStart < 2000 || this.academicYearStart > 2200
      || (this.purpose === 'Lgs' || this.purpose === 'Yks') && (!Number.isInteger(this.targetExamYear) || this.targetExamYear! < this.academicYearStart || this.targetExamYear! > this.academicYearStart + 5)
      || this.purpose === 'Yks' && this.primaryScoreType === 'DIL' && !this.foreignLanguage) {
      this.error.set('Eğitim yılını ve seçtiğiniz amaca ait sınav bilgilerini tamamlayın.'); return;
    }
    const previous = this.state()?.context;
    const sameScope = previous && previous.academicYearStart === this.academicYearStart && previous.purpose === this.purpose
      && previous.primaryScoreType === (this.purpose === 'Yks' ? this.primaryScoreType : null)
      && previous.foreignLanguage === (this.purpose === 'Yks' && this.primaryScoreType === 'DIL' ? this.foreignLanguage : null);
    this.persist(this.request(sameScope ? this.courses : [], sameScope ? this.topics : [], !!sameScope && this.startingStatusReviewed), 'Availability');
  }
  private request(courses = this.courses, topics = this.topics, reviewed = this.startingStatusReviewed): LearningSetupUpdate {
    return { expectedVersion: this.state()?.context?.version ?? null, academicYearStart: this.academicYearStart, purpose: this.purpose,
      targetExamYear: this.purpose === 'Lgs' || this.purpose === 'Yks' ? this.targetExamYear : null,
      primaryScoreType: this.purpose === 'Yks' ? this.primaryScoreType : null,
      foreignLanguage: this.purpose === 'Yks' && this.primaryScoreType === 'DIL' ? this.foreignLanguage : null,
      courses, topics, startingStatusReviewed: reviewed };
  }
  private persist(request: LearningSetupUpdate, next: string) {
    if (this.saving() || !this.ready()) return;
    this.saving.set(true); this.error.set(null);
    this.service.save(request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: state => { this.apply(state); this.step.set(next); if (next === 'Starting' || next === 'Courses') this.loadCatalog(); },
      error: e => this.error.set(e?.status === 409 ? 'Kurulum başka bir oturumda değişti. Seçimleriniz korundu; güncel bilgileri yeniden yükleyin.' : e?.error?.message ?? 'Kurulum kaydedilemedi. Seçimleriniz ekranda korundu.')
    });
  }
  afterProfileSaved() { this.load(); }
  afterHoursSaved() { this.load(); }
  loadCatalog(page = 1) {
    if (this.catalogLoading()) return;
    this.catalogLoading.set(true); this.catalogError.set(null);
    this.service.catalog(this.selectedLesson || null, page).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.catalogLoading.set(false))).subscribe({
      next: catalog => { this.catalog.set(catalog); this.topicPage = page; }, error: () => this.catalogError.set('Ders kataloğu yüklenemedi. Tekrar deneyin.')
    });
  }
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
  saveCourses() { if (!this.courses.length) { this.error.set('En az bir ders seçin.'); return; } this.persist(this.request(), 'Starting'); }
  declaration(id: string) { return this.topics.find(x => x.topicId === id)?.declaredStatus ?? ''; }
  setDeclaration(id: string, status: TopicDeclaration | '') {
    this.topics = this.topics.filter(x => x.topicId !== id); if (status) this.topics.push({ topicId: id, declaredStatus: status }); this.changed();
  }
  finishStarting() { if (!this.startingStatusReviewed) { this.error.set('Başlangıç bilgilerini gözden geçirdiğinizi onaylayın.'); return; } this.persist(this.request(), 'Preview'); }
  private canLeaveChildren() { return (this.profile?.canLeavePage() ?? true) && (this.hours?.canLeavePage() ?? true) && (this.preview?.canLeavePage() ?? true); }
  canLeavePage() { return !this.saving() && this.canLeaveChildren() && (!this.dirty || window.confirm('Kaydedilmemiş kurulum seçimlerinden vazgeçilsin mi?')); }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(e: BeforeUnloadEvent) { if (this.dirty || this.saving()) { e.preventDefault(); e.returnValue = ''; } }
}
