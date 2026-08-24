import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingApiService } from './speed-reading-api.service';

describe('SpeedReadingApiService', () => {
  let service: SpeedReadingApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SpeedReadingApiService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SpeedReadingApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the current user reading statistics from the dedicated service', () => {
    const response = {
      totalSessions: 4,
      averageWpm: 275.5,
      averageComprehension: 82.25,
      totalMinutes: 38,
      bestWpm: 320
    };

    service.getReadingStatistics().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/progress/reading-statistics');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('passes paging to the personalized learning path endpoint', () => {
    service.getPersonalizedLearningPath(2, 10).subscribe();

    const request = http.expectOne('/api/speed-reading/learning-paths/personalized?pageNumber=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });
  });

  it('loads the scoped student analytics summary with an optional date range', () => {
    service.getStudentAnalyticsSummary({
      dateFrom: '2026-08-01T00:00:00Z',
      dateTo: '2026-08-24T23:59:59Z'
    }).subscribe();

    const request = http.expectOne(
      '/api/speed-reading/analytics/student/summary?dateFrom=2026-08-01T00:00:00Z&dateTo=2026-08-24T23:59:59Z');
    expect(request.request.method).toBe('GET');
    request.flush({
      userId: 'student-1',
      dateFrom: '2026-08-01T00:00:00Z',
      dateTo: '2026-08-24T23:59:59Z',
      readingSessions: 0,
      averageWpm: 0,
      averageComprehension: 0,
      totalReadingMinutes: 0,
      bestWpm: 0,
      exercisesCompleted: 0,
      exercisesPassed: 0,
      averageSuccessRate: 0,
      daily: []
    });
  });
});
