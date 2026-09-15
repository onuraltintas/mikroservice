import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { takeUntil, finalize, map, switchMap } from 'rxjs/operators';
import { TeachersService } from '../../../core/services/teachers.service';
import { StudentsService } from '../../../core/services/students.service';
import { ReportsService } from '../../../core/services/reports.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { BaseComponent } from '../../../core/components/base.component';
import { Student } from '../../../core/models/student.model';
import { LineChartComponent } from '../../../shared/components/charts/line-chart.component';
import { StudentCoachingTabComponent } from './coaching-tab/student-coaching-tab.component';

interface StudentStats {
  totalExercises: number;
  completedExercises: number;
  averageKDP: number;
  averageComprehension: number;
  totalTimeMinutes: number;
  currentStreak: number;
  goalCompletionRate: number;
  programState?: {
    programName: string;
    currentDay: number;
    currentWeek: number;
    difficultyLevel: number;
    adaptiveDifficultyOffset: number;
    daysCompleted: number;
    totalDays: number;
    averageSuccessRate: number;
  };
}

interface RecentActivity {
  date: string;
  exerciseName: string;
  activityType: string;
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
  private teachersService = inject(TeachersService);
  private studentsService = inject(StudentsService);
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
    const endDate = new Date();
    const startDate = new Date(endDate.getTime() - 90 * 24 * 60 * 60 * 1000); // Last 90 days
    const teacherId = this.authService.currentUserValue?.id;
    if (!teacherId) {
      this.loading.set(false);
      return;
    }

    const roster$ = this.isInstitutionViewer()
      ? this.studentsService.getInstitutionStudents()
      : this.teachersService.getMyStudents();

    roster$.pipe(
      map(students => students.find(student => student.id === studentId) ?? null),
      switchMap(student => {
        if (!student) {
          this.student = null;
          return of(null);
        }

        this.student = student;
        return this.reportsService.getTeacherStudentDetailReport(teacherId, studentId, startDate, endDate);
      }),
      takeUntil(this.destroy$),
      finalize(() => this.loading.set(false))
    )
      .subscribe({
        next: report => {
          if (report) {
            this.applyReport(report);
          }
        },
        error: (err) => {
          console.error('Error loading student data:', err);
          this.student = null;
        }
      });
  }

  private isInstitutionViewer(): boolean {
    return this.authService.hasRole('InstitutionAdmin') || this.authService.hasRole('InstitutionOwner');
  }

  private applyReport(report: any): void {
    const dashboard = report.studentReports.dashboard;
    const readingSpeed = report.studentReports.readingSpeed;
    const comprehension = report.studentReports.comprehension;
    const activity = report.studentReports.activity;

    this.stats = {
      totalExercises: dashboard.totalActivities,
      completedExercises: dashboard.exercisesCompleted ?? 0,
      averageKDP: Math.round(readingSpeed.statistics.averageWPM),
      averageComprehension: Math.round(comprehension.overallComprehension),
      totalTimeMinutes: activity.studyTime.totalMinutes,
      currentStreak: activity.currentStreak.days,
      goalCompletionRate: dashboard.goalCompletionRate,
      programState: dashboard.programState
    };
    this.seriesData = report.studentReports.series.activeSeries || [];

    const progress = readingSpeed.wpmTrendChart?.data ?? [];
    this.recentActivities = (activity.recentActivities ?? []).map((item: any) => ({
      date: item.completedAt,
      exerciseName: item.activityType === 'reading'
        ? `Metin: ${item.contentTitle}`
        : `Egzersiz: ${item.contentTitle}`,
      activityType: item.activityType,
      kdp: item.wpm ?? 0,
      comprehension: item.comprehension ?? item.successRate ?? 0,
      duration: Math.round((item.durationSeconds ?? 0) / 60)
    }));
    this.kdpChartData = [{ name: 'KDP', series: progress }];
    this.comprehensionChartData = [{
      name: 'Anlama Oranı',
      series: comprehension.comprehensionTrend?.data ?? []
    }];
    this.setupReferenceLines();
  }

  setupReferenceLines(): void {
    if (this.student) {
      // KDP reference line
      this.kdpReferenceLines = this.student.targetWPM && this.student.targetWPM > 0
        ? [{ name: 'Hedef', value: this.student.targetWPM }]
        : [];

      // Comprehension reference line
      this.comprehensionReferenceLines = this.student.targetComprehension && this.student.targetComprehension > 0
        ? [{ name: 'Hedef', value: this.student.targetComprehension }]
        : [];
    }
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
