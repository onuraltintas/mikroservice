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
});
