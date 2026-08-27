import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, distinctUntilChanged, takeUntil, finalize, catchError, map } from 'rxjs/operators';
import { StudentsService } from '../../../core/services/students.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { BaseComponent } from '../../../core/components/base.component';
import { Student, StudentExerciseResult } from '../../../core/models/student.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { StudentCoachingTabComponent } from './coaching-tab/student-coaching-tab.component';

interface StudentStats {
  totalExercises: number;
  completedExercises: number;
  averageKDP: number;
  averageComprehension: number;
  totalTimeMinutes: number;
  currentStreak: number;
}

interface RecentActivity {
  date: string;
  exerciseName: string;
  kdp: number;
  comprehension: number;
  duration: number;
}

@Component({
  selector: 'app-teacher-student-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatTabsModule,
    MatTableModule,
    MatProgressSpinnerModule,
    LineChartComponent,
    StudentCoachingTabComponent,
  ],
  templateUrl: './student-detail.component.html',
  styleUrls: ['./student-detail.component.scss']
})
export class StudentDetailComponent extends BaseComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private studentsService = inject(StudentsService);
  private teachersService = inject(TeachersService); // Injected but used via fallback strategy
  private reportsService = inject(ReportsService);
  private authService = inject(AuthService);
  protected override toaster = inject(ToasterService);

  student: Student | null = null;
  stats: StudentStats | null = null;
  recentActivities: RecentActivity[] = [];
  seriesData: any[] = []; // For Series Progress tab

  activityColumns: string[] = ['date', 'exercise', 'kdp', 'comprehension', 'duration'];

  // Chart data
  kdpChartData: any[] = [];
  comprehensionChartData: any[] = [];
  chartView: [number, number] = [700, 300];

  // Reference lines
  kdpReferenceLines: any[] = [];
  comprehensionReferenceLines: any[] = [];

  ngOnInit(): void {
    const studentId = this.route.snapshot.paramMap.get('id');
    if (studentId) {
      this.loadStudentData(studentId);
    }
  }

  override ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadStudentData(studentId: string): void {
    this.loading.set(true);

    const currentUser = this.authService.currentUserValue;
    if (!currentUser) return;

    const teacherId = currentUser.id;

    // Check if user is Admin, they might not have a Teacher profile
    if (this.authService.hasAdminAccess()) {
      console.warn('Admin user accessing teacher view. Some data may be unavailable.');
    }

    // Robust fetching strategy: Try Teacher endpoint -> Generic endpoint
    const resultsObservable = this.studentsService.getStudentExerciseResults(teacherId, studentId).pipe(
      catchError(err => {
        console.warn('Teacher endpoint failed, trying generic student endpoint...', err);
        return this.studentsService.getStudentResults(studentId).pipe(
          catchError(fallbackErr => {
            console.error('Generic endpoint also failed:', fallbackErr);
            return of([]); // Return empty if both fail
          })
        );
      })
    );

    // Fetch Series Report
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 90 * 24 * 60 * 60 * 1000); // Last 90 days
    const reportObservable = this.reportsService.getTeacherStudentDetailReport(teacherId, studentId, startDate, endDate).pipe(
      catchError(err => {
        console.error('Failed to load student report:', err);
        return of(null);
      })
    );

    forkJoin({
      student: this.studentsService.getStudentById(studentId),
      results: resultsObservable,
      report: reportObservable
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ student, results, report }) => {
          this.student = student;

          // Process Exercise Results
          if (results && results.length > 0) {
            this.calculateStats(results);
            this.prepareRecentActivities(results);
            this.prepareChartData(results);
            this.setupReferenceLines();
          } else {
            // If no results, init empty stats
            this.stats = {
              totalExercises: 0,
              completedExercises: 0,
              averageKDP: 0,
              averageComprehension: 0,
              totalTimeMinutes: 0,
              currentStreak: 0
            };
          }

          // Process Series Data from Report
          if (report && report.studentReports && report.studentReports.series) {
            this.seriesData = report.studentReports.series.activeSeries || [];
          }

          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading student data:', err);
          this.loading.set(false);
        }
      });
  }

  calculateStats(results: StudentExerciseResult[]): void {
    const completedResults = results.filter(r => r.isCompleted);
    const totalTime = completedResults.reduce((sum, r) => sum + (r.timeSpentSeconds || 0), 0);

    this.stats = {
      totalExercises: results.length,
      completedExercises: completedResults.length,
      averageKDP: Math.round(completedResults.reduce((sum, r) => sum + (r.wordsPerMinute || 0), 0) / completedResults.length) || 0,
      averageComprehension: Math.round(completedResults.reduce((sum, r) => sum + (r.comprehensionScore || 0), 0) / completedResults.length) || 0,
      totalTimeMinutes: Math.round(totalTime / 60),
      currentStreak: this.calculateStreak(results)
    };
  }

  calculateStreak(results: any[]): number {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const dates = results
      .map(r => {
        const date = new Date(r.completedAt);
        date.setHours(0, 0, 0, 0);
        return date.getTime();
      })
      .filter((date, index, self) => self.indexOf(date) === index)
      .sort((a, b) => b - a);

    let streak = 0;
    let currentDate = today.getTime();

    for (const date of dates) {
      if (date === currentDate) {
        streak++;
        currentDate -= 86400000; // 1 day in milliseconds
      } else if (date < currentDate) {
        break;
      }
    }

    return streak;
  }

  prepareRecentActivities(results: StudentExerciseResult[]): void {
    this.recentActivities = results
      .filter(r => r.isCompleted)
      .sort((a, b) => new Date(b.completedAt).getTime() - new Date(a.completedAt).getTime())
      .slice(0, 10)
      .map(r => ({
        date: r.completedAt,
        exerciseName: r.exerciseTitle || 'Egzersiz',
        kdp: r.wordsPerMinute || 0,
        comprehension: r.comprehensionScore || 0,
        duration: Math.round((r.timeSpentSeconds || 0) / 60)
      }));
  }

  prepareChartData(results: StudentExerciseResult[]): void {
    // Show all completed results (no date filter)
    const allResults = results
      .filter(r => r.isCompleted)
      .sort((a, b) => new Date(a.completedAt).getTime() - new Date(b.completedAt).getTime());

    if (allResults.length === 0) {
      this.kdpChartData = [];
      this.comprehensionChartData = [];
      return;
    }

    // Prepare KDP chart data
    const kdpData = allResults.map(r => ({
      name: this.formatDateForChart(r.completedAt),
      value: r.wordsPerMinute || 0
    }));

    this.kdpChartData = [{
      name: 'KDP',
      series: kdpData
    }];

    // Prepare Comprehension chart data
    const comprehensionData = allResults.map(r => ({
      name: this.formatDateForChart(r.completedAt),
      value: r.comprehensionScore || 0
    }));

    this.comprehensionChartData = [{
      name: 'Anlama Oranı',
      series: comprehensionData
    }];
  }

  setupReferenceLines(): void {
    if (this.student) {
      // KDP reference line
      this.kdpReferenceLines = [{
        name: 'Hedef',
        value: this.student.targetWPM
      }];

      // Comprehension reference line
      this.comprehensionReferenceLines = [{
        name: 'Hedef',
        value: this.student.targetComprehension
      }];
    }
  }

  formatDateForChart(dateString: string): string {
    const date = new Date(dateString);
    return `${date.getDate()}/${date.getMonth() + 1}`;
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
  }

  formatDate(date: Date | string): string {
    return new Date(date).toLocaleDateString('tr-TR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  formatLastLogin(lastLoginAt?: Date): string {
    if (!lastLoginAt) return 'Hiç giriş yapmadı';

    const now = new Date();
    const loginDate = new Date(lastLoginAt);
    const diffMs = now.getTime() - loginDate.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Bugün';
    if (diffDays === 1) return 'Dün';
    if (diffDays < 7) return `${diffDays} gün önce`;

    return this.formatDate(lastLoginAt);
  }
}
