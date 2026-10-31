import { CoachingLabelPipe } from '../coaching-labels';
import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, input, OnInit, signal, ViewChild, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { validCoachingId, validWeeklyDate, WeeklyDestination } from '../../../../../shared/coaching-notification-target';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, ExamResult, Goal } from '../../../core/services/coaching-portal.service';
import { StudentStudyReportComponent } from './student-study-report.component';
import { PracticeProgressComponent } from './practice-progress.component';

@Component({
  selector: 'app-coaching-portal-progress',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, ReactiveFormsModule, StudentStudyReportComponent, PracticeProgressComponent],
  templateUrl: './coaching-portal-progress.component.html',
  styleUrl: './coaching-portal-progress.component.scss'
})
export class CoachingPortalProgressComponent implements OnInit {
  /** T6: under Haftam's summary the page drops its own heading. */
  readonly embedded = input(false);
  private readonly route = inject(ActivatedRoute, {optional:true});
  private readonly destroyRef = inject(DestroyRef);
  readonly weeklyTarget = signal<WeeklyDestination | null>(null);
  private weeklyNavigationVersion = 0;
  @ViewChild(PracticeProgressComponent) private practiceProgress?: PracticeProgressComponent;
  trackGoal(_index: number, goal: Goal) { return goal.id; }
  canUpdateGoals() {
    return !this.isSavingGoal() && !this.updatingGoalId() && (this.practiceProgress?.canRefresh() ?? true);
  }
  async canLeavePage() {
    return !this.isSavingGoal() && !this.updatingGoalId() && (await this.practiceProgress?.canLeavePage() ?? true);
  }
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly formBuilder = inject(FormBuilder);

