import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { CoachingStudentExamsService, LessonAnswers, StudentExam, StudentExamType } from '../../../core/services/coaching-student-exams.service';
import { CoachingStudyPlanningService, StudyTopic, TargetPage } from '../../../core/services/coaching-study-planning.service';

@Component({ selector: 'app-student-exam-results', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './student-exam-results.component.html', styleUrl: './student-study-plans.component.scss' })
export class StudentExamResultsComponent {
  private readonly service = inject(CoachingStudentExamsService);
  private readonly catalog = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  readonly page = signal<TargetPage<StudentExam>>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly topics = signal<TargetPage<StudyTopic>>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
  readonly catalogLessons = computed(() => [...new Map(this.topics().items.filter(x => x.lessonId).map(x => [x.lessonId!, x])).values()]);
  readonly busy = signal(false); readonly stale = signal(false);
  readonly error = signal<string | null>(null); readonly success = signal<string | null>(null);
  selected: StudentExam | null = null;
  title = ''; examType: StudentExamType = 'Mock'; examDate = ''; score: number | null = null; maxScore = 100;
  correctAnswers = 0; wrongAnswers = 0; emptyAnswers = 0;
  lessons: LessonAnswers[] = []; search = ''; grade: number | null = null; examCode = '';
  confirmed = false; dirty = false;
  readonly examTypes: { value: StudentExamType; label: string }[] = [
    { value: 'Mock', label: 'Deneme' }, { value: 'LGS', label: 'LGS denemesi' }, { value: 'YKS', label: 'YKS denemesi' },
    { value: 'Weekly', label: 'Haftalık' }, { value: 'Monthly', label: 'Aylık' }, { value: 'MidTerm', label: 'Ara sınav' },
    { value: 'Final', label: 'Final' }, { value: 'Quiz', label: 'Kısa sınav' } ];
  ngOnInit() { this.loadList(); }
  changed() { if (!this.busy()) { this.dirty = true; this.confirmed = false; this.success.set(null); } }
  loadList(pageNumber = 1) {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(null);
    this.service.list(pageNumber).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: page => this.page.set(page), error: () => this.error.set('Sonuçlar yüklenemedi. Yeniden deneyin.') });
  }
  searchTopics(pageNumber = 1) {
    if (this.busy()) return;
    this.busy.set(true);
    this.catalog.searchTopics(this.search, this.grade, this.examCode, pageNumber)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: page => this.topics.set(page), error: () => this.error.set('Konu listesi yüklenemedi. Seçimleriniz korundu.') });
  }
  addTopic(id: string) {
    if (this.busy() || this.lessons.length >= 50) return;
    const topic = this.topics().items.find(x => x.id === id);
    if (!topic?.lessonId || this.lessons.some(x => x.topicId === id || (x.lessonId === topic.lessonId && !x.topicId))) return;
    this.lessons.push({ lessonId: topic.lessonId, topicId: id, lessonName: topic.lessonName, topicName: topic.name,
      questionCount: 1, correct: 0, wrong: 0, empty: 1 }); this.changed();
  }
  addLesson(lessonId: string) {
    if (this.busy() || this.lessons.length >= 50 || this.lessons.some(x => x.lessonId === lessonId)) return;
    const lesson = this.catalogLessons().find(x => x.lessonId === lessonId);
    if (!lesson) return;
    this.lessons.push({ lessonId, topicId: null, lessonName: lesson.lessonName, topicName: null,
      questionCount: 1, correct: 0, wrong: 0, empty: 1 }); this.changed();
  }
  removeTopic(index: number) { if (!this.busy()) { this.lessons.splice(index, 1); this.changed(); } }
  newExam() {
    if (!this.canLeavePage()) return;
    this.selected = null; this.title = ''; this.examDate = ''; this.score = null; this.maxScore = 100;
    this.examType = 'Mock'; this.correctAnswers = this.wrongAnswers = this.emptyAnswers = 0; this.lessons = [];
    this.dirty = false; this.confirmed = false; this.stale.set(false); this.error.set(null); this.success.set(null);
  }
  open(id: string) {
    if (!this.canLeavePage()) return;
    this.busy.set(true);
    this.service.get(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: exam => this.apply(exam), error: () => this.error.set('Sonuç yüklenemedi. Mevcut formunuz korundu.') });
  }
  private apply(exam: StudentExam) {
    this.selected = exam; this.title = exam.title; this.examType = exam.examType; this.examDate = exam.examDate;
    this.score = exam.score; this.maxScore = exam.maxScore;
    this.correctAnswers = exam.correctAnswers; this.wrongAnswers = exam.wrongAnswers; this.emptyAnswers = exam.emptyAnswers;
    this.lessons = exam.lessons?.map(x => ({ ...x })) ?? []; this.confirmed = false; this.dirty = false; this.stale.set(false);
  }
  save() {
    if (this.busy() || this.stale() || !this.confirmed) return;
    const date = /^\d{4}-\d{2}-\d{2}$/.test(this.examDate) ? new Date(`${this.examDate}T00:00:00Z`) : new Date(NaN);
    const counts = this.lessons.length ? [this.lessons.reduce((a, x) => a + x.correct, 0), this.lessons.reduce((a, x) => a + x.wrong, 0), this.lessons.reduce((a, x) => a + x.empty, 0)]
      : [this.correctAnswers, this.wrongAnswers, this.emptyAnswers];
    if (!this.title.trim() || this.title.trim().length > 200 || !Number.isFinite(date.getTime())
      || date.toISOString().slice(0, 10) !== this.examDate || this.examDate <= '0001-01-01'
      || this.score === null || !Number.isFinite(this.score) || !Number.isFinite(this.maxScore)
      || this.score < 0 || this.score > this.maxScore || this.maxScore <= 0 || this.maxScore > 999.99
      || counts.some(x => !Number.isInteger(x) || x < 0) || counts.reduce((a, x) => a + x, 0) > 1000
      || this.lessons.some(x => !Number.isInteger(x.questionCount) || x.questionCount < 1 || x.questionCount > 1000
        || [x.correct, x.wrong, x.empty].some(n => !Number.isInteger(n) || n < 0) || x.correct + x.wrong + x.empty !== x.questionCount)) {
      this.error.set('Başlık, tarih, puan ve doğru/yanlış/boş sayılarını kontrol edin. Ders/konu toplamları soru sayısıyla eşleşmeli.'); return;
    }
    const input = { title: this.title.trim(), examType: this.examType, examDate: this.examDate, score: this.score,
      maxScore: this.maxScore, correctAnswers: counts[0], wrongAnswers: counts[1], emptyAnswers: counts[2], lessons: this.lessons.map(x => ({ ...x })) };
    const isNew = !this.selected;
    const write = this.selected ? this.service.replace(this.selected.id, this.selected.version, input) : this.service.create(input);
    this.busy.set(true); this.error.set(null);
    write.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: exam => {
      this.apply(exam); this.success.set('Sonucun kaydedildi. Öğrenci beyanı olarak raporlanacak.');
      this.page.update(page => ({ ...page, totalCount: page.totalCount + (isNew ? 1 : 0), items: [exam, ...page.items.filter(x => x.id !== exam.id)].slice(0, 20) }));
    }, error: error => { this.confirmed = false; if (error?.status === 409) this.stale.set(true);
      this.error.set(error?.status === 409 ? 'Sonuç değişti. Kaydı yeniden yükle; formun henüz kaydedilmedi.' : 'Sonuç kaydedilemedi. Bilgilerin korundu; kontrol edip yeniden dene.'); } });
  }
  deleteSelected() {
    if (this.busy() || this.dirty || !this.selected || !window.confirm('Bu öğrenci beyanını kalıcı olarak silmek istiyor musun?')) return;
    const exam = this.selected; this.busy.set(true);
    this.service.delete(exam.id, exam.version).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)))
      .subscribe({ next: () => { this.selected = null; this.lessons = []; this.title = ''; this.score = null; this.confirmed = false;
          this.page.update(page => ({ ...page, items: page.items.filter(x => x.id !== exam.id), totalCount: Math.max(0, page.totalCount - 1) })); this.success.set('Sonuç silindi.'); },
        error: error => { if (error?.status === 409) this.stale.set(true); this.error.set('Sonuç silinemedi. Güncel kaydı yükleyip tekrar dene.'); } });
  }
  canLeavePage() { return !this.busy() && (!this.dirty || window.confirm('Kaydedilmemiş değişiklikleri bırakmak istiyor musun?')); }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.busy() || this.dirty) { event.preventDefault(); event.returnValue = ''; }
  }
}
