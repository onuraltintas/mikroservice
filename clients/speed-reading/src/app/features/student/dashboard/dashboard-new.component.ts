import { Component, OnInit, signal, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntil, finalize } from 'rxjs/operators';
import { forkJoin } from 'rxjs';
import { BaseComponent } from '../../../core/components/base.component';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { AssessmentService } from '../../../services/assessment.service';
import { GamificationService } from '../../../core/services/gamification.service';
import { CountUpDirective } from '../../../shared/directives/count-up.directive';
import { DailyPlanWidgetComponent } from './widgets/daily-plan-widget.component';
import { PerformanceAnalysisWidgetComponent } from './widgets/performance-analysis-widget.component';
import { StreakWidgetComponent } from './widgets/streak-widget.component';
import { LevelBadgesWidgetComponent } from './widgets/level-badges-widget.component';
import { QuickAccessWidgetComponent } from './widgets/quick-access-widget.component';
import { RecentAchievementsWidgetComponent } from './widgets/recent-achievements-widget.component';
import { ExerciseProgramService } from '../../../core/services/exercise-program.service';
import { environment } from '../../../../environments/environment';
import { ProgramCompletionModalComponent } from '../program-completion-modal/program-completion-modal';

import { QuoteWidgetComponent } from './widgets/quote-widget.component';
import { AssignmentsWidgetComponent } from './widgets/assignments-widget.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    CountUpDirective,
    DailyPlanWidgetComponent,
    StreakWidgetComponent,
    LevelBadgesWidgetComponent,
    PerformanceAnalysisWidgetComponent,
    QuickAccessWidgetComponent,
    RecentAchievementsWidgetComponent,
    QuoteWidgetComponent,
    AssignmentsWidgetComponent
  ],
  templateUrl: './dashboard-new.component.html',
  styleUrl: './dashboard-new.component.scss'
})
export class DashboardNewComponent extends BaseComponent implements OnInit {
  private exerciseService = inject(ExerciseProgramService);
  private http = inject(HttpClient);
  private gamificationService = inject(GamificationService);
  // snackBar inherited from BaseComponent via toaster

  currentUser: any;
  // loading inherited from BaseComponent
  showAssessmentBanner = signal(false);

  // Dashboard stats
  todayProgress = signal(0);
  currentStreak = signal(0);
  successRate = signal(0);
  currentLevel = signal(0);
  currentXP = signal(0);

  // Program info
  programName = signal('Program');
  currentWeek = signal(1);
  currentDay = signal(1);
  totalWeeks = signal(0);
  totalDays = signal(0);
  completedExercises = signal(0);
  hasProgram = signal<boolean | null>(null);

  constructor(
    private authService: AuthService,
    private router: Router,
    private assessmentService: AssessmentService
  ) {
    super();
    this.currentUser = this.authService.currentUserValue;

    // Check assessment completion status
    effect(() => {
      // Admins don't need to complete assessment (they use Test Mode)
      if (this.isAdmin) {
        this.showAssessmentBanner.set(false);
        return;
      }

      const hasCompleted = this.assessmentService.hasCompleted();
      // If assessment is NOT completed, show banner.
      // But if user has NO program (and assessment is done maybe?), show empty state instead of banner?
      // For now, let's keep banner logic as is.
      this.showAssessmentBanner.set(!hasCompleted);
    });
  }

