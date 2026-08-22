import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, ExamResult, Goal, StudentProgressSummary } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-coaching-portal-progress',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './coaching-portal-progress.component.html',
  styleUrl: './coaching-portal-progress.component.scss'
})
export class CoachingPortalProgressComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly formBuilder = inject(FormBuilder);

  readonly goals = signal<Goal[]>([]);
  readonly examResults = signal<ExamResult[]>([]);
  readonly examPageNumber = signal(1);
  readonly examTotalPages = signal(1);
  readonly loadingMoreExams = signal(false);
  readonly summary = signal<StudentProgressSummary | null>(null);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly showGoalForm = signal(false);
  readonly isSavingGoal = signal(false);
  readonly updatingGoalId = signal<string | null>(null);
  readonly goalFormError = signal<string | null>(null);
  readonly goalCategories = [
    { value: 1, label: 'Sınav hazırlığı' },
    { value: 2, label: 'Ders hakimiyeti' },
    { value: 3, label: 'Not yükseltme' },
    { value: 4, label: 'Çalışma alışkanlığı' },
    { value: 5, label: 'Zaman yönetimi' },
    { value: 99, label: 'Diğer' }
  ];
  readonly minTargetDate = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);
  readonly goalForm = this.formBuilder.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    category: [1, [Validators.required]],
    description: ['', [Validators.maxLength(2_000)]],
    targetDate: [''],
    targetScore: [null as number | null, [Validators.min(0), Validators.max(999.99)]]
  });

  ngOnInit() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğrenci profili bulunamadı.');
      return;
    }

    forkJoin({
      summary: this.coachingService.getStudentProgress(studentId).pipe(catchError(() => of(null))),
      goals: this.coachingService.getStudentGoals(studentId, 1, 100),
      exams: this.coachingService.getStudentExamResults(studentId, 1, 25)
    }).subscribe({
      next: result => {
        this.summary.set(result.summary);
        this.goals.set(result.goals.items);
        this.examResults.set(result.exams.items);
        this.examPageNumber.set(result.exams.pageNumber);
        this.examTotalPages.set(result.exams.totalPages ?? Math.max(1, Math.ceil(result.exams.totalCount / result.exams.pageSize)));
      },
      error: () => {
        this.errorMessage.set('İlerleme verileri yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  toggleGoalForm() {
    this.showGoalForm.update(visible => !visible);
    this.goalFormError.set(null);
  }

  createGoal() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.goalFormError.set('Öğrenci profili bulunamadı.');
      return;
    }

    if (this.goalForm.invalid) {
      this.goalForm.markAllAsTouched();
      return;
    }

    const value = this.goalForm.getRawValue();
    const targetDate = value.targetDate ? `${value.targetDate}T23:59:59.000Z` : null;
    this.isSavingGoal.set(true);
    this.goalFormError.set(null);

    this.coachingService.createStudentGoal(
      studentId,
      {
        title: value.title ?? '',
        category: Number(value.category),
        description: value.description,
        targetDate,
        targetScore: value.targetScore
      },
      this.newIdempotencyKey()
    ).subscribe({
      next: () => {
        this.showGoalForm.set(false);
        this.goalForm.reset({ title: '', category: 1, description: '', targetDate: '', targetScore: null });
        this.loadGoals(studentId);
      },
      error: () => {
        this.goalFormError.set('Hedef oluşturulamadı. Bilgileri kontrol edip tekrar deneyin.');
        this.isSavingGoal.set(false);
      },
      complete: () => this.isSavingGoal.set(false)
    });
  }

  updateProgress(goal: Goal, event: Event) {
    const input = event.target as HTMLInputElement;
    const progress = Math.min(100, Math.max(0, Number(input.value)));
    if (!Number.isFinite(progress) || progress === goal.progress) return;

    this.updatingGoalId.set(goal.id);
    this.coachingService.updateGoalProgress(goal.id, progress).subscribe({
      next: () => {
        this.goals.update(items => items.map(item => item.id === goal.id
          ? { ...item, progress, isCompleted: progress === 100 }
          : item));
      },
      error: () => {
        this.goalFormError.set('Hedef ilerlemesi güncellenemedi.');
        input.value = String(goal.progress);
      },
      complete: () => this.updatingGoalId.set(null)
    });
  }

  categoryLabel(category: string) {
    return this.goalCategories.find(item => item.value === Number(category))?.label ?? category;
  }

  private loadGoals(studentId: string) {
    this.coachingService.getStudentGoals(studentId, 1, 100).subscribe({
      next: page => this.goals.set(page.items),
      error: () => this.goalFormError.set('Hedef listesi yenilenemedi.')
    });
  }

  private newIdempotencyKey() {
    return globalThis.crypto.randomUUID();
  }

  averageScore() {
    const summary = this.summary();
    if (summary?.averageExamPercentage !== undefined) return Math.round(summary.averageExamPercentage);
    const results = this.examResults();
    if (results.length === 0) return null;
    const percentage = results.reduce((total, result) => total + (result.maxScore > 0 ? result.score / result.maxScore : 0), 0) / results.length;
    return Math.round(percentage * 100);
  }

  assignmentCompletion() {
    const summary = this.summary();
    if (!summary || summary.totalAssignments === 0) return null;
    return Math.round(summary.submittedAssignments / summary.totalAssignments * 100);
  }

  attendancePercentage() {
    return this.summary()?.attendancePercentage ?? null;
  }

  scorePercentage(result: ExamResult) {
    return result.maxScore > 0 ? Math.round(result.score / result.maxScore * 100) : null;
  }

  subjectScoreLabel(scores?: Record<string, number>) {
    return scores ? Object.entries(scores).map(([subject, score]) => `${subject}: ${score}`).join(' · ') : '—';
  }

  examTrend() {
    return [...this.examResults()]
      .sort((left, right) => new Date(left.examDate).getTime() - new Date(right.examDate).getTime())
      .slice(-6);
  }

  loadMoreExams() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId || this.examPageNumber() >= this.examTotalPages() || this.loadingMoreExams()) return;
    const nextPage = this.examPageNumber() + 1;
    this.loadingMoreExams.set(true);
    this.coachingService.getStudentExamResults(studentId, nextPage, 25).pipe(
      finalize(() => this.loadingMoreExams.set(false))
    ).subscribe({
      next: page => {
        this.examResults.update(items => [...items, ...page.items]);
        this.examPageNumber.set(page.pageNumber);
      },
      error: () => this.errorMessage.set('Sınav sonuçlarının devamı yüklenemedi.')
    });
  }
}
