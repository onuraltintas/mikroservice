import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, map } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudent, CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import {
  CoachingTeacherExam,
  CoachingTeacherExamDetail,
  CoachingTeacherExamResult,
  CoachingTeacherExamCreateRequest,
  CoachingTeacherExamUpdateRequest,
  CoachingTeacherExamResultRequest,
  CoachingTeacherExamsService
} from './coaching-teacher-exams.service';

interface ExamForm {
  title: string;
  type: number;
  examDate: string;
  maxScore: number;
  subject: string;
  description: string;
  durationMinutes: number | undefined;
  targetGradeLevel: number | undefined;
}

interface ExamResultForm {
  studentId: string;
  score: number;
  correctAnswers: number;
  wrongAnswers: number;
  emptyAnswers: number;
  ranking: number | undefined;
  notes: string;
  subjectScoresText: string;
}

@Component({
  selector: 'staff-coaching-teacher-exams',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-exams.component.html',
  styleUrl: './coaching-teacher-exams.component.scss'
})
export class CoachingTeacherExamsComponent implements OnInit {
  private readonly authService = inject(StaffAuthService);
  private readonly examsService = inject(CoachingTeacherExamsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);
  private studentsRequestId = 0;
  private resultsRequestId = 0;

  readonly exams = signal<CoachingTeacherExam[]>([]);
  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly studentNames = signal<Record<string, string>>({});
  readonly studentNameLookupFailed = signal(false);
  readonly isLoading = signal(true);
  readonly loadingMoreExams = signal(false);
  readonly isLoadingStudents = signal(false);
  readonly isLoadingResults = signal(false);
  readonly loadingMoreResults = signal(false);
  readonly isSavingExam = signal(false);
  readonly isSavingResult = signal(false);
  readonly isExamFormVisible = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingExamId = signal<string | null>(null);
  readonly selectedExam = signal<CoachingTeacherExam | null>(null);
  readonly examDetail = signal<CoachingTeacherExamDetail | null>(null);
  readonly editingResultId = signal<string | null>(null);
  readonly examPageNumber = signal(1);
  readonly examTotalPages = signal(1);
  readonly examTotalCount = signal(0);
  readonly resultPageNumber = signal(1);
  readonly resultTotalPages = signal(1);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');

  readonly examTypes = [
    { value: 1, key: 'Mock', label: 'Deneme' },
    { value: 2, key: 'Weekly', label: 'Haftalık test' },
    { value: 3, key: 'Monthly', label: 'Aylık değerlendirme' },
    { value: 4, key: 'LGS', label: 'LGS' },
    { value: 5, key: 'YKS', label: 'YKS' },
    { value: 6, key: 'MidTerm', label: 'Ara sınav' },
    { value: 7, key: 'Final', label: 'Final' },
    { value: 8, key: 'Quiz', label: 'Kısa sınav' }
  ];

  form = this.emptyExamForm();
  resultForm = this.emptyResultForm();

  ngOnInit(): void {
    if (!this.authService.getCurrentUserId()) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    this.loadExams();
    this.loadStudents();
  }

