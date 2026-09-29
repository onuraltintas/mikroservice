import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { SpeedReadingTeacherService } from './speed-reading-teacher.service';

describe('SpeedReadingTeacherService', () => {
  let http: HttpTestingController;
  let service: SpeedReadingTeacherService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingTeacherService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SpeedReadingTeacherService);
  });

  afterEach(() => http.verify());

  it('loads a paged teacher-scoped roster with normalized filters', async () => {
    const response = firstValueFrom(
      service.getMyStudents(2, 10, {
        searchTerm: '  Elif  ',
        gradeLevel: 7,
        isActive: false,
      }),
    );
    const request = http.expectOne(
      (candidate) =>
        candidate.url === '/api/speed-reading/teachers/me/students' &&
        candidate.params.get('pageNumber') === '2' &&
        candidate.params.get('pageSize') === '10' &&
        candidate.params.get('searchTerm') === 'Elif' &&
        candidate.params.get('gradeLevel') === '7' &&
        candidate.params.get('isActive') === 'false',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 10 });

    await expect(response).resolves.toMatchObject({ totalCount: 0, pageNumber: 2 });
  });

  it('clamps pagination and omits empty filters', async () => {
    const response = firstValueFrom(service.getMyStudents(0, 1000, { searchTerm: '   ' }));
    const request = http.expectOne(
      '/api/speed-reading/teachers/me/students?pageNumber=1&pageSize=100',
    );
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 });

    await response;
  });

  it('loads the active teacher class overview from the separate Speed Reading analytics API', async () => {
    const dateFrom = new Date('2026-09-01T00:00:00.000Z');
    const dateTo = new Date('2026-09-30T00:00:00.000Z');
    const response = firstValueFrom(service.getClassOverview(dateFrom, dateTo));
    const request = http.expectOne(
      (candidate) =>
        candidate.url === '/api/speed-reading/analytics/teacher/class-overview' &&
        candidate.params.get('dateFrom') === dateFrom.toISOString() &&
        candidate.params.get('dateTo') === dateTo.toISOString(),
    );
    expect(request.request.method).toBe('GET');
    request.flush({
      dateFrom: dateFrom.toISOString(),
      dateTo: dateTo.toISOString(),
      totalStudents: 4,
      activeStudents: 3,
      activeStudentsDataAvailable: true,
      classAverageWpmDataAvailable: true,
      classAverageComprehensionDataAvailable: true,
      classAverageWpm: 245,
      classAverageComprehension: 82,
      totalActivitiesCompleted: 19,
      studentsAboveAverage: 1,
      studentsAtAverage: 2,
      studentsBelowAverage: 1,
      topPerformers: [],
      studentsNeedingSupport: [],
    });

    await expect(response).resolves.toMatchObject({ totalStudents: 4, classAverageWpm: 245 });
  });

  it('loads a student detail report through the authenticated teacher scope', async () => {
    const dateFrom = new Date('2026-09-01T00:00:00.000Z');
    const dateTo = new Date('2026-09-30T23:59:59.999Z');
    const response = firstValueFrom(service.getStudentReport('student-1', dateFrom, dateTo));
    const paths = ['summary', 'reading-speed', 'comprehension', 'activity'];
    const requests = paths.map((path) =>
      http.expectOne(
        (request) =>
          request.url === `/api/speed-reading/analytics/teacher/students/student-1/${path}` &&
          request.params.get('dateFrom') === dateFrom.toISOString() &&
          request.params.get('dateTo') === dateTo.toISOString(),
      ),
    );
    expect(requests.every((request) => request.request.method === 'GET')).toBe(true);
    requests[0].flush({ readingSessions: 4, averageWpm: 245 });
    requests[1].flush({ averageWpm: 245, recommendations: [], benchmark: {} });
    requests[2].flush({ averageComprehension: 80, weakAreas: [], strongAreas: [], benchmark: {} });
    requests[3].flush({
      dataAvailable: true,
      recentActivities: [],
      currentStreak: {},
      studyTime: {},
    });

    await expect(response).resolves.toMatchObject({
      summary: { readingSessions: 4 },
      readingSpeed: { averageWpm: 245 },
      comprehension: { averageComprehension: 80 },
      activity: { dataAvailable: true },
    });
  });
});
