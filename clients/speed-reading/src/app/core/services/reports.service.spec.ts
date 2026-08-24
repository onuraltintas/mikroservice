import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReportsService } from './reports.service';

describe('ReportsService', () => {
  let service: ReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ReportsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(ReportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the central student analytics and preserves dashboard goals and milestones', () => {
    const startDate = new Date('2020-01-01T00:00:00.000Z');
    const endDate = new Date('2026-08-24T12:00:00.000Z');
    let report: any;

    service.getStudentDashboardReport('ignored-student-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/student/summary');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('dateTo')).toBe(endDate.toISOString());
    expect(request.request.params.get('dateFrom')).toBe(
      new Date(endDate.getTime() - 366 * 24 * 60 * 60 * 1000).toISOString());

    request.flush({
      dateFrom: '2025-08-24T12:00:00.000Z',
      dateTo: '2026-08-24T12:00:00.000Z',
      readingSessions: 2,
      averageWpm: 250,
      averageComprehension: 80,
      totalReadingMinutes: 40,
      exercisesCompleted: 3,
      latestWpm: 275,
      latestComprehension: 85,
      currentLevel: 4,
      currentStreak: 6,
      longestStreak: 12,
      totalXp: 900,
      milestonesEarned: 2,
      dailyGoalMinutes: 20,
      goalCompletionRate: 6.67,
      recentMilestones: [{
        id: 'milestone-1',
        title: 'Hızlı Başlangıç',
        description: 'İlk başarım',
        earnedAt: '2026-08-23T10:00:00.000Z',
        type: 'speed',
        icon: '⚡'
      }],
      daily: []
    });

    expect(report.dailyGoalMinutes).toBe(20);
    expect(report.goalCompletionRate).toBe(6.67);
    expect(report.recentMilestones.length).toBe(1);
    expect(report.recentMilestones[0].earnedAt).toEqual(new Date('2026-08-23T10:00:00.000Z'));
    expect(report.recentMilestones[0].type).toBe('speed');
  });

  it('loads reading-speed analytics from the token-scoped endpoint', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getStudentReadingSpeedReport('another-student-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/student/reading-speed');
    expect(request.request.params.has('studentId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      currentWpm: 310,
      averageWpm: 290,
      medianWpm: 285,
      minWpm: 180,
      maxWpm: 420,
      standardDeviation: 54.2,
      improvementRate: 12.5,
      trend: [{ date: '2026-01-15', value: 290 }],
      categories: [{
        categoryName: 'Bilim',
        value: 300,
        questionsAttempted: 0,
        correctAnswers: 0,
        performanceLevel: ''
      }],
      benchmark: {
        studentValue: 290,
        institutionAverage: 250,
        platformAverage: 230,
        performanceLevel: 'Above Average'
      },
      sessionsBelow200Wpm: 1,
      sessions200To400Wpm: 4,
      sessionsAbove400Wpm: 1,
      recommendations: []
    });

    expect(report.currentWPM).toBe(310);
    expect(report.statistics.medianWPM).toBe(285);
    expect(report.statistics.standardDeviation).toBe(54.2);
    expect(report.categoryWPMChart.data[0].name).toBe('Bilim');
  });

  it('loads comprehension analytics and keeps unsupported question-type data explicit', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getStudentComprehensionReport('another-student-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/student/comprehension');
    expect(request.request.params.has('studentId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      currentComprehension: 82,
      averageComprehension: 78,
      maxComprehension: 95,
      minComprehension: 60,
      improvementRate: 4,
      trend: [],
      categories: [{
        categoryName: 'Bilim',
        value: 55,
        questionsAttempted: 10,
        correctAnswers: 5,
        performanceLevel: 'Needs Improvement'
      }],
      questionTypes: [],
      totalQuestionsAttempted: 10,
      correctAnswers: 5,
      successRate: 50,
      benchmark: {
        studentValue: 78,
        institutionAverage: 72,
        platformAverage: 70,
        performanceLevel: 'Average'
      },
      weakAreas: ['Bilim'],
      strongAreas: []
    });

    expect(report.overallComprehension).toBe(78);
    expect(report.categoryBreakdown[0].questionsAnswered).toBe(10);
    expect(report.questionTypeChart.data).toEqual([]);
    expect(report.improvementAreas[0].priority).toBe('high');
  });

  it('loads series analytics from the token-scoped endpoint and maps program progress', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getStudentSeriesReport('another-student-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/student/series');
    expect(request.request.params.has('studentId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      dataAvailable: true,
      unavailableReason: null,
      totalSeriesStarted: 1,
      seriesCompleted: 0,
      seriesInProgress: 1,
      totalMilestones: 0,
      activeSeries: [{
        seriesId: 'program-1',
        seriesName: 'Başlangıç Programı',
        progress: 50,
        daysCompleted: 5,
        totalDays: 10,
        startedAt: '2026-01-10T10:00:00.000Z',
        lastActivityAt: null,
        averageScore: 72
      }],
      completionTimeline: [{ date: '2026-01-15', value: 50 }],
      milestones: [],
      performanceStats: {
        averageCompletionTime: 0,
        averageScore: 72,
        consistencyScore: 50,
        engagementLevel: 'medium'
      }
    });

    expect(report.summary.totalSeriesStarted).toBe(1);
    expect(report.activeSeries[0].startedAt).toEqual(new Date('2026-01-10T10:00:00.000Z'));
    expect(report.activeSeries[0].lastActivityAt).toBeNull();
    expect(report.completionTimelineChart.data[0].name).toBe('2026-01-15');
    expect(report.completionTimelineChart.data[0].value).toBe(50);
  });

  it('loads activity analytics from the token-scoped endpoint and maps distributions', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getStudentActivityReport('another-student-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/student/activity');
    expect(request.request.params.has('studentId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      currentStreak: {
        days: 3,
        longestStreak: 5,
        lastActivityDate: '2026-01-15T18:00:00.000Z',
        isActive: true
      },
      heatmap: [{ date: '2026-01-15', value: 45, level: 3 }],
      hourlyDistribution: [{ label: '18:00', value: 2 }],
      dailyDistribution: [{ label: 'Çarşamba', value: 3 }],
      studyTime: {
        totalMinutes: 45,
        averageSessionLength: 15,
        totalSessions: 3,
        mostActiveHour: 18,
        mostActiveDay: 'Çarşamba',
        consistency: 60
      }
    });

    expect(report.currentStreak.days).toBe(3);
    expect(report.activityHeatmap.data[0].level).toBe(3);
    expect(report.hourlyDistributionChart.data[0].series[0].value).toBe(2);
    expect(report.dailyDistributionChart.data[0].name).toBe('Çarşamba');
    expect(report.studyTime.totalMinutes).toBe(45);
  });

  it('loads admin platform usage from the central analytics endpoint', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getAdminPlatformUsageReport(startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/admin/platform-usage');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('dateFrom')).toBe(startDate.toISOString());
    expect(request.request.params.get('dateTo')).toBe(endDate.toISOString());
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      totalUsers: 10,
      activeUsers: 4,
      newUsers: 0,
      newUserDataAvailable: false,
      totalActivities: 20,
      totalReadingSessions: 8,
      averageSessionDuration: 12.5,
      userGrowthRate: 0,
      userGrowthRateDataAvailable: false,
      engagementRate: 40,
      retentionRate: 25,
      userGrowth: [],
      dailyActiveUsers: [{ name: '2026-01-01', series: [{ name: 'Aktif kullanıcı', value: 4 }] }],
      activityVolume: [{ name: '2026-01-01', series: [{ name: 'Aktivite', value: 5 }] }],
      hourlyActivity: [{ name: '18:00', series: [{ name: 'Aktivite', value: 3 }] }],
      popularContent: [{ title: 'Bilim', type: 'ReadingText', usageCount: 7 }],
      topInstitutions: [],
      featureUsageStats: { reading: 8, exercise: 12 }
    });

    expect(report.totalUsers).toBe(10);
    expect(report.averageSessionDuration).toBe(12.5);
    expect(report.metadata.startDate).toEqual(startDate);
    expect(report.newUserDataAvailable).toBeFalse();
    expect(report.userGrowth).toEqual([]);
    expect(report.popularContent[0].title).toBe('Bilim');
  });

  it('loads admin content analysis from the central analytics endpoint', () => {
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    let report: any;

    service.getAdminContentAnalysisReport(startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/admin/content-analysis');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('dateFrom')).toBe(startDate.toISOString());
    expect(request.request.params.get('dateTo')).toBe(endDate.toISOString());
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      totalExercises: 5,
      totalReadingTexts: 2,
      totalTrainingSeries: 0,
      totalProgramTemplates: 1,
      totalAssignments: 0,
      assignmentDataAvailable: false,
      mostUsedContent: [{
        contentId: 'text-1',
        contentType: 'ReadingText',
        title: 'Bilim',
        usageCount: 4,
        averageScore: 82,
        averageComprehension: 82
      }],
      leastUsedContent: [],
      performanceByContentType: [],
      engagementByContentType: [],
      contentGaps: [],
      popularTopics: ['Bilim'],
      readingAnalysis: [{
        difficultyLevel: 2,
        totalReads: 4,
        averageWpm: 285,
        averageComprehension: 82
      }],
      exerciseAnalysis: [],
      readingPerformanceChart: [],
      exerciseFrequencyChart: []
    });

    expect(report.totalExercises).toBe(5);
    expect(report.metadata.reportType).toBe('Admin');
    expect(report.readingAnalysis[0].averageWPM).toBe(285);
    expect(report.mostUsedContent[0].averageComprehension).toBe(82);
    expect(report.assignmentDataAvailable).toBeFalse();
  });
});