  readonly goals = signal<Goal[]>([]);
  readonly habitGoals = computed(() => this.goals().filter(x => x.category !== 'ExamPreparation' && x.category !== '1'));
  readonly goalPageNumber = signal(1);
  readonly goalTotalPages = signal(1);
  readonly loadingMoreGoals = signal(false);
  readonly examResults = signal<ExamResult[]>([]);
  readonly examPageNumber = signal(1);
  readonly examTotalPages = signal(1);
  readonly loadingMoreExams = signal(false);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly showGoalForm = signal(false);
  readonly isSavingGoal = signal(false);
  readonly updatingGoalId = signal<string | null>(null);
  readonly goalFormError = signal<string | null>(null);
  readonly goalCategories = [
    { value: 2, apiName: 'SubjectMastery', label: 'Ders hakimiyeti' },
    { value: 3, apiName: 'GradeImprovement', label: 'Not yükseltme' },
    { value: 4, apiName: 'StudyHabits', label: 'Çalışma alışkanlığı' },
    { value: 5, apiName: 'TimeManagement', label: 'Zaman yönetimi' },
    { value: 99, apiName: 'Other', label: 'Diğer' }
  ];
  // K4 (31.10.2026): "tomorrow" is counted in Türkiye time, not from the UTC date.
  readonly minTargetDate = tomorrowInTurkey(new Date());
  readonly goalForm = this.formBuilder.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    category: [4, [Validators.required]],
    description: ['', [Validators.maxLength(2_000)]],
    targetDate: [''],
    targetScore: [null as number | null, [Validators.min(0), Validators.max(999.99)]]
  });

  ngOnInit() {
    this.route?.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(async params => {
      const navigationVersion = ++this.weeklyNavigationVersion;
      const week = params.get('week'); const decision = params.get('decision');
      if (!week) return;
      if (!validWeeklyDate(week) || decision !== null && !validCoachingId(decision)) { this.errorMessage.set('Haftalık kayıt bağlantısı geçersiz.'); return; }
      if (this.weeklyTarget()?.weekStart === week && this.weeklyTarget()?.decisionId === decision) return;
      if (this.practiceProgress && !await this.canLeavePage()) { this.errorMessage.set('Kaydedilmemiş girdilerin korunuyor. Haftalık kaydı açmadan önce kaydet veya düzenlemeyi kapat.'); return; }
      if (navigationVersion !== this.weeklyNavigationVersion) return;
      this.weeklyTarget.set({studentId:this.authService.userProfile()?.id ?? '',weekStart:week,decisionId:decision});
    });
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğrenci profili bulunamadı.');
      return;
    }

    forkJoin({
      goals: this.coachingService.getStudentGoals(studentId, 1, 100),
      exams: this.coachingService.getStudentExamResults(studentId, 1, 25)
    }).subscribe({
      next: result => {
        this.goals.set(result.goals.items);
        this.goalPageNumber.set(result.goals.pageNumber);
        this.goalTotalPages.set(result.goals.totalPages ?? Math.max(1, Math.ceil(result.goals.totalCount / result.goals.pageSize)));
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

  async createGoal() {
    if (!await this.canLeavePage()) return;
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
        this.goalFormError.set('Hedef oluşturulamadı. Bilgileri kontrol edip tekrar dene.');
        this.isSavingGoal.set(false);
      },
      complete: () => this.isSavingGoal.set(false)
    });
  }

  async updateProgress(goal: Goal, event: Event) {
    const input = event.target as HTMLInputElement;
    if (!await this.canLeavePage()) { input.value = String(goal.progress); return; }
    const progress = Math.min(100, Math.max(0, Number(input.value)));
    if (!Number.isFinite(progress) || progress === goal.progress) return;

    this.updatingGoalId.set(goal.id);
    this.coachingService.updateGoalProgress(goal.id, progress).pipe(
      finalize(() => this.updatingGoalId.set(null))
    ).subscribe({
      next: () => {
        this.goals.update(items => items.map(item => item.id === goal.id
          ? { ...item, progress, isCompleted: progress === 100 }
          : item));
      },
      error: () => {
        this.goalFormError.set('Hedef ilerlemesi güncellenemedi.');
        input.value = String(goal.progress);
      }
    });
  }

  categoryLabel(category: string) {
    return this.goalCategories.find(item => item.value === Number(category) || item.apiName === category)?.label ?? (category === 'ExamPreparation' || category === '1' ? 'Sınav hazırlığı' : category);
  }

  private loadGoals(studentId: string) {
    this.coachingService.getStudentGoals(studentId, 1, 100).subscribe({
      next: page => {
        this.goals.set(page.items);
        this.goalPageNumber.set(page.pageNumber);
        this.goalTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.goalFormError.set('Hedef listesi yenilenemedi.')
    });
  }

  private newIdempotencyKey() {
    return globalThis.crypto.randomUUID();
  }



  loadMoreGoals() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId || this.goalPageNumber() >= this.goalTotalPages() || this.loadingMoreGoals()) return;
    this.loadingMoreGoals.set(true);
    this.coachingService.getStudentGoals(studentId, this.goalPageNumber() + 1, 100).pipe(
      finalize(() => this.loadingMoreGoals.set(false))
    ).subscribe({
      next: page => {
        this.goals.update(items => [...items, ...page.items]);
        this.goalPageNumber.set(page.pageNumber);
      },
      error: () => this.goalFormError.set('Hedeflerin devamı yüklenemedi. Yeniden deneyebilirsin.')
    });
  }



  scorePercentage(result: ExamResult) {
    return result.maxScore > 0 ? Math.round(result.score / result.maxScore * 100) : null;
  }

  subjectScoreLabel(scores?: Record<string, number>) {
    return scores ? Object.entries(scores).map(([subject, score]) => `${subject}: ${score}`).join(' · ') : '—';
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
  private readonly feedback = coachingFeedback(undefined, this.errorMessage);
}

/** The day after today in Europe/Istanbul as yyyy-MM-dd. */
export function tomorrowInTurkey(now: Date): string {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
  const [y, m, d] = today.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + 1)).toISOString().slice(0, 10);
}
