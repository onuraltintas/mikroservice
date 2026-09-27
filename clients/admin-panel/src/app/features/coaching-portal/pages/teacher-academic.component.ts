import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, map } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  CoachingPortalService,
  TeacherExam,
  TeacherExamDetail,
  TeacherExamResult,
  TeacherGoal,
  TeacherStudent
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-academic',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './teacher-academic.component.html'
})
export class TeacherAcademicComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly exams = signal<TeacherExam[]>([]);
  readonly goals = signal<TeacherGoal[]>([]);
  readonly students = signal<TeacherStudent[]>([]);
  studentSearchTerm = '';
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly isLoadingStudents = signal(false);
  readonly examPageNumber = signal(1);
  readonly examTotalPages = signal(1);
  readonly goalPageNumber = signal(1);
  readonly goalTotalPages = signal(1);
  readonly isLoading = signal(true);
  readonly isSavingExam = signal(false);
  readonly isLoadingExamResults = signal(false);
  readonly isSavingExamResult = signal(false);
  readonly examResultPageNumber = signal(1);
  readonly examResultTotalPages = signal(1);
  readonly isSavingGoal = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingExamId = signal<string | null>(null);
  readonly editingGoalId = signal<string | null>(null);
  readonly selectedExam = signal<TeacherExam | null>(null);
  readonly examDetail = signal<TeacherExamDetail | null>(null);
  readonly editingExamResultId = signal<string | null>(null);
  private studentSearchRequestId = 0;

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
  readonly goalCategories = [
    { value: 1, label: 'Sınav hazırlığı' },
    { value: 2, label: 'Ders hakimiyeti' },
    { value: 3, label: 'Not yükseltme' },
    { value: 4, label: 'Çalışma alışkanlıkları' },
    { value: 5, label: 'Zaman yönetimi' },
    { value: 99, label: 'Diğer' }
  ];

  examForm = this.emptyExamForm();
  goalForm = this.emptyGoalForm();
  resultForm = this.emptyResultForm();

  ngOnInit() {
    if (!this.authService.userProfile()?.id) {
      this.errorMessage.set('Öğretmen profili bulunamadı.');
      this.isLoading.set(false);
      return;
    }
    this.loadExams();
    this.loadGoals();
    this.loadStudents();
  }

  loadExams(append = false) {
    const teacherId = this.authService.userProfile()?.id;
    if (!teacherId) {
      this.isLoading.set(false);
      return;
    }
    const pageNumber = append ? this.examPageNumber() + 1 : 1;
    this.isLoading.set(true);
    this.coachingService.getTeacherExams(teacherId, pageNumber, 25).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: page => {
        this.exams.update(items => append ? [...items, ...page.items] : page.items);
        this.examPageNumber.set(page.pageNumber);
        this.examTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Sınavlar yüklenemedi.')
    });
  }

  searchStudents(searchTerm = this.studentSearchTerm) {
    this.studentSearchTerm = searchTerm.trim();
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  loadMoreStudents() {
    if (this.isLoadingStudents() || this.studentPageNumber() >= this.studentTotalPages()) return;
    this.loadStudents(true);
  }

  private loadStudents(append = false) {
    const requestId = ++this.studentSearchRequestId;
    const pageNumber = append ? this.studentPageNumber() + 1 : 1;
    this.isLoadingStudents.set(true);
    const studentsRequest = this.studentSearchTerm
      ? this.coachingService.getTeacherStudents(pageNumber, 100, this.studentSearchTerm)
      : this.coachingService.getTeacherStudents(pageNumber, 100);
    studentsRequest.pipe(
      finalize(() => {
        if (requestId === this.studentSearchRequestId) this.isLoadingStudents.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestId !== this.studentSearchRequestId) return;
        const selectedIds = new Set(this.selectedStudentIds());
        const retainedStudents = append
          ? this.students()
          : this.students().filter(student => selectedIds.has(student.userId));
        const studentsById = new Map<string, TeacherStudent>();
        for (const student of [...retainedStudents, ...page.items]) studentsById.set(student.userId, student);
        this.students.set([...studentsById.values()]);
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        if (requestId === this.studentSearchRequestId) this.errorMessage.set('Öğrenci listesi yüklenemedi.');
      }
    });
  }

  private selectedStudentIds() {
    return [...new Set([this.goalForm.studentId, this.resultForm.studentId].filter(Boolean))];
  }

  private ensureStudentOption(studentId: string) {
    if (!studentId || this.students().some(student => student.userId === studentId)) return;
    this.coachingService.getTeacherStudents(1, 1, undefined, [studentId]).subscribe({
      next: page => {
        const student = page.items.find(item => item.userId === studentId);
        if (student) {
          this.students.update(items => items.some(item => item.userId === studentId) ? items : [...items, student]);
        } else {
          this.errorMessage.set('Bu öğrenci artık aktif öğrenci listenizde bulunmuyor.');
        }
      },
      error: () => this.errorMessage.set('Öğrenci bilgisi yüklenemedi.')
    });
  }

  loadGoals(append = false) {
    const teacherId = this.authService.userProfile()?.id;
    if (!teacherId) return;
    const pageNumber = append ? this.goalPageNumber() + 1 : 1;
    this.coachingService.getTeacherGoals(teacherId, pageNumber, 25).subscribe({
      next: page => {
        this.goals.update(items => append ? [...items, ...page.items] : page.items);
        this.goalPageNumber.set(page.pageNumber);
        this.goalTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Hedefler yüklenemedi.')
    });
  }

  saveExam() {
    const teacherId = this.authService.userProfile()?.id;
    const title = this.examForm.title.trim();
    const examDate = new Date(this.examForm.examDate);
    if (!teacherId || !title) {
      this.errorMessage.set('Sınav başlığı zorunludur.');
      return;
    }
    if (Number.isNaN(examDate.getTime()) || examDate <= new Date()) {
      this.errorMessage.set('Sınav tarihi gelecekte olmalıdır.');
      return;
    }
    if (!Number.isFinite(this.examForm.maxScore) || this.examForm.maxScore <= 0) {
      this.errorMessage.set('Maksimum puan sıfırdan büyük olmalıdır.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingExam.set(true);
    const examId = this.editingExamId();
    const request = {
      title,
      type: this.examForm.type,
      examDate: examDate.toISOString(),
      maxScore: this.examForm.maxScore,
      description: this.examForm.description.trim() || null
    };
    const operation = examId
      ? this.coachingService.updateTeacherExam(examId, {
        examId,
        ...request,
        subject: this.examForm.subject.trim() || null,
        durationMinutes: this.examForm.durationMinutes ?? null,
        targetGradeLevel: this.examForm.targetGradeLevel ?? null
      })
      : this.coachingService.createTeacherExam({ teacherId, ...request }, this.idempotencyKey('exam'));
    operation.pipe(finalize(() => this.isSavingExam.set(false))).subscribe({
      next: () => {
        this.successMessage.set(examId ? 'Sınav güncellendi.' : 'Sınav oluşturuldu.');
        this.cancelExamEdit();
        this.loadExams();
      },
      error: () => this.errorMessage.set('Sınav kaydedilemedi; alanları ve yetkinizi kontrol edin.')
    });
  }

  editExam(exam: TeacherExam) {
    this.editingExamId.set(exam.id);
    this.examForm = {
      title: exam.title,
      type: this.examTypes.find(item => item.key === exam.examType)?.value ?? 1,
      examDate: this.toLocalDateTime(exam.examDate),
      maxScore: exam.maxScore,
      description: exam.description ?? '',
      subject: exam.subject ?? '',
      durationMinutes: exam.durationMinutes,
      targetGradeLevel: exam.targetGradeLevel
    };
  }

  cancelExamEdit() {
    this.editingExamId.set(null);
    this.examForm = this.emptyExamForm();
  }

  selectExamResults(exam: TeacherExam) {
    this.selectedExam.set(exam);
    this.examDetail.set(null);
    this.examResultPageNumber.set(1);
    this.examResultTotalPages.set(1);
    this.editingExamResultId.set(null);
    this.resultForm = this.emptyResultForm();
    this.errorMessage.set(null);
    this.isLoadingExamResults.set(true);
    this.loadExamResults(exam, 1, false);
  }

  loadMoreExamResults() {
    const exam = this.selectedExam();
    if (!exam || this.examResultPageNumber() >= this.examResultTotalPages() || this.isLoadingExamResults()) return;
    this.loadExamResults(exam, this.examResultPageNumber() + 1, true);
  }

  editExamResult(result: TeacherExamResult) {
    this.editingExamResultId.set(result.id);
    this.ensureStudentOption(result.studentId);
    this.resultForm = {
      studentId: result.studentId,
      score: result.score,
      correctAnswers: result.correctAnswers ?? 0,
      wrongAnswers: result.wrongAnswers ?? 0,
      emptyAnswers: result.emptyAnswers ?? 0,
      ranking: result.ranking,
      notes: result.teacherNotes ?? '',
      subjectScoresText: result.subjectScores ? JSON.stringify(result.subjectScores, null, 2) : ''
    };
  }

  cancelExamResultEdit() {
    this.editingExamResultId.set(null);
    this.resultForm = this.emptyResultForm();
  }

  saveExamResult() {
    const exam = this.selectedExam();
    const resultId = this.editingExamResultId();
    const studentId = this.resultForm.studentId;
    if (!exam || (!resultId && !studentId)) {
      this.errorMessage.set('Yeni sonuç için öğrenci seçilmelidir.');
      return;
    }
    if (!Number.isFinite(this.resultForm.score) || this.resultForm.score < 0 || this.resultForm.score > exam.maxScore) {
      this.errorMessage.set(`Puan 0 ile ${exam.maxScore} arasında olmalıdır.`);
      return;
    }
    if ([this.resultForm.correctAnswers, this.resultForm.wrongAnswers, this.resultForm.emptyAnswers].some(value => !Number.isInteger(value) || value < 0)) {
      this.errorMessage.set('Doğru, yanlış ve boş cevap sayıları negatif olmayan tam sayı olmalıdır.');
      return;
    }
    if (this.resultForm.ranking !== undefined && (!Number.isInteger(this.resultForm.ranking) || this.resultForm.ranking < 1)) {
      this.errorMessage.set('Sıralama 1 veya daha büyük bir tam sayı olmalıdır.');
      return;
    }

    const subjectScores = this.parseSubjectScores(this.resultForm.subjectScoresText, exam.maxScore);
    if (subjectScores === undefined) return;

    const request = {
      examId: exam.id,
      ...(resultId ? { resultId } : { studentId }),
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
    this.isSavingExamResult.set(true);
    const operation = resultId
      ? this.coachingService.updateTeacherExamResult(exam.id, resultId, request).pipe(map(response => response as unknown))
      : this.coachingService.addTeacherExamResult(exam.id, request, this.idempotencyKey('exam-result')).pipe(map(response => response as unknown));
    operation.pipe(finalize(() => this.isSavingExamResult.set(false))).subscribe({
      next: () => {
        this.successMessage.set(resultId ? 'Sınav sonucu güncellendi.' : 'Sınav sonucu eklendi.');
        this.cancelExamResultEdit();
        this.selectExamResults(exam);
        this.loadExams();
      },
      error: () => this.errorMessage.set('Sınav sonucu kaydedilemedi; puan ve yetki bilgilerini kontrol edin.')
    });
  }

  saveGoal() {
    const teacherId = this.authService.userProfile()?.id;
    const title = this.goalForm.title.trim();
    if (!teacherId || !title || !this.goalForm.studentId) {
      this.errorMessage.set('Hedef başlığı ve öğrenci zorunludur.');
      return;
    }
    const targetDate = this.goalForm.targetDate ? new Date(this.goalForm.targetDate) : null;
    if (targetDate && Number.isNaN(targetDate.getTime())) {
      this.errorMessage.set('Hedef tarihi geçerli olmalıdır.');
      return;
    }
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingGoal.set(true);
    const goalId = this.editingGoalId();
    const request = {
      title,
      category: this.goalForm.category,
      description: this.goalForm.description.trim() || null,
      targetDate: targetDate?.toISOString() ?? null,
      targetScore: this.goalForm.targetScore ?? null
    };
    const operation = goalId
      ? this.coachingService.updateTeacherGoal(goalId, {
        goalId,
        ...request,
        targetExamType: this.goalForm.targetExamType ?? null,
        targetSubject: this.goalForm.targetSubject.trim() || null
      })
      : this.coachingService.createTeacherGoal({ teacherId, studentId: this.goalForm.studentId, ...request }, this.idempotencyKey('goal'));
    operation.pipe(finalize(() => this.isSavingGoal.set(false))).subscribe({
      next: () => {
        this.successMessage.set(goalId ? 'Hedef güncellendi.' : 'Hedef oluşturuldu.');
        this.cancelGoalEdit();
        this.loadGoals();
      },
      error: () => this.errorMessage.set('Hedef kaydedilemedi; alanları ve yetkinizi kontrol edin.')
    });
  }

  editGoal(goal: TeacherGoal) {
    this.editingGoalId.set(goal.id);
    this.ensureStudentOption(goal.studentId);
    this.goalForm = {
      studentId: goal.studentId,
      title: goal.title,
      category: this.goalCategories.find(item => item.label === goal.category)?.value ?? 99,
      description: goal.description ?? '',
      targetDate: goal.targetDate ? this.toLocalDateTime(goal.targetDate) : '',
      targetScore: goal.targetScore,
      targetExamType: this.examTypes.find(item => item.key === goal.targetExamType)?.value,
      targetSubject: goal.targetSubject ?? ''
    };
  }

  cancelGoalEdit() {
    this.editingGoalId.set(null);
    this.goalForm = this.emptyGoalForm();
  }

  studentLabel(studentId: string) {
    const student = this.students().find(item => item.userId === studentId);
    return student?.fullName ?? `Öğrenci ${studentId.slice(-8)}`;
  }

  trackById(_: number, item: TeacherExam | TeacherGoal) {
    return item.id;
  }

  private emptyExamForm() {
    return {
      title: '',
      type: 1,
      examDate: this.defaultDate(),
      maxScore: 100,
      description: '',
      subject: '',
      durationMinutes: undefined as number | undefined,
      targetGradeLevel: undefined as number | undefined
    };
  }

  private emptyGoalForm() {
    return {
      studentId: '',
      title: '',
      category: 1,
      description: '',
      targetDate: '',
      targetScore: undefined as number | undefined,
      targetExamType: undefined as number | undefined,
      targetSubject: ''
    };
  }

  private emptyResultForm() {
    return {
      studentId: '',
      score: 0,
      correctAnswers: 0,
      wrongAnswers: 0,
      emptyAnswers: 0,
      ranking: undefined as number | undefined,
      notes: '',
      subjectScoresText: ''
    };
  }

  private parseSubjectScores(value: string, maxScore: number) {
    if (!value.trim()) return null;
    try {
      const parsed = JSON.parse(value) as unknown;
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error();
      const scores = Object.entries(parsed as Record<string, unknown>);
      if (scores.some(([subject, score]) => !subject.trim() || typeof score !== 'number' || !Number.isFinite(score) || score < 0 || score > maxScore)) throw new Error();
      return Object.fromEntries(scores) as Record<string, number>;
    } catch {
      this.errorMessage.set(`Ders puanları geçerli JSON olmalıdır (örnek: {"Matematik": 85}) ve 0-${maxScore} aralığında olmalıdır.`);
      return undefined;
    }
  }

  private loadExamResults(exam: TeacherExam, pageNumber: number, append: boolean) {
    this.isLoadingExamResults.set(true);
    this.coachingService.getTeacherExamDetail(exam.id, pageNumber, 25).pipe(
      finalize(() => this.isLoadingExamResults.set(false))
    ).subscribe({
      next: detail => {
        const previous = this.examDetail();
        this.examDetail.set(append && previous
          ? { ...detail, results: [...previous.results, ...detail.results] }
          : detail);
        this.examResultPageNumber.set(detail.resultPageNumber);
        this.examResultTotalPages.set(detail.resultTotalPages);
      },
      error: () => this.errorMessage.set('Sınav sonuçları yüklenemedi.')
    });
  }

  private defaultDate() {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string) {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private idempotencyKey(scope: string) {
    return globalThis.crypto?.randomUUID?.() ?? `teacher-${scope}-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
