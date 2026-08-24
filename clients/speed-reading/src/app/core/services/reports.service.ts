import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  StudentDashboardReport,
  RecentMilestone,
  StudentReadingSpeedReport,
  StudentComprehensionReport,
  StudentSeriesReport,
  StudentActivityReport,
  TeacherClassOverviewReport,
  TeacherStudentDetailReport,
  TeacherAssignmentReport,
  TeacherContentAnalysisReport,
  TeacherTimeBasedProgressReport,
  AdminInstitutionReport,
  AdminPlatformUsageReport,
  AdminContentAnalysisReport,
  AdminSystemHealthReport,
  AdaptivePerformanceReport
} from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/reports`;
  private speedReadingApiUrl = `${environment.speedReadingApiUrl}/analytics`;

  // ==================== STUDENT REPORTS ====================

  getStudentDashboardReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentDashboardReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeDashboardStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    // The central endpoint derives the user from the access token. The legacy
    // studentId argument is kept for component compatibility and is ignored.
    void studentId;
    return this.http.get<StudentAnalyticsSummary>(`${this.speedReadingApiUrl}/student/summary`, { params })
      .pipe(map(summary => this.toStudentDashboardReport(summary)));
  }

  getStudentReadingSpeedReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentReadingSpeedReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentReadingSpeedReport>(`${this.apiUrl}/student/reading-speed`, { params });
  }

  getStudentComprehensionReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentComprehensionReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentComprehensionReport>(`${this.apiUrl}/student/comprehension`, { params });
  }

  getStudentSeriesReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentSeriesReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentSeriesReport>(`${this.apiUrl}/student/series`, { params });
  }

  getStudentActivityReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentActivityReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentActivityReport>(`${this.apiUrl}/student/activity`, { params });
  }

  getAdaptivePerformanceReport(
    studentId: string,
    startDate?: Date,
    endDate?: Date,
    exerciseType?: string
  ): Observable<AdaptivePerformanceReport> {
    let params = new HttpParams();
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());
    if (exerciseType) params = params.set('exerciseType', exerciseType);

    return this.http.get<AdaptivePerformanceReport>(
      `${this.apiUrl}/student/adaptive-performance/${studentId}`,
      { params }
    );
  }

  // ==================== TEACHER REPORTS ====================

  getTeacherClassOverviewReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherClassOverviewReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherClassOverviewReport>(`${this.apiUrl}/teacher/class-overview`, { params });
  }

  getTeacherStudentDetailReport(teacherId: string, studentId: string, startDate: Date, endDate: Date): Observable<TeacherStudentDetailReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherStudentDetailReport>(`${this.apiUrl}/teacher/student-detail`, { params });
  }

  getTeacherAssignmentReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherAssignmentReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherAssignmentReport>(`${this.apiUrl}/teacher/assignments`, { params });
  }

  getTeacherContentAnalysisReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherContentAnalysisReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherContentAnalysisReport>(`${this.apiUrl}/teacher/content-analysis`, { params });
  }

  getTeacherTimeBasedProgressReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherTimeBasedProgressReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherTimeBasedProgressReport>(`${this.apiUrl}/teacher/time-progress`, { params });
  }

  // ==================== ADMIN REPORTS ====================

  getAdminInstitutionReport(startDate: Date, endDate: Date): Observable<AdminInstitutionReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminInstitutionReport>(`${this.apiUrl}/admin/institutions`, { params });
  }

  getAdminPlatformUsageReport(startDate: Date, endDate: Date): Observable<AdminPlatformUsageReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminPlatformUsageReport>(`${this.apiUrl}/admin/platform-usage`, { params });
  }

  getAdminContentAnalysisReport(startDate: Date, endDate: Date): Observable<AdminContentAnalysisReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminContentAnalysisReport>(`${this.apiUrl}/admin/content-analysis`, { params });
  }

  getAdminSystemHealthReport(startDate: Date, endDate: Date): Observable<AdminSystemHealthReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminSystemHealthReport>(`${this.apiUrl}/admin/system-health`, { params });
  }

  // ==================== EXPORT ====================

  exportReportToPdf(reportData: any): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/pdf`, reportData, { responseType: 'blob' });
  }

  exportReportToExcel(reportData: any): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/excel`, reportData, { responseType: 'blob' });
  }

  private normalizeDashboardStart(startDate: Date, endDate: Date): Date {
    const maximumStart = new Date(endDate.getTime() - 366 * 24 * 60 * 60 * 1000);
    return startDate < maximumStart ? maximumStart : startDate;
  }

  private toStudentDashboardReport(summary: StudentAnalyticsSummary): StudentDashboardReport {
    const daily = summary.daily ?? [];
    const metadataStart = new Date(summary.dateFrom);
    const metadataEnd = new Date(summary.dateTo);
    const activityTrend = daily.map(point => ({
      name: point.date,
      series: [{ name: 'Aktivite', value: point.readingSessions + point.exerciseCount }]
    }));
    const wpmProgress = daily.map(point => ({
      name: point.date,
      series: [{ name: 'WPM', value: point.averageWpm }]
    }));
    const comprehensionTrend = daily.map(point => ({
      name: point.date,
      series: [{ name: 'Anlama', value: point.averageComprehension }]
    }));

    return {
      metadata: {
        reportId: 'student-analytics-summary',
        reportType: 'Student',
        generatedAt: new Date(),
        generatedBy: 'self',
        startDate: metadataStart,
        endDate: metadataEnd
      },
      totalActivities: summary.readingSessions + summary.exercisesCompleted,
      totalReadingTime: summary.totalReadingMinutes,
      currentWPM: summary.latestWpm,
      averageComprehension: summary.averageComprehension,
      currentLevel: summary.currentLevel,
      daysActive: daily.filter(point => point.readingSessions > 0 || point.exerciseCount > 0).length,
      dailyGoalMinutes: summary.dailyGoalMinutes,
      goalCompletionRate: summary.goalCompletionRate,
      streak: summary.currentStreak,
      longestStreak: summary.longestStreak,
      totalXP: summary.totalXp,
      milestonesEarned: summary.milestonesEarned,
      activityTrend,
      wpmProgress,
      comprehensionTrend,
      activityDistribution: [
        { name: 'Okuma', series: [{ name: 'Oturum', value: summary.readingSessions }] },
        { name: 'Egzersiz', series: [{ name: 'Tamamlanan', value: summary.exercisesCompleted }] }
      ],
      recentMilestones: (summary.recentMilestones ?? []).map(milestone => ({
        id: milestone.id,
        title: milestone.title,
        description: milestone.description,
        earnedAt: new Date(milestone.earnedAt),
        type: this.toMilestoneType(milestone.type),
        icon: milestone.icon || undefined
      }))
    };
  }

  private toMilestoneType(type: string): RecentMilestone['type'] {
    switch (type) {
      case 'speed':
      case 'comprehension':
      case 'streak':
      case 'completion':
      case 'achievement':
        return type;
      default:
        return 'achievement';
    }
  }
}

interface StudentAnalyticsSummary {
  dateFrom: string;
  dateTo: string;
  readingSessions: number;
  averageWpm: number;
  averageComprehension: number;
  totalReadingMinutes: number;
  exercisesCompleted: number;
  latestWpm: number;
  latestComprehension: number;
  currentLevel: number;
  currentStreak: number;
  longestStreak: number;
  totalXp: number;
  milestonesEarned: number;
  dailyGoalMinutes: number;
  goalCompletionRate: number;
  recentMilestones: StudentAnalyticsMilestone[];
  daily: StudentAnalyticsDailyPoint[];
}

interface StudentAnalyticsMilestone {
  id: string;
  title: string;
  description: string;
  earnedAt: string;
  type: string;
  icon: string;
}

interface StudentAnalyticsDailyPoint {
  date: string;
  readingSessions: number;
  exerciseCount: number;
  averageWpm: number;
  averageComprehension: number;
}