  ngOnInit(): void {
    // Only load data if user is authenticated
    if (!this.currentUser) {
      this.router.navigate(['/login']);
      return;
    }

    // Check assessment status
    this.checkAssessmentStatus();

    // Load dashboard stats
    this.loadDashboardStats();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  checkAssessmentStatus(): void {
    if (!this.currentUser) return;

    this.assessmentService.getAssessmentStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        error: (error) => {
          console.error('Error checking assessment status:', error);
          // Don't show error toast for status check, just log it.
          // this.handleError(error, 'Değerlendirme durumu kontrol edilirken hata oluştu');
        }
      });
  }

  loadDashboardStats(): void {
    this.loading.set(true);

    // Load today's progress
    this.exerciseService.getTodayExercises({ headers: { 'X-Skip-Error-Toast': 'true' } })
      .pipe(
        takeUntil(this.destroy$),
        // finalize handled in forkJoin or manually
      )
      .subscribe({
        next: (exercises: any[]) => {
          const completed = exercises.filter((e: any) => e.isCompleted).length;
          const total = exercises.length;
          this.todayProgress.set(total > 0 ? Math.round((completed / total) * 100) : 0);
        },
        error: (error) => {
          // Silent fail for today exercises if main program fails
          console.warn('Error loading today exercises (might be due to no program):', error);
        }
      });

    // Load overall progress and achievements
    forkJoin({
      progress: this.exerciseService.getMyProgress({ headers: { 'X-Skip-Error-Toast': 'true' } }),
      allAchievements: this.gamificationService.getAllAchievements(),
      userAchievements: this.gamificationService.getUserAchievements()
    })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: ({ progress, allAchievements, userAchievements }) => {
          this.hasProgram.set(true); // Program loaded successfully

          // Program info
          this.programName.set(progress.templateName || 'Hızlı Okuma Programı');
          this.currentWeek.set(progress.currentWeek || 1);
          this.currentDay.set(progress.currentDay || 1);
          this.totalWeeks.set(progress.totalWeeks || 0);
          this.totalDays.set(progress.totalDays || 0);
          this.completedExercises.set(progress.totalExercisesCompleted || 0);
          this.successRate.set(Math.round(progress.averageSuccessRate || 0));

          // Gamification Stats (from GamificationService)
          this.gamificationService.getUserGameification().subscribe(ug => {
            if (ug) {
              this.currentStreak.set(ug.currentStreak);
              this.currentLevel.set(ug.currentLevel);
              this.currentXP.set(ug.totalXP);
            } else {
              // Fallback if no gamification record yet
              this.currentStreak.set(progress.currentStreak || 0);
              this.currentLevel.set(progress.currentDifficultyLevel || 1);

              // Calculate Badge XP fallback
              const badgeXP = userAchievements.reduce((total, ua) => {
                const achievement = allAchievements.find(a => a.id === ua.achievementId);
                return total + (achievement?.xpReward || 0);
              }, 0);
              const level = progress.currentDifficultyLevel || 1;
              const streak = progress.currentStreak || 0;
              this.currentXP.set(((level - 1) * 200) + (streak * 10) + badgeXP);
            }
          });
        },
        error: (error) => {
          // If 404, it means user has no program yet (new user)
          if (error.status === 404) {
            this.hasProgram.set(false); // No program
            this.currentStreak.set(0);
            this.successRate.set(0);
            this.currentLevel.set(1);
            this.currentXP.set(0);
            this.programName.set('Program Bekleniyor');
            // Silent return, no console error
          } else {
            console.error('Error loading progress:', error);
            this.handleError(error, 'İlerleme verileri yüklenirken hata oluştu');
          }
        }
      });
  }



  goToDailyPlan(): void {
    this.router.navigate(['/student/daily-exercises']);
  }

  goToExercises(): void {
    this.router.navigate(['/student/daily-exercises']);
  }

  goToTodayExercises(): void {
    this.router.navigate(['/student/daily-exercises']);
  }

  goToHistoricalExercises(): void {
    this.router.navigate(['/student/daily-exercises'], {
      queryParams: { tab: 'history' }
    });
  }

  createPlan(): void {
    this.router.navigate(['/student/daily-exercises']);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  startAssessment(): void {
    this.router.navigate(['/student/assessment']);
  }

  getInitials(firstName?: string, lastName?: string): string {
    if (!firstName && !lastName) return '';
    return `${(firstName?.charAt(0) || '').toUpperCase()}${(lastName?.charAt(0) || '').toUpperCase()}`;
  }

  // ===== ADMIN TEST METHODS =====

  get isAdmin(): boolean {
    return this.authService.hasAdminAccess();
  }

  async completeDayForTest() {
    const confirmed = await this.confirm('Bugünün tüm egzersizlerini otomatik tamamlamak istiyor musunuz?');
    if (!confirmed) return;

    this.loading.set(true);
    this.http.post(`${environment.apiUrl}/admin/test-mode/complete-day`, {
      successRate: 85
    })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (response: any) => {
          this.handleSuccess('Gün başarıyla tamamlandı!');

          if (response.programCompleted) {
            // Program bitti, modal aç
            // Not: Gerçek senaryoda backend'den stats ve recommendation gelmeli
            // Test modunda bunları simüle edebiliriz veya backend'i güncelleyebiliriz
            // Şimdilik basit bir mesaj gösterelim, çünkü modal için detaylı veri lazım
            // alert('🎉 PROGRAM TAMAMLANDI! (Test Modu)');
            this.toaster.success('Program tamamlandı! (Test Modu)', 5000);
            // İsterseniz burada modal'ı dummy data ile açabiliriz
          }

          this.loadDashboardStats();
        },
        error: (error) => {
          console.error('Error completing day:', error);
          this.handleError(error, 'Gün tamamlanırken hata oluştu');
        }
      });
  }

  async skipDayForTest() {
    const confirmed = await this.confirm('Bir sonraki güne atlamak istiyor musunuz?');
    if (!confirmed) return;

    this.loading.set(true);
    this.http.post(`${environment.apiUrl}/admin/test-mode/skip-day`, {})
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: () => {
          this.handleSuccess('Sonraki güne geçildi!');
          this.loadDashboardStats();
        },
        error: (error) => {
          console.error('Error skipping day:', error);
          this.handleError(error, 'Gün atlanırken hata oluştu');
        }
      });
  }
}
