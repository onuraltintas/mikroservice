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
});
