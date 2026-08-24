import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TeacherReportService } from './teacher-report.service';

describe('TeacherReportService', () => {
  let service: TeacherReportService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TeacherReportService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TeacherReportService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads a token-scoped student reading-speed report from the central service', () => {
    let report: any;
    service.getStudentReadingSpeedTrend('student-1', 30).subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/teacher/students/student-1/reading-speed');
    expect(request.request.params.get('dateFrom')).toBeTruthy();
    expect(request.request.params.get('dateTo')).toBeTruthy();
    request.flush({
      userId: 'student-1',
      dateFrom: '2026-01-01T00:00:00.000Z',
      dateTo: '2026-01-31T00:00:00.000Z',
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
      recommendations: ['Düzenli pratik yapın']
    });

    expect(report.averageWPM).toBe(290);
    expect(report.currentWPM).toBe(310);
    expect(report.wpmOverTime[0].series[0].value).toBe(290);
    expect(report.wpmByExerciseType[0].name).toBe('Bilim');
  });

  it('loads a token-scoped student comprehension report from the central service', () => {
    let report: any;
    service.getStudentComprehensionTrend('student-1', 30).subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/teacher/students/student-1/comprehension');
    request.flush({
      userId: 'student-1',
      dateFrom: '2026-01-01T00:00:00.000Z',
      dateTo: '2026-01-31T00:00:00.000Z',
      currentComprehension: 82,
      averageComprehension: 78,
      maxComprehension: 95,
      minComprehension: 60,
      improvementRate: 4,
      trend: [{ date: '2026-01-15', value: 78 }],
      categories: [],
      questionTypes: [],
      totalQuestionsAttempted: 10,
      correctAnswers: 8,
      successRate: 80,
      benchmark: {
        studentValue: 78,
        institutionAverage: 72,
        platformAverage: 70,
        performanceLevel: 'Average'
      },
      weakAreas: ['Paragraf'],
      strongAreas: ['Bilim']
    });

    expect(report.averageComprehension).toBe(78);
    expect(report.currentComprehension).toBe(82);
    expect(report.comprehensionOverTime[0].series[0].value).toBe(78);
    expect(report.weakAreas).toEqual(['Paragraf']);
  });

  it('loads a token-scoped student activity report from the central service', () => {
    let report: any;
    service.getStudentActivityReport('student-1').subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/teacher/students/student-1/activity');
    request.flush({
      userId: 'student-1',
      dateFrom: '2026-01-01T00:00:00.000Z',
      dateTo: '2026-01-31T00:00:00.000Z',
      dataAvailable: true,
      unavailableReason: null,
      currentStreak: {
        days: 4,
        longestStreak: 9,
        lastActivityDate: '2026-01-30T00:00:00.000Z',
        isActive: true
      },
      heatmap: [
        { date: '2026-01-29', value: 2, level: 1 },
        { date: '2026-01-30', value: 3, level: 2 }
      ],
      hourlyDistribution: [{ label: '10:00', value: 2 }],
      dailyDistribution: [{ label: 'Cuma', value: 3 }],
      studyTime: {
        totalMinutes: 120,
        averageSessionLength: 30,
        totalSessions: 4,
        mostActiveHour: 10,
        mostActiveDay: 'Cuma',
        consistency: 50
      }
    });

    expect(report.totalActivities).toBe(4);
    expect(report.totalReadingMinutes).toBe(120);
    expect(report.daysActive).toBe(2);
    expect(report.currentStreak).toBe(4);
    expect(report.longestStreak).toBe(9);
    expect(report.activityByType[0].series[0].value).toBe(2);
    expect(report.dailyActivity[1].series[0].value).toBe(3);
  });

  it('loads teacher content analysis from the central service', () => {
    let report: any;
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    service.getContentAnalysisReport('ignored-teacher-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/teacher/content-analysis');
    expect(request.request.params.has('teacherId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      exerciseAnalysis: [],
      exerciseFrequencyChart: [],
      readingAnalysis: [{ difficultyLevel: 2, totalReads: 3, averageWpm: 275, averageComprehension: 80 }],
      readingPerformanceChart: []
    });

    expect(report.readingAnalysis[0].averageWPM).toBe(275);
    expect(report.metadata.reportType).toBe('Teacher');
  });

  it('loads admin-targeted teacher content analysis through the scoped endpoint', () => {
    let report: any;
    const teacherId = 'teacher-1';
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    service.getAdminContentAnalysisReport(teacherId, startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === `/api/speed-reading/analytics/admin/teachers/${teacherId}/content-analysis`);
    expect(request.request.params.has('teacherId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      exerciseAnalysis: [],
      exerciseFrequencyChart: [],
      readingAnalysis: [],
      readingPerformanceChart: []
    });

    expect(report.exerciseAnalysis).toEqual([]);
    expect(report.metadata.reportType).toBe('Teacher');
  });

  it('loads teacher time progress from the central service', () => {
    let report: any;
    const startDate = new Date('2026-01-01T00:00:00.000Z');
    const endDate = new Date('2026-01-31T00:00:00.000Z');
    service.getTimeBasedProgressReport('ignored-teacher-id', startDate, endDate)
      .subscribe(value => report = value);

    const request = http.expectOne(
      candidate => candidate.url === '/api/speed-reading/analytics/teacher/time-progress');
    expect(request.request.params.has('teacherId')).toBeFalse();
    request.flush({
      dateFrom: startDate.toISOString(),
      dateTo: endDate.toISOString(),
      weeklyProgressChart: [],
      monthlyProgressChart: [],
      activityIntensityChart: [],
      improvingStudents: [],
      decliningStudents: []
    });

    expect(report.weeklyProgressChart).toEqual([]);
    expect(report.metadata.reportType).toBe('Teacher');
  });
});
