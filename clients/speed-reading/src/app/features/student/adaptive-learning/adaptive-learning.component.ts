import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { finalize, forkJoin, takeUntil } from 'rxjs';
import { BaseComponent } from '../../../core/components/base.component';
import { AdaptiveLearningService, AdaptiveDashboard, WeakArea, ContentRecommendation } from '../../../core/services/adaptive-learning.service';
import { AdaptiveTextService } from '../../../core/services/adaptive-text.service';
import { StudentReadingProfile } from '../../../core/models/adaptive-text.model';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-adaptive-learning',
  standalone: true,
  imports: [
    CommonModule, RouterModule
  ],
  templateUrl: './adaptive-learning.component.html'
})
export class AdaptiveLearningComponent extends BaseComponent implements OnInit {
  private adaptiveService = inject(AdaptiveLearningService);
  private adaptiveTextService = inject(AdaptiveTextService);
  private authService = inject(AuthService);

  dashboard: AdaptiveDashboard | null = null;
  weakAreas: WeakArea[] = [];
  recommendations: ContentRecommendation[] = [];
  readingProfile: StudentReadingProfile | null = null;
  hasData = false;

  activeTab = 0;

  readonly bloomDescriptions: Record<number, string> = {
    1: 'Bilgileri hatırlama ve tanıma',
    2: 'Bilgiyi yorumlama ve açıklama',
    3: 'Bilgiyi yeni duruma uygulama',
    4: 'Parçalara ayırma ve ilişki kurma',
    5: 'Yargılama ve eleştirme',
    6: 'Yeni yapılar oluşturma'
  };

  readonly bloomNames: Record<number, string> = {
    1: 'Hatırlama', 2: 'Anlama', 3: 'Uygulama',
    4: 'Analiz', 5: 'Değerlendirme', 6: 'Yaratma'
  };

  readonly bloomLevels = [1, 2, 3, 4, 5, 6];

  readonly recommendationTypeLabels: Record<string, string> = {
    WeakAreaTargeted: 'Zayıf Alan Odaklı',
    DifficultyProgression: 'Seviye Atlatma',
    CategoryDiversity: 'Kategori Çeşitlendirme'
  };

  readonly recommendationTypeIcons: Record<string, string> = {
    WeakAreaTargeted: 'psychology',
    DifficultyProgression: 'trending_up',
    CategoryDiversity: 'diversity_3'
  };

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    const userId = this.authService.currentUserValue?.id ?? '';

    forkJoin({
      dashboard: this.adaptiveService.getDashboard(),
      weakAreas: this.adaptiveService.getWeakAreas(),
      recommendations: this.adaptiveService.getRecommendations(10),
      readingProfile: this.adaptiveTextService.getProfile(userId)
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => {
          this.dashboard = r.dashboard;
          this.weakAreas = r.weakAreas;
          this.recommendations = r.recommendations;
          this.readingProfile = r.readingProfile;
          this.hasData = !!(r.dashboard?.profile?.totalReadingSessions > 0 || r.dashboard?.profile?.totalExerciseSessions > 0);
        },
        error: () => {
          this.hasData = false;
        }
      });
  }

  getBloomScore(level: number): number {
    return this.dashboard?.profile?.bloomPerformance?.[level] ?? 0;
  }

  isWeakBloom(level: number): boolean {
    return this.getBloomScore(level) < 70 && this.getBloomScore(level) > 0;
  }

  isStrongBloom(level: number): boolean {
    return this.getBloomScore(level) >= 70;
  }

  getBloomWeakArea(level: number): WeakArea | undefined {
    return this.weakAreas.find(w => w.bloomLevel === level);
  }

  getBloomBarColor(level: number): string {
    const score = this.getBloomScore(level);
    if (score === 0) return 'primary';
    if (score < 50) return 'warn';
    if (score < 70) return 'accent';
    return 'primary';
  }

  getDifficultyLabel(level: number): string {
    return this.adaptiveTextService.getLevelDescription(level);
  }

  getSpeedLabel(wpm: number): string {
    return this.adaptiveTextService.getSpeedDescription(wpm);
  }

  formatTime(seconds: number): string {
    return this.adaptiveTextService.formatReadingTime(seconds);
  }

  estimatedReadingTime(wordCount: number, wpm: number): string {
    if (!wpm || wpm === 0) wpm = 200;
    const minutes = Math.ceil(wordCount / wpm);
    return `~${minutes} dk`;
  }

  get dailyGoalPct(): number {
    return Math.min(100, this.dashboard?.todayGoal?.achievementPercentage ?? 0);
  }

  get minutesPct(): number {
    const g = this.dashboard?.todayGoal;
    if (!g || g.targetMinutes === 0) return 0;
    return Math.min(100, Math.round((g.actualMinutes / g.targetMinutes) * 100));
  }

  get sessionsPct(): number {
    const g = this.dashboard?.todayGoal;
    if (!g || g.targetReadingSessions === 0) return 0;
    return Math.min(100, Math.round((g.actualReadingSessions / g.targetReadingSessions) * 100));
  }

  get exercisesPct(): number {
    const g = this.dashboard?.todayGoal;
    if (!g || g.targetExercises === 0) return 0;
    return Math.min(100, Math.round((g.actualExercises / g.targetExercises) * 100));
  }

  get totalMinutesFormatted(): string {
    const m = this.dashboard?.profile?.totalMinutesSpent ?? 0;
    const h = Math.floor(m / 60);
    const mins = m % 60;
    return h > 0 ? `${h}s ${mins}dk` : `${m}dk`;
  }
}