  loadExams(append = false): void {
    const teacherId = this.authService.getCurrentUserId();
    if (!teacherId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    const pageNumber = append ? this.examPageNumber() + 1 : 1;
    const loading = append ? this.loadingMoreExams : this.isLoading;
    loading.set(true);
    this.errorMessage.set(null);
    this.examsService.getTeacherExams(teacherId, pageNumber, 25).pipe(
      finalize(() => loading.set(false))
    ).subscribe({
      next: page => {
        this.exams.update(current => append ? [...current, ...page.items] : page.items);
        this.examPageNumber.set(page.pageNumber);
        this.examTotalCount.set(page.totalCount);
        this.examTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Sınavlar yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  loadMoreExams(): void {
    if (!this.loadingMoreExams() && this.examPageNumber() < this.examTotalPages()) this.loadExams(true);
  }

  loadStudents(append = false): void {
    const requestId = ++this.studentsRequestId;
    const pageNumber = append ? this.studentPageNumber() + 1 : 1;
    this.isLoadingStudents.set(true);
    this.studentsService.getMyStudents(pageNumber, 100, this.studentSearchTerm() || undefined).pipe(
      finalize(() => {
        if (requestId === this.studentsRequestId) this.isLoadingStudents.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestId !== this.studentsRequestId) return;
        const retained = append ? this.students() : this.students().filter(student => student.userId === this.resultForm.studentId);
        const merged = [...new Map([...retained, ...page.items].map(student => [student.userId, student])).values()];
        this.students.set(merged);
        this.studentNames.update(names => ({
          ...names,
          ...Object.fromEntries(page.items.map(student => [student.userId, student.fullName]))
        }));
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        if (requestId === this.studentsRequestId) this.errorMessage.set('Aktif öğrenci listeniz yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  searchStudents(value: string): void {
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  loadMoreStudents(): void {
    if (!this.isLoadingStudents() && this.studentPageNumber() < this.studentTotalPages()) this.loadStudents(true);
  }

  createExam(): void {
    this.editingExamId.set(null);
    this.form = this.emptyExamForm();
    this.isExamFormVisible.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  saveExam(): void {
    const teacherId = this.authService.getCurrentUserId();
    const title = this.form.title.trim();
    const examDate = new Date(this.form.examDate);
    if (!teacherId || !title || title.length > 200) {
      this.errorMessage.set('Sınav başlığı zorunludur ve 200 karakteri aşamaz.');
      return;
    }
    if (!this.examTypes.some(type => type.value === this.form.type)) {
      this.errorMessage.set('Geçerli bir sınav türü seçin.');
      return;
    }
    const examId = this.editingExamId();
    if (Number.isNaN(examDate.getTime()) || (!examId && examDate <= new Date())) {
      this.errorMessage.set('Sınav tarihi gelecekte olmalıdır.');
      return;
    }
    if (!Number.isFinite(this.form.maxScore) || this.form.maxScore < 0.01 || this.form.maxScore > 999.99) {
      this.errorMessage.set('Maksimum puan 0,01 ile 999,99 arasında olmalıdır.');
      return;
    }
    if (this.form.description.length > 2_000 || this.form.subject.length > 200) {
      this.errorMessage.set('Açıklama veya ders alanı izin verilen karakter sınırını aşıyor.');
      return;
    }

    if (examId && this.form.durationMinutes !== undefined
      && (!Number.isInteger(this.form.durationMinutes) || this.form.durationMinutes < 1 || this.form.durationMinutes > 480)) {
      this.errorMessage.set('Sınav süresi 1 ile 480 dakika arasında tam sayı olmalıdır.');
      return;
    }
    if (examId && this.form.targetGradeLevel !== undefined
      && (!Number.isInteger(this.form.targetGradeLevel) || this.form.targetGradeLevel < 1 || this.form.targetGradeLevel > 12)) {
      this.errorMessage.set('Sınıf seviyesi 1 ile 12 arasında tam sayı olmalıdır.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingExam.set(true);
    const normalizedDate = examDate.toISOString();
    const operation = examId
      ? this.examsService.updateExam(examId, {
        examId,
        title,
        type: this.form.type,
        subject: this.form.subject || null,
        description: this.form.description || null,
        examDate: normalizedDate,
        durationMinutes: this.form.durationMinutes ?? null,
        maxScore: this.form.maxScore,
        targetGradeLevel: this.form.targetGradeLevel ?? null
      } satisfies CoachingTeacherExamUpdateRequest)
      : this.examsService.createExam({
        teacherId,
        title,
        type: this.form.type,
        examDate: normalizedDate,
        maxScore: this.form.maxScore,
        description: this.form.description || null
      } satisfies CoachingTeacherExamCreateRequest, this.idempotencyKey('exam'));

    operation.pipe(finalize(() => this.isSavingExam.set(false))).subscribe({
      next: () => {
        this.successMessage.set(examId ? 'Sınav güncellendi.' : 'Sınav oluşturuldu.');
        this.cancelExamEdit();
        this.loadExams();
      },
      error: () => this.errorMessage.set('Sınav kaydedilemedi. Alanları ve öğretmen yetkinizi kontrol edin.')
    });
  }

  editExam(exam: CoachingTeacherExam): void {
    this.editingExamId.set(exam.id);
    this.isExamFormVisible.set(true);
    this.form = {
      title: exam.title,
      type: this.examTypes.find(type => type.key === exam.examType)?.value ?? 1,
      examDate: this.toLocalDateTime(exam.examDate),
      maxScore: exam.maxScore,
      subject: exam.subject ?? '',
      description: exam.description ?? '',
      durationMinutes: exam.durationMinutes ?? undefined,
      targetGradeLevel: exam.targetGradeLevel ?? undefined
    };
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  cancelExamEdit(): void {
    this.editingExamId.set(null);
    this.form = this.emptyExamForm();
    this.isExamFormVisible.set(false);
  }

  openResults(exam: CoachingTeacherExam): void {
    this.loadingMoreResults.set(false);
    this.selectedExam.set(exam);
    this.examDetail.set(null);
    this.resultPageNumber.set(1);
    this.resultTotalPages.set(1);
    this.cancelExamResultEdit();
    this.errorMessage.set(null);
    this.loadResults(exam, 1, false);
  }

  closeResults(): void {
    this.resultsRequestId++;
    this.selectedExam.set(null);
    this.examDetail.set(null);
    this.isLoadingResults.set(false);
    this.loadingMoreResults.set(false);
    this.cancelExamResultEdit();
  }

  loadMoreResults(): void {
    const exam = this.selectedExam();
    if (!exam || this.isLoadingResults() || this.resultPageNumber() >= this.resultTotalPages()) return;
    this.loadResults(exam, this.resultPageNumber() + 1, true);
  }

  editExamResult(result: CoachingTeacherExamResult): void {
    this.editingResultId.set(result.id);
    this.ensureStudentOption(result.studentId);
    this.resultForm = {
      studentId: result.studentId,
      score: result.score,
      correctAnswers: result.correctAnswers ?? 0,
      wrongAnswers: result.wrongAnswers ?? 0,
      emptyAnswers: result.emptyAnswers ?? 0,
      ranking: result.ranking ?? undefined,
      notes: result.teacherNotes ?? '',
      subjectScoresText: result.subjectScores ? JSON.stringify(result.subjectScores, null, 2) : ''
    };
  }

  cancelExamResultEdit(): void {
    this.editingResultId.set(null);
    this.resultForm = this.emptyResultForm();
  }

  saveExamResult(): void {
    const exam = this.selectedExam();
    const resultId = this.editingResultId();
    if (!exam || (!resultId && !this.resultForm.studentId)) {
      this.errorMessage.set('Yeni sonuç için aktif öğrencilerinizden birini seçin.');
      return;
    }
    if (!Number.isFinite(this.resultForm.score) || this.resultForm.score < 0 || this.resultForm.score > exam.maxScore) {
      this.errorMessage.set(`Puan 0 ile ${exam.maxScore} arasında olmalıdır.`);
      return;
    }
    const answers = [this.resultForm.correctAnswers, this.resultForm.wrongAnswers, this.resultForm.emptyAnswers];
    if (answers.some(value => !Number.isInteger(value) || value < 0)) {
      this.errorMessage.set('Doğru, yanlış ve boş cevap sayıları negatif olmayan tam sayı olmalıdır.');
      return;
    }
    if (this.resultForm.ranking !== undefined
      && (!Number.isInteger(this.resultForm.ranking) || this.resultForm.ranking < 1)) {
      this.errorMessage.set('Sıralama 1 veya daha büyük bir tam sayı olmalıdır.');
      return;
    }
    if (this.resultForm.notes.length > 2_000) {
      this.errorMessage.set('Öğretmen notu 2.000 karakteri aşamaz.');
      return;
    }
    const subjectScores = this.parseSubjectScores(this.resultForm.subjectScoresText, exam.maxScore);
    if (subjectScores === undefined) return;

    const request: CoachingTeacherExamResultRequest = {
      examId: exam.id,
      ...(resultId ? { resultId } : { studentId: this.resultForm.studentId }),
      score: this.resultForm.score,
      correctAnswers: this.resultForm.correctAnswers,
      wrongAnswers: this.resultForm.wrongAnswers,
      emptyAnswers: this.resultForm.emptyAnswers,
      subjectScores,
      ranking: this.resultForm.ranking ?? null,
      notes: this.resultForm.notes.trim() || null
    };
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingResult.set(true);
    const operation = resultId
      ? this.examsService.updateExamResult(exam.id, resultId, request).pipe(map(() => undefined))
      : this.examsService.addExamResult(exam.id, request, this.idempotencyKey('exam-result')).pipe(map(() => undefined));
    operation.pipe(finalize(() => this.isSavingResult.set(false))).subscribe({
      next: () => {
        this.successMessage.set(resultId ? 'Sınav sonucu güncellendi.' : 'Sınav sonucu eklendi.');
        this.cancelExamResultEdit();
        this.openResults(exam);
        this.loadExams();
      },
      error: () => this.errorMessage.set('Sınav sonucu kaydedilemedi. Puanı, öğrenci ilişkisini ve yetkinizi kontrol edin.')
    });
  }

  studentLabel(studentId: string): string {
    return this.studentNames()[studentId]
      ?? (this.studentNameLookupFailed() ? 'Öğrenci adı alınamadı' : `Öğrenci ${studentId.slice(-8)}`);
  }

  examTypeLabel(type: string): string {
    return this.examTypes.find(item => item.key === type)?.label ?? type;
  }

  trackById(_: number, item: CoachingTeacherExam | CoachingTeacherExamResult): string {
    return item.id;
  }

  private loadResults(exam: CoachingTeacherExam, pageNumber: number, append: boolean): void {
    const requestId = ++this.resultsRequestId;
    const loading = append ? this.loadingMoreResults : this.isLoadingResults;
    loading.set(true);
    this.examsService.getExamDetail(exam.id, pageNumber, 25).pipe(
      finalize(() => {
        if (requestId === this.resultsRequestId) loading.set(false);
      })
    ).subscribe({
      next: detail => {
        if (requestId !== this.resultsRequestId || this.selectedExam()?.id !== exam.id) return;
        const previous = this.examDetail();
        this.examDetail.set(append && previous ? { ...detail, results: [...previous.results, ...detail.results] } : detail);
        this.resultPageNumber.set(detail.resultPageNumber);
        this.resultTotalPages.set(detail.resultTotalPages);
        this.loadStudentNames(detail.results);
      },
      error: () => {
        if (requestId === this.resultsRequestId) this.errorMessage.set('Sınav sonuçları yüklenemedi. Lütfen tekrar deneyin.');
      }
    });
  }

  private loadStudentNames(results: readonly CoachingTeacherExamResult[]): void {
    const ids = [...new Set(results.map(result => result.studentId))]
      .filter(studentId => !this.studentNames()[studentId]);
    if (ids.length === 0) return;
    const batches: string[][] = [];
    for (let index = 0; index < ids.length; index += 100) batches.push(ids.slice(index, index + 100));
    forkJoin(batches.map(batch => this.studentsService.getMyStudents(1, batch.length, undefined, batch))).subscribe({
      next: pages => {
        const resolved = pages.flatMap(page => page.items);
        const resolvedIds = new Set(resolved.map(student => student.userId));
        this.studentNameLookupFailed.set(ids.some(id => !resolvedIds.has(id)));
        this.students.set([...new Map([...this.students(), ...resolved].map(student => [student.userId, student])).values()]);
        this.studentNames.update(names => ({
          ...names,
          ...Object.fromEntries(resolved.map(student => [student.userId, student.fullName]))
        }));
      },
      error: () => this.studentNameLookupFailed.set(true)
    });
  }

  private ensureStudentOption(studentId: string): void {
    if (!studentId || this.students().some(student => student.userId === studentId)) return;
    this.studentsService.getMyStudents(1, 1, undefined, [studentId]).subscribe({
      next: page => {
        const student = page.items.find(item => item.userId === studentId);
        if (!student) {
          this.studentNameLookupFailed.set(true);
          return;
        }
        this.students.update(current => [...current, student]);
        this.studentNames.update(names => ({ ...names, [student.userId]: student.fullName }));
      },
      error: () => this.studentNameLookupFailed.set(true)
    });
  }

  private parseSubjectScores(value: string, maxScore: number): Record<string, number> | null | undefined {
    if (!value.trim()) return null;
    try {
      const parsed: unknown = JSON.parse(value);
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error();
      const entries = Object.entries(parsed as Record<string, unknown>);
      if (entries.some(([subject, score]) => !subject.trim()
        || typeof score !== 'number'
        || !Number.isFinite(score)
        || score < 0
        || score > maxScore)) throw new Error();
      return Object.fromEntries(entries) as Record<string, number>;
    } catch {
      this.errorMessage.set(`Ders puanları geçerli JSON olmalı ve her değer 0-${maxScore} aralığında olmalıdır (örnek: {"Matematik": 85}).`);
      return undefined;
    }
  }

  private emptyExamForm(): ExamForm {
    return {
      title: '', type: 1, examDate: this.defaultDate(), maxScore: 100,
      subject: '', description: '', durationMinutes: undefined, targetGradeLevel: undefined
    };
  }

  private emptyResultForm(): ExamResultForm {
    return {
      studentId: '', score: 0, correctAnswers: 0, wrongAnswers: 0,
      emptyAnswers: 0, ranking: undefined, notes: '', subjectScoresText: ''
    };
  }

  private defaultDate(): string {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string): string {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private idempotencyKey(scope: string): string {
    return globalThis.crypto?.randomUUID?.() ?? `teacher-${scope}-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
