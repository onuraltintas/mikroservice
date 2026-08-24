import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTabsModule } from '@angular/material/tabs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface PlatformStats {
  totalActiveStudents: number;
  averageSuccessRate: number;
  averageCurrentStreak: number;
  totalCompletedExercises: number;
}

interface ProgramDistribution {
  programName: string;
  studentCount: number;
  percentage: number;
}

interface WeeklyProgress {
  weekNumber: number;
  averageProgress: number;
  completionRate: number;
}

interface StudentProgress {
  studentName: string;
  studentEmail: string;
  programName: string;
  currentWeek: number;
  currentDay: number;
  currentStreak: number;
  longestStreak: number;
  successRate: number;
  difficultyLevel: number;
  lastActivityDate: string;
}

interface AnalyticsResponse {
  platformStats: PlatformStats;
  programDistribution: ProgramDistribution[];
  weeklyProgress: WeeklyProgress[];
  recentStudentProgress: StudentProgress[];
}

/**
 * Analytics Dashboard Component
 * Display platform-wide analytics and student progress
 */
@Component({
  selector: 'app-program-analytics',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTabsModule
  ],
  templateUrl: './program-analytics.component.html',
  styleUrls: ['./program-analytics.component.scss']
})
export class ProgramAnalyticsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  analytics = signal<AnalyticsResponse | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);

  displayedColumns: string[] = ['student', 'program', 'progress', 'streak', 'successRate', 'lastActivity'];

  ngOnInit(): void {
    this.loadAnalytics();
  }

  loadAnalytics(): void {
    this.loading.set(true);
    this.error.set(null);

    const url = `${environment.apiUrl}/student-progress/analytics/dashboard`;

    this.http.get<AnalyticsResponse>(url)
      .subscribe({
        next: (response) => {
          // Backend returns data directly, not wrapped in ApiResponse
          this.analytics.set(response);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('[ProgramAnalytics] Error loading analytics:', err);
          console.error('[ProgramAnalytics] Error status:', err.status);
          console.error('[ProgramAnalytics] Error message:', err.message);
          this.error.set('Analitik veriler yüklenirken hata oluştu');
          this.loading.set(false);
        }
      });
  }

  refreshData(): void {
    this.loadAnalytics();
  }

  getSuccessRateClass(rate: number): string {
    if (rate >= 80) return 'success-high';
    if (rate >= 60) return 'success-medium';
    return 'success-low';
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Bugün';
    if (diffDays === 1) return 'Dün';
    if (diffDays < 7) return `${diffDays} gün önce`;

    return date.toLocaleDateString('tr-TR');
  }
}
