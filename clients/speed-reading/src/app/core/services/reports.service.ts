import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  StudentDashboardReport,
  ReportMetadata,
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
  AdaptivePerformanceReport,
  ChartData,
  PopularContent,
  InstitutionActivity,
  ContentUsageData,
  ExerciseTypeAnalysis
} from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/reports`;
  private reportExportApiUrl = `${environment.speedReadingApiUrl}/reports`;
  private speedReadingApiUrl = `${environment.speedReadingApiUrl}/analytics`;

  // ==================== STUDENT REPORTS ====================

  getStudentDashboardReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentDashboardReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    // The central endpoint derives the user from the access token. The legacy
    // studentId argument is kept for component compatibility and is ignored.
    void studentId;
    return this.http.get<StudentAnalyticsSummary>(`${this.speedReadingApiUrl}/student/summary`, { params })
      .pipe(map(summary => this.toStudentDashboardReport(summary)));
  }

  getStudentReadingSpeedReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentReadingSpeedReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void studentId;
    return this.http.get<StudentReadingSpeedAnalytics>(`${this.speedReadingApiUrl}/student/reading-speed`, { params })
      .pipe(map(value => this.toStudentReadingSpeedReport(value)));
  }

  getStudentComprehensionReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentComprehensionReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void studentId;
    return this.http.get<StudentComprehensionAnalytics>(`${this.speedReadingApiUrl}/student/comprehension`, { params })
      .pipe(map(value => this.toStudentComprehensionReport(value)));
  }

  getStudentSeriesReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentSeriesReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void studentId;
    return this.http.get<StudentSeriesAnalytics>(`${this.speedReadingApiUrl}/student/series`, { params })
      .pipe(map(value => this.toStudentSeriesReport(value)));
  }

  getStudentActivityReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentActivityReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void studentId;
    return this.http.get<StudentActivityAnalytics>(`${this.speedReadingApiUrl}/student/activity`, { params })
      .pipe(map(value => this.toStudentActivityReport(value)));
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
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void teacherId;
    return this.http.get<TeacherClassOverviewAnalytics>(
      `${this.speedReadingApiUrl}/teacher/class-overview`,
      { params })
      .pipe(map(value => this.toTeacherClassOverviewReport(value)));
  }

  getAdminTeacherClassOverviewReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherClassOverviewReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    return this.http.get<TeacherClassOverviewAnalytics>(
      `${this.speedReadingApiUrl}/admin/teachers/${teacherId}/class-overview`,
      { params })
      .pipe(map(value => this.toTeacherClassOverviewReport(value)));
  }

  getTeacherStudentDetailReport(teacherId: string, studentId: string, startDate: Date, endDate: Date): Observable<TeacherStudentDetailReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void teacherId;

    const baseUrl = `${this.speedReadingApiUrl}/teacher/students/${studentId}`;
    return forkJoin({
      dashboard: this.http.get<StudentAnalyticsSummary>(`${baseUrl}/summary`, { params }),
      readingSpeed: this.http.get<StudentReadingSpeedAnalytics>(`${baseUrl}/reading-speed`, { params }),
      comprehension: this.http.get<StudentComprehensionAnalytics>(`${baseUrl}/comprehension`, { params }),
      series: this.http.get<StudentSeriesAnalytics>(`${baseUrl}/series`, { params }),
      activity: this.http.get<StudentActivityAnalytics>(`${baseUrl}/activity`, { params })
    }).pipe(map(value => {
      const dashboard = this.toStudentDashboardReport(value.dashboard);
      const readingSpeed = this.toStudentReadingSpeedReport(value.readingSpeed);
      const comprehension = this.toStudentComprehensionReport(value.comprehension);
      const series = this.toStudentSeriesReport(value.series);
      const activity = this.toStudentActivityReport(value.activity);

      return {
        metadata: this.toReportMetadata('teacher-student-detail', value.dashboard.dateFrom, value.dashboard.dateTo, 'Teacher'),
        studentInfo: {
          studentId,
          studentName: '',
          enrollmentDate: new Date(0),
          lastActivity: activity.currentStreak.lastActivityDate ?? new Date(0)
        },
        comparisonChart: {
          data: [
            { name: 'WPM', value: readingSpeed.currentWPM },
            { name: 'Anlama', value: comprehension.overallComprehension },
            { name: 'Aktivite', value: dashboard.totalActivities },
            { name: 'Seri', value: series.summary.seriesCompleted }
          ],
          labels: ['WPM', 'Anlama', 'Aktivite', 'Seri']
        },
        studentReports: { dashboard, readingSpeed, comprehension, series, activity },
        strengths: value.comprehension.strongAreas ?? [],
        weaknesses: value.comprehension.weakAreas ?? [],
        recommendations: value.readingSpeed.recommendations ?? []
      } as TeacherStudentDetailReport;
    }));
  }

  getTeacherAssignmentReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherAssignmentReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void teacherId;
    return this.http.get<TeacherAssignmentAnalytics>(
      `${this.speedReadingApiUrl}/teacher/assignments`,
      { params })
      .pipe(map(value => this.toTeacherAssignmentReport(value)));
  }

  getTeacherContentAnalysisReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherContentAnalysisReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void teacherId;
    return this.http.get<TeacherContentAnalysisAnalytics>(
      `${this.speedReadingApiUrl}/teacher/content-analysis`,
      { params })
      .pipe(map(value => this.toTeacherContentAnalysisReport(value)));
  }

  getTeacherTimeBasedProgressReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherTimeBasedProgressReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    void teacherId;
    return this.http.get<TeacherTimeProgressAnalytics>(
      `${this.speedReadingApiUrl}/teacher/time-progress`,
      { params })
      .pipe(map(value => this.toTeacherTimeProgressReport(value)));
  }

  // ==================== ADMIN REPORTS ====================

  getAdminInstitutionReport(startDate: Date, endDate: Date): Observable<AdminInstitutionReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    return this.http.get<AdminInstitutionAnalytics>(
      `${this.speedReadingApiUrl}/admin/institutions`,
      { params })
      .pipe(map(value => this.toAdminInstitutionReport(value)));
  }

  getAdminPlatformUsageReport(startDate: Date, endDate: Date): Observable<AdminPlatformUsageReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    return this.http.get<AdminPlatformUsageAnalytics>(`${this.speedReadingApiUrl}/admin/platform-usage`, { params })
      .pipe(map(value => this.toAdminPlatformUsageReport(value)));
  }

  getAdminContentAnalysisReport(startDate: Date, endDate: Date): Observable<AdminContentAnalysisReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    return this.http.get<AdminContentAnalysisAnalytics>(
      `${this.speedReadingApiUrl}/admin/content-analysis`,
      { params })
      .pipe(map(value => this.toAdminContentAnalysisReport(value)));
  }

  getAdminSystemHealthReport(startDate: Date, endDate: Date): Observable<AdminSystemHealthReport> {
    const params = new HttpParams()
      .set('dateFrom', this.normalizeAnalyticsStart(startDate, endDate).toISOString())
      .set('dateTo', endDate.toISOString());
    return this.http.get<AdminSystemHealthAnalytics>(
      `${this.speedReadingApiUrl}/admin/system-health`,
      { params })
      .pipe(map(value => this.toAdminSystemHealthReport(value)));
  }

  // ==================== EXPORT ====================

  exportReportToPdf(reportData: any): Observable<Blob> {
    return this.http.post(`${this.reportExportApiUrl}/export/pdf`, reportData, { responseType: 'blob' });
  }

  exportReportToExcel(reportData: any): Observable<Blob> {
    return this.http.post(`${this.reportExportApiUrl}/export/excel`, reportData, { responseType: 'blob' });
  }

  private normalizeAnalyticsStart(startDate: Date, endDate: Date): Date {
    const maximumStart = new Date(endDate.getTime() - 366 * 24 * 60 * 60 * 1000);
    return startDate < maximumStart ? maximumStart : startDate;
  }

  private toStudentReadingSpeedReport(value: StudentReadingSpeedAnalytics): StudentReadingSpeedReport {
    const trend = value.trend ?? [];
    return {
      metadata: this.toReportMetadata('student-reading-speed', value.dateFrom, value.dateTo),
      currentWPM: value.currentWpm,
      wpmBenchmarks: [
        { label: 'Öğrenci ortalaması', value: value.benchmark.studentValue },
        { label: 'Kurum ortalaması', value: value.benchmark.institutionAverage },
        { label: 'Platform ortalaması', value: value.benchmark.platformAverage }
      ],
      wpmTrendChart: {
        data: trend.map(point => ({ name: point.date, value: point.value })),
        labels: trend.map(point => point.date)
      },
      categoryWPMChart: {
        data: value.categories.map(category => ({
          name: category.categoryName,
          series: [{ name: 'WPM', value: category.value }]
        }))
      },
      statistics: {
        averageWPM: value.averageWpm,
        medianWPM: value.medianWpm,
        minWPM: value.minWpm,
        maxWPM: value.maxWpm,
        standardDeviation: value.standardDeviation,
        improvementRate: value.improvementRate,
        totalReadings: value.sessionsBelow200Wpm + value.sessions200To400Wpm + value.sessionsAbove400Wpm
      }
    };
  }

  private toStudentComprehensionReport(value: StudentComprehensionAnalytics): StudentComprehensionReport {
    const categories = value.categories ?? [];
    const questionTypes = value.questionTypes ?? [];
    return {
      metadata: this.toReportMetadata('student-comprehension', value.dateFrom, value.dateTo),
      overallComprehension: value.averageComprehension,
      comprehensionBenchmarks: [
        { label: 'Öğrenci ortalaması', value: value.benchmark.studentValue },
        { label: 'Kurum ortalaması', value: value.benchmark.institutionAverage },
        { label: 'Platform ortalaması', value: value.benchmark.platformAverage }
      ],
      questionTypeChart: {
        data: questionTypes.map(item => ({ name: item.type, value: item.value })),
        labels: questionTypes.map(item => item.type)
      },
      categoryComprehensionChart: {
        data: categories.map(category => ({
          name: category.categoryName,
          series: [{ name: 'Anlama', value: category.value }]
        }))
      },
      categoryBreakdown: categories.map(category => ({
        categoryId: category.categoryName,
        categoryName: category.categoryName,
        comprehensionRate: category.value,
        questionsAnswered: category.questionsAttempted,
        correctAnswers: category.correctAnswers,
        averageTime: 0
      })),
      improvementAreas: (value.weakAreas ?? []).map(area => ({
        area,
        currentLevel: categories.find(category => category.categoryName === area)?.value ?? 0,
        targetLevel: 80,
        recommendations: ['Bu kategoride daha fazla okuma ve anlama egzersizi yapın'],
        priority: 'high' as const
      }))
    };
  }

  private toStudentSeriesReport(value: StudentSeriesAnalytics): StudentSeriesReport {
    const timeline = value.completionTimeline ?? [];
    return {
      metadata: this.toReportMetadata('student-series', value.dateFrom, value.dateTo),
      dataAvailable: value.dataAvailable !== false,
      unavailableReason: value.unavailableReason ?? undefined,
      summary: {
        totalSeriesStarted: value.totalSeriesStarted,
        seriesCompleted: value.seriesCompleted,
        seriesInProgress: value.seriesInProgress,
        totalMilestones: value.totalMilestones
      },
      activeSeries: (value.activeSeries ?? []).map(series => ({
        seriesId: series.seriesId,
        seriesName: series.seriesName,
        progress: series.progress,
        daysCompleted: series.daysCompleted,
        totalDays: series.totalDays,
        startedAt: new Date(series.startedAt),
        lastActivityAt: series.lastActivityAt ? new Date(series.lastActivityAt) : null,
        averageScore: series.averageScore
      })),
      completionTimelineChart: {
        data: timeline.map(point => ({
          name: point.date,
          value: point.value
        })),
        labels: timeline.map(point => point.date)
      },
      milestones: (value.milestones ?? []).map(milestone => ({
        id: milestone.id,
        title: milestone.title,
        description: milestone.description,
        earnedAt: new Date(milestone.earnedAt),
        seriesId: milestone.seriesId ?? '',
        seriesName: milestone.seriesName,
        type: milestone.type,
        icon: milestone.icon || undefined
      })),
      performanceStats: {
        averageCompletionTime: value.performanceStats.averageCompletionTime,
        averageScore: value.performanceStats.averageScore,
        consistencyScore: value.performanceStats.consistencyScore,
        engagementLevel: this.toEngagementLevel(value.performanceStats.engagementLevel)
      }
    };
  }

  private toStudentActivityReport(value: StudentActivityAnalytics): StudentActivityReport {
    return {
      metadata: this.toReportMetadata('student-activity', value.dateFrom, value.dateTo),
      dataAvailable: value.dataAvailable !== false,
      unavailableReason: value.unavailableReason ?? undefined,
      currentStreak: {
        days: value.currentStreak.days,
        longestStreak: value.currentStreak.longestStreak,
        lastActivityDate: value.currentStreak.lastActivityDate
          ? new Date(value.currentStreak.lastActivityDate)
          : null,
        isActive: value.currentStreak.isActive
      },
      activityHeatmap: {
        data: (value.heatmap ?? []).map(point => ({
          date: point.date,
          value: point.value,
          level: Math.max(0, Math.min(4, point.level)) as 0 | 1 | 2 | 3 | 4
        }))
      },
      hourlyDistributionChart: {
        data: (value.hourlyDistribution ?? []).map(point => ({
          name: point.label,
          series: [{ name: 'Aktivite', value: point.value }]
        }))
      },
      dailyDistributionChart: {
        data: (value.dailyDistribution ?? []).map(point => ({
          name: point.label,
          series: [{ name: 'Aktivite', value: point.value }]
        }))
      },
      studyTime: {
        totalMinutes: value.studyTime.totalMinutes,
        averageSessionLength: value.studyTime.averageSessionLength,
        totalSessions: value.studyTime.totalSessions,
        mostActiveHour: value.studyTime.mostActiveHour,
        mostActiveDay: value.studyTime.mostActiveDay,
        consistency: value.studyTime.consistency
      }
    };
  }

  private toEngagementLevel(value: string): 'high' | 'medium' | 'low' {
    return value === 'high' || value === 'medium' ? value : 'low';
  }

  private toReportMetadata(
    reportId: string,
    dateFrom: string,
    dateTo: string,
    reportType = 'Student'
  ): ReportMetadata {
    return {
      reportId,
      reportType,
      generatedAt: new Date(),
      generatedBy: 'self',
      startDate: new Date(dateFrom),
      endDate: new Date(dateTo)
    };
  }

  private toAdminPlatformUsageReport(value: AdminPlatformUsageAnalytics): AdminPlatformUsageReport {
    return {
      metadata: this.toReportMetadata(
        'admin-platform-usage',
        value.dateFrom,
        value.dateTo,
        'Admin'),
      totalUsers: value.totalUsers,
      activeUsers: value.activeUsers,
      newUsers: value.newUsers,
      newUserDataAvailable: value.newUserDataAvailable,
      totalActivities: value.totalActivities,
      totalReadingSessions: value.totalReadingSessions,
      averageSessionDuration: value.averageSessionDuration,
      userGrowthRate: value.userGrowthRate,
      userGrowthRateDataAvailable: value.userGrowthRateDataAvailable,
      engagementRate: value.engagementRate,
      retentionRate: value.retentionRate,
      userGrowth: value.userGrowth ?? [],
      dailyActiveUsers: value.dailyActiveUsers ?? [],
      activityVolume: value.activityVolume ?? [],
      hourlyActivity: value.hourlyActivity ?? [],
      popularContent: value.popularContent ?? [],
      topInstitutions: value.topInstitutions ?? [],
      featureUsageStats: value.featureUsageStats ?? {}
    };
  }

  private toTeacherClassOverviewReport(value: TeacherClassOverviewAnalytics): TeacherClassOverviewReport {
    return {
      metadata: this.toReportMetadata('teacher-class-overview', value.dateFrom, value.dateTo, 'Teacher'),
      totalStudents: value.totalStudents,
      activeStudents: value.activeStudents,
      activeStudentsDataAvailable: value.activeStudentsDataAvailable,
      classAverageWpmDataAvailable: value.classAverageWpmDataAvailable,
      classAverageComprehensionDataAvailable: value.classAverageComprehensionDataAvailable,
      classAverageWPM: value.classAverageWpm,
      classAverageComprehension: value.classAverageComprehension,
      totalActivitiesCompleted: value.totalActivitiesCompleted,
      studentsAboveAverage: value.studentsAboveAverage,
      studentsAtAverage: value.studentsAtAverage,
      studentsBelowAverage: value.studentsBelowAverage,
      topPerformers: (value.topPerformers ?? []).map(item => ({
        studentIdentifier: item.studentIdentifier,
        averageWPM: item.averageWpm,
        averageComprehension: item.averageComprehension,
        activitiesCompleted: item.activitiesCompleted,
        totalMinutes: item.totalMinutes,
        performanceLevel: item.performanceLevel
      })),
      studentsNeedingSupport: (value.studentsNeedingSupport ?? []).map(item => ({
        studentIdentifier: item.studentIdentifier,
        averageWPM: item.averageWpm,
        averageComprehension: item.averageComprehension,
        activitiesCompleted: item.activitiesCompleted,
        totalMinutes: item.totalMinutes,
        performanceLevel: item.performanceLevel
      }))
    };
  }

  private toTeacherAssignmentReport(value: TeacherAssignmentAnalytics): TeacherAssignmentReport {
    const assignment = value.assignmentInfo;
    const completion = value.completionStats;
    const performance = value.performanceStats;
    const time = value.timeStats;
    return {
      metadata: this.toReportMetadata('teacher-assignments', value.dateFrom, value.dateTo, 'Teacher'),
      dataAvailable: value.dataAvailable,
      unavailableReason: value.unavailableReason ?? undefined,
      assignmentInfo: assignment ? {
        assignmentId: assignment.assignmentId,
        title: assignment.title,
        description: assignment.description,
        dueDate: new Date(assignment.dueDate),
        assignedDate: new Date(assignment.assignedDate)
      } : { assignmentId: '', title: '', description: '', dueDate: new Date(0), assignedDate: new Date(0) },
      completionStats: completion ?? {
        totalStudents: 0, completed: 0, inProgress: 0, notStarted: 0, completionRate: 0
      },
      performanceStats: performance ?? {
        averageScore: 0, medianScore: 0, highestScore: 0, lowestScore: 0, standardDeviation: 0
      },
      scoreDistribution: { data: value.scoreDistribution ?? [] },
      studentBreakdown: (value.studentBreakdown ?? []).map(item => ({
        studentId: item.studentId,
        studentName: item.studentName,
        status: this.toAssignmentStatus(item.status),
        score: item.score ?? undefined,
        completionTime: item.completionTime ?? undefined,
        submittedAt: item.submittedAt ? new Date(item.submittedAt) : undefined
      })),
      timeStats: time ?? {
        averageCompletionTime: 0, medianCompletionTime: 0, fastestCompletion: 0, slowestCompletion: 0
      }
    };
  }

  private toAssignmentStatus(value: string): 'completed' | 'in-progress' | 'not-started' {
    return value === 'completed' || value === 'in-progress' ? value : 'not-started';
  }

  private toTeacherContentAnalysisReport(value: TeacherContentAnalysisAnalytics): TeacherContentAnalysisReport {
    return {
      metadata: this.toReportMetadata('teacher-content-analysis', value.dateFrom, value.dateTo, 'Teacher'),
      exerciseAnalysis: value.exerciseAnalysis ?? [],
      exerciseFrequencyChart: value.exerciseFrequencyChart ?? [],
      readingAnalysis: (value.readingAnalysis ?? []).map(item => ({
        difficultyLevel: item.difficultyLevel,
        totalReads: item.totalReads,
        averageWPM: item.averageWpm,
        averageComprehension: item.averageComprehension
      })),
      readingPerformanceChart: value.readingPerformanceChart ?? []
    };
  }

  private toTeacherTimeProgressReport(value: TeacherTimeProgressAnalytics): TeacherTimeBasedProgressReport {
    return {
      metadata: this.toReportMetadata('teacher-time-progress', value.dateFrom, value.dateTo, 'Teacher'),
      weeklyProgressChart: value.weeklyProgressChart ?? [],
      monthlyProgressChart: value.monthlyProgressChart ?? [],
      activityIntensityChart: value.activityIntensityChart ?? [],
      improvingStudents: (value.improvingStudents ?? []).map(item => ({
        studentId: item.studentId,
        studentName: item.studentName,
        previousScore: item.previousScore,
        currentScore: item.currentScore,
        improvement: item.improvement,
        trend: item.trend === 'declining' ? 'declining' : 'improving'
      })),
      decliningStudents: (value.decliningStudents ?? []).map(item => ({
        studentId: item.studentId,
        studentName: item.studentName,
        previousScore: item.previousScore,
        currentScore: item.currentScore,
        improvement: item.improvement,
        trend: 'declining'
      }))
    };
  }

  private toAdminInstitutionReport(value: AdminInstitutionAnalytics): AdminInstitutionReport {
    return {
      metadata: this.toReportMetadata(
        'admin-institution-analytics',
        value.dateFrom,
        value.dateTo,
        'Admin'),
      totalInstitutions: value.totalInstitutions,
      activeInstitutions: value.activeInstitutions,
      totalUsers: value.totalUsers,
      totalStudents: value.totalStudents,
      totalTeachers: value.totalTeachers,
      institutionComparison: (value.institutionComparison ?? []).map(item => ({
        institutionId: item.institutionId,
        institutionName: item.institutionName,
        totalUsers: item.totalUsers,
        activeUsers: item.activeUsers,
        totalStudents: item.totalStudents,
        totalTeachers: item.totalTeachers,
        totalActivities: item.totalActivities,
        averageWPM: item.averageWpm,
        averageWPMDataAvailable: item.averageWpmDataAvailable,
        averageComprehension: item.averageComprehension,
        averageComprehensionDataAvailable: item.averageComprehensionDataAvailable,
        averagePerformance: item.averagePerformance,
        engagementRate: item.engagementRate
      })),
      institutionComparisonChart: value.institutionComparisonChart ?? {
        name: 'Kurumlar',
        series: []
      },
      usersByInstitution: value.usersByInstitution ?? [],
      activityByInstitution: value.activityByInstitution ?? [],
      performanceByInstitution: value.performanceByInstitution ?? [],
      topInstitutions: (value.topInstitutions ?? []).map(item => ({
        institutionName: item.institutionName,
        averageWPM: item.averageWpm,
        averageWPMDataAvailable: item.averageWpmDataAvailable,
        averageComprehension: item.averageComprehension,
        averageComprehensionDataAvailable: item.averageComprehensionDataAvailable,
        activeStudents: item.activeStudents,
        activeStudentsDataAvailable: item.activeStudentsDataAvailable,
        totalActivities: item.totalActivities
      }))
    };
  }

  private toAdminContentAnalysisReport(value: AdminContentAnalysisAnalytics): AdminContentAnalysisReport {
    return {
      metadata: this.toReportMetadata(
        'admin-content-analysis',
        value.dateFrom,
        value.dateTo,
        'Admin'),
      totalExercises: value.totalExercises,
      totalReadingTexts: value.totalReadingTexts,
      totalTrainingSeries: value.totalTrainingSeries,
      totalProgramTemplates: value.totalProgramTemplates,
      totalAssignments: value.totalAssignments,
      assignmentDataAvailable: value.assignmentDataAvailable,
      mostUsedContent: value.mostUsedContent ?? [],
      leastUsedContent: value.leastUsedContent ?? [],
      performanceByContentType: value.performanceByContentType ?? [],
      engagementByContentType: value.engagementByContentType ?? [],
      contentGaps: value.contentGaps ?? [],
      popularTopics: value.popularTopics ?? [],
      readingAnalysis: (value.readingAnalysis ?? []).map(item => ({
        difficultyLevel: item.difficultyLevel,
        totalReads: item.totalReads,
        averageWPM: item.averageWpm,
        averageComprehension: item.averageComprehension
      })),
      exerciseAnalysis: value.exerciseAnalysis ?? [],
      readingPerformanceChart: value.readingPerformanceChart ?? [],
      exerciseFrequencyChart: value.exerciseFrequencyChart ?? []
    };
  }

  private toAdminSystemHealthReport(value: AdminSystemHealthAnalytics): AdminSystemHealthReport {
    return {
      metadata: this.toReportMetadata(
        'admin-system-health',
        value.dateFrom,
        value.dateTo,
        'Admin'),
      overallHealthScore: value.overallHealthScore,
      overallHealthDataAvailable: value.overallHealthDataAvailable,
      healthStatus: value.healthStatus,
      averagePlatformWPM: value.averagePlatformWpm,
      averagePlatformComprehension: value.averagePlatformComprehension,
      userSatisfactionScore: value.userSatisfactionScore,
      userSatisfactionDataAvailable: value.userSatisfactionDataAvailable,
      totalExercisesCompleted: value.totalExercisesCompleted,
      totalQuestionsAnswered: value.totalQuestionsAnswered,
      successRate: value.successRate,
      errorRate: value.errorRate,
      errorRateDataAvailable: value.errorRateDataAvailable,
      healthTrend: value.healthTrend ?? [],
      performanceTrend: value.performanceTrend ?? [],
      systemAlerts: (value.systemAlerts ?? []).map(alert => ({
        severity: alert.severity,
        alertType: alert.alertType,
        message: alert.message,
        detectedAt: new Date(alert.detectedAt)
      })),
      systemAlertsDataAvailable: value.systemAlertsDataAvailable
    };
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

interface AdminPlatformUsageAnalytics {
  dateFrom: string;
  dateTo: string;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  newUserDataAvailable?: boolean;
  totalActivities: number;
  totalReadingSessions: number;
  averageSessionDuration: number;
  userGrowthRate: number;
  userGrowthRateDataAvailable?: boolean;
  engagementRate: number;
  retentionRate: number;
  userGrowth: ChartData[];
  dailyActiveUsers: ChartData[];
  activityVolume: ChartData[];
  hourlyActivity: ChartData[];
  popularContent: PopularContent[];
  topInstitutions: InstitutionActivity[];
  featureUsageStats: Record<string, number>;
}

interface AdminInstitutionAnalytics {
  dateFrom: string;
  dateTo: string;
  totalInstitutions: number;
  activeInstitutions: number;
  totalUsers: number;
  totalStudents: number;
  totalTeachers: number;
  institutionComparison: AdminInstitutionComparisonAnalytics[];
  institutionComparisonChart: ChartData;
  usersByInstitution: ChartData[];
  activityByInstitution: ChartData[];
  performanceByInstitution: ChartData[];
  topInstitutions: AdminTopInstitutionAnalytics[];
}

interface AdminInstitutionComparisonAnalytics {
  institutionId: string;
  institutionName: string;
  totalUsers: number;
  activeUsers: number;
  totalStudents: number;
  totalTeachers: number;
  totalActivities: number;
  averageWpm: number;
  averageWpmDataAvailable?: boolean;
  averageComprehension: number;
  averageComprehensionDataAvailable?: boolean;
  averagePerformance: number;
  engagementRate: number;
}

interface AdminTopInstitutionAnalytics {
  institutionName: string;
  averageWpm: number;
  averageWpmDataAvailable?: boolean;
  averageComprehension: number;
  averageComprehensionDataAvailable?: boolean;
  activeStudents: number;
  activeStudentsDataAvailable?: boolean;
  totalActivities: number;
}

interface AdminContentAnalysisAnalytics {
  dateFrom: string;
  dateTo: string;
  totalExercises: number;
  totalReadingTexts: number;
  totalTrainingSeries: number;
  totalProgramTemplates: number;
  totalAssignments: number;
  assignmentDataAvailable?: boolean;
  mostUsedContent: ContentUsageData[];
  leastUsedContent: ContentUsageData[];
  performanceByContentType: ChartData[];
  engagementByContentType: ChartData[];
  contentGaps: string[];
  popularTopics: string[];
  readingAnalysis: AdminReadingLevelAnalysis[];
  exerciseAnalysis: ExerciseTypeAnalysis[];
  readingPerformanceChart: ChartData[];
  exerciseFrequencyChart: ChartData[];
}

interface AdminReadingLevelAnalysis {
  difficultyLevel: number;
  totalReads: number;
  averageWpm: number;
  averageComprehension: number;
}

interface AdminSystemHealthAnalytics {
  dateFrom: string;
  dateTo: string;
  overallHealthScore: number;
  overallHealthDataAvailable?: boolean;
  healthStatus: string;
  averagePlatformWpm: number;
  averagePlatformComprehension: number;
  userSatisfactionScore: number;
  userSatisfactionDataAvailable?: boolean;
  totalExercisesCompleted: number;
  totalQuestionsAnswered: number;
  successRate: number;
  errorRate: number;
  errorRateDataAvailable?: boolean;
  healthTrend: ChartData[];
  performanceTrend: ChartData[];
  systemAlerts: AdminSystemAlert[];
  systemAlertsDataAvailable?: boolean;
}

interface TeacherClassOverviewAnalytics {
  dateFrom: string;
  dateTo: string;
  totalStudents: number;
  activeStudents: number;
  activeStudentsDataAvailable?: boolean;
  classAverageWpmDataAvailable?: boolean;
  classAverageComprehensionDataAvailable?: boolean;
  classAverageWpm: number;
  classAverageComprehension: number;
  totalActivitiesCompleted: number;
  studentsAboveAverage: number;
  studentsAtAverage: number;
  studentsBelowAverage: number;
  topPerformers: TeacherStudentPerformanceAnalytics[];
  studentsNeedingSupport: TeacherStudentPerformanceAnalytics[];
}

interface TeacherStudentPerformanceAnalytics {
  studentIdentifier: string;
  averageWpm: number;
  averageComprehension: number;
  activitiesCompleted: number;
  totalMinutes: number;
  performanceLevel: string;
}

interface TeacherAssignmentAnalytics {
  dateFrom: string;
  dateTo: string;
  dataAvailable?: boolean;
  unavailableReason?: string | null;
  assignmentInfo: TeacherAssignmentInfoAnalytics | null;
  completionStats: TeacherAssignmentCompletionAnalytics | null;
  performanceStats: TeacherAssignmentPerformanceAnalytics | null;
  scoreDistribution: ChartData[];
  studentBreakdown: TeacherAssignmentStudentAnalytics[];
  timeStats: TeacherAssignmentTimeAnalytics | null;
}

interface TeacherAssignmentInfoAnalytics {
  assignmentId: string;
  title: string;
  description: string;
  dueDate: string;
  assignedDate: string;
}

interface TeacherAssignmentCompletionAnalytics {
  totalStudents: number;
  completed: number;
  inProgress: number;
  notStarted: number;
  completionRate: number;
}

interface TeacherAssignmentPerformanceAnalytics {
  averageScore: number;
  medianScore: number;
  highestScore: number;
  lowestScore: number;
  standardDeviation: number;
}

interface TeacherAssignmentStudentAnalytics {
  studentId: string;
  studentName: string;
  status: string;
  score?: number | null;
  completionTime?: number | null;
  submittedAt?: string | null;
}

interface TeacherAssignmentTimeAnalytics {
  averageCompletionTime: number;
  medianCompletionTime: number;
  fastestCompletion: number;
  slowestCompletion: number;
}

interface TeacherContentAnalysisAnalytics {
  dateFrom: string;
  dateTo: string;
  exerciseAnalysis: ExerciseTypeAnalysis[];
  exerciseFrequencyChart: ChartData[];
  readingAnalysis: AdminReadingLevelAnalysis[];
  readingPerformanceChart: ChartData[];
}

interface TeacherTimeProgressAnalytics {
  dateFrom: string;
  dateTo: string;
  weeklyProgressChart: ChartData[];
  monthlyProgressChart: ChartData[];
  activityIntensityChart: ChartData[];
  improvingStudents: TeacherProgressStudentAnalytics[];
  decliningStudents: TeacherProgressStudentAnalytics[];
}

interface TeacherProgressStudentAnalytics {
  studentId: string;
  studentName: string;
  previousScore: number;
  currentScore: number;
  improvement: number;
  trend: string;
}

interface AdminSystemAlert {
  severity: string;
  alertType: string;
  message: string;
  detectedAt: string;
}

interface StudentAnalyticsMilestone {
  id: string;
  title: string;
  description: string;
  earnedAt: string;
  type: string;
  icon: string;
}

interface StudentAnalyticsTrendPoint {
  date: string;
  value: number;
}

interface StudentAnalyticsBenchmark {
  studentValue: number;
  institutionAverage: number;
  platformAverage: number;
  performanceLevel: string;
}

interface StudentAnalyticsCategoryPoint {
  categoryName: string;
  value: number;
  questionsAttempted: number;
  correctAnswers: number;
  performanceLevel: string;
}

interface StudentAnalyticsQuestionTypePoint {
  type: string;
  value: number;
  questionsAttempted: number;
  correctAnswers: number;
}

interface StudentReadingSpeedAnalytics {
  dateFrom: string;
  dateTo: string;
  currentWpm: number;
  averageWpm: number;
  medianWpm: number;
  minWpm: number;
  maxWpm: number;
  standardDeviation: number;
  improvementRate: number;
  trend: StudentAnalyticsTrendPoint[];
  categories: StudentAnalyticsCategoryPoint[];
  benchmark: StudentAnalyticsBenchmark;
  sessionsBelow200Wpm: number;
  sessions200To400Wpm: number;
  sessionsAbove400Wpm: number;
  recommendations: string[];
}

interface StudentComprehensionAnalytics {
  dateFrom: string;
  dateTo: string;
  currentComprehension: number;
  averageComprehension: number;
  maxComprehension: number;
  minComprehension: number;
  improvementRate: number;
  trend: StudentAnalyticsTrendPoint[];
  categories: StudentAnalyticsCategoryPoint[];
  questionTypes: StudentAnalyticsQuestionTypePoint[];
  totalQuestionsAttempted: number;
  correctAnswers: number;
  successRate: number;
  benchmark: StudentAnalyticsBenchmark;
  weakAreas: string[];
  strongAreas: string[];
}

interface StudentSeriesAnalytics {
  dateFrom: string;
  dateTo: string;
  dataAvailable: boolean;
  unavailableReason?: string | null;
  totalSeriesStarted: number;
  seriesCompleted: number;
  seriesInProgress: number;
  totalMilestones: number;
  activeSeries: StudentSeriesItem[];
  completionTimeline: StudentAnalyticsTrendPoint[];
  milestones: StudentSeriesMilestone[];
  performanceStats: StudentSeriesPerformanceStats;
}

interface StudentSeriesItem {
  seriesId: string;
  seriesName: string;
  progress: number;
  daysCompleted: number;
  totalDays: number;
  startedAt: string;
  lastActivityAt?: string | null;
  averageScore: number;
}

interface StudentSeriesMilestone {
  id: string;
  title: string;
  description: string;
  earnedAt: string;
  seriesId?: string | null;
  seriesName: string;
  type: string;
  icon: string;
}

interface StudentSeriesPerformanceStats {
  averageCompletionTime: number;
  averageScore: number;
  consistencyScore: number;
  engagementLevel: string;
}

interface StudentActivityAnalytics {
  dateFrom: string;
  dateTo: string;
  dataAvailable: boolean;
  unavailableReason?: string | null;
  currentStreak: StudentActivityStreak;
  heatmap: StudentActivityHeatmapPoint[];
  hourlyDistribution: StudentActivityDistributionPoint[];
  dailyDistribution: StudentActivityDistributionPoint[];
  studyTime: StudentActivityStudyTime;
}

interface StudentActivityStreak {
  days: number;
  longestStreak: number;
  lastActivityDate?: string | null;
  isActive: boolean;
}

interface StudentActivityHeatmapPoint {
  date: string;
  value: number;
  level: number;
}

interface StudentActivityDistributionPoint {
  label: string;
  value: number;
}

interface StudentActivityStudyTime {
  totalMinutes: number;
  averageSessionLength: number;
  totalSessions: number;
  mostActiveHour: number;
  mostActiveDay: string;
  consistency: number;
}

interface StudentAnalyticsDailyPoint {
  date: string;
  readingSessions: number;
  exerciseCount: number;
  averageWpm: number;
  averageComprehension: number;
}
