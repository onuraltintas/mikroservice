import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PlatformMetricsService } from './platform-metrics.service';

describe('PlatformMetricsService', () => {
  let service: PlatformMetricsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PlatformMetricsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PlatformMetricsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the owned speed-reading analytics contract and maps its daily activity data', () => {
    let summary: any;
    service.getMetrics(new Date('2026-08-01T00:00:00.000Z'), new Date('2026-08-03T00:00:00.000Z'))
      .subscribe(value => summary = value);

    const request = http.expectOne(req => req.url === '/api/speed-reading/analytics/admin/platform-usage');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('startDate')).toBe('2026-08-01T00:00:00.000Z');
    expect(request.request.params.get('endDate')).toBe('2026-08-03T00:00:00.000Z');
    request.flush({
      dateFrom: '2026-08-01T00:00:00.000Z',
      dateTo: '2026-08-03T00:00:00.000Z',
      totalUsers: 10,
      activeUsers: 4,
      newUsers: 0,
      totalActivities: 8,
      totalReadingSessions: 3,
      averageSessionDuration: 12.5,
      userGrowthRate: 0,
      engagementRate: 40,
      retentionRate: 25,
      dailyActiveUsers: [{ name: '2026-08-02', series: [{ name: 'Aktif kullanıcı', value: 4 }] }],
      activityVolume: [{ name: '2026-08-02', series: [{ name: 'Aktivite', value: 8 }] }],
      featureUsageStats: { exercise: 5 }
    });

    expect(summary.totals.totalUsers).toBe(10);
    expect(summary.totals.totalExercises).toBe(5);
    expect(summary.totals.averageDailyActiveUsers).toBe(4);
    expect(summary.dailyMetrics[0].date).toEqual(new Date('2026-08-02'));
    expect(summary.dailyMetrics[0].totalActivities).toBe(8);
  });
});
