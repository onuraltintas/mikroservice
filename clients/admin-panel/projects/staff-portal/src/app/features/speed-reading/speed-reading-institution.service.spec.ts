import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { SpeedReadingInstitutionService } from './speed-reading-institution.service';

describe('SpeedReadingInstitutionService', () => {
  let http: HttpTestingController;
  let service: SpeedReadingInstitutionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingInstitutionService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SpeedReadingInstitutionService);
  });

  afterEach(() => http.verify());

  it('resolves the current institution through the Identity route without accepting a client institution id', async () => {
    const response = firstValueFrom(service.getMyInstitution());
    const request = http.expectOne('/api/institution/speed-reading/me');
    expect(request.request.method).toBe('GET');
    request.flush({ institutionId: 'institution-1', institutionName: 'Örnek Kurum' });

    await expect(response).resolves.toEqual({
      institutionId: 'institution-1',
      institutionName: 'Örnek Kurum',
    });
  });

  it('requests a role-scoped institution roster with normalized filters', async () => {
    const response = firstValueFrom(
      service.getMembers('institution-1', 'Student', 2, 10, {
        searchTerm: '  Elif  ',
        gradeLevel: 8,
        isActive: false,
      }),
    );
    const request = http.expectOne(
      (candidate) =>
        candidate.url === '/api/speed-reading/institutions/institution-1/members' &&
        candidate.params.get('pageNumber') === '2' &&
        candidate.params.get('pageSize') === '10' &&
        candidate.params.get('role') === 'Student' &&
        candidate.params.get('searchTerm') === 'Elif' &&
        candidate.params.get('gradeLevel') === '8' &&
        candidate.params.get('isActive') === 'false',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 10 });

    await expect(response).resolves.toMatchObject({ totalCount: 0, pageNumber: 2 });
  });

  it('loads institution analytics from the separate Speed Reading API', async () => {
    const from = new Date('2026-09-01T00:00:00.000Z');
    const to = new Date('2026-09-30T00:00:00.000Z');
    const response = firstValueFrom(service.getClassOverview('institution-1', from, to));
    const request = http.expectOne(
      (candidate) =>
        candidate.url ===
          '/api/speed-reading/analytics/institutions/institution-1/class-overview' &&
        candidate.params.get('dateFrom') === from.toISOString() &&
        candidate.params.get('dateTo') === to.toISOString(),
    );
    expect(request.request.method).toBe('GET');
    request.flush({
      dateFrom: from.toISOString(),
      dateTo: to.toISOString(),
      totalStudents: 12,
      activeStudents: 9,
      activeStudentsDataAvailable: true,
      classAverageWpmDataAvailable: true,
      classAverageComprehensionDataAvailable: true,
      classAverageWpm: 240,
      classAverageComprehension: 81,
      totalActivitiesCompleted: 64,
      studentsAboveAverage: 3,
      studentsAtAverage: 5,
      studentsBelowAverage: 4,
      topPerformers: [],
      studentsNeedingSupport: [],
    });

    await expect(response).resolves.toMatchObject({ totalStudents: 12, classAverageWpm: 240 });
  });

  it('updates a student profile only through the scoped institution endpoint', async () => {
    const response = firstValueFrom(
      service.updateStudentProfile('institution-1', 'student-1', 9, 'teacher-1'),
    );
    const request = http.expectOne(
      '/api/speed-reading/institutions/institution-1/members/student-1/student-profile',
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ gradeLevel: 9, teacherUserId: 'teacher-1' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await expect(response).resolves.toBeNull();
  });

  it('changes only the selected product membership role through the scoped endpoint', async () => {
    const response = firstValueFrom(
      service.setMemberStatus('institution-1', 'teacher-1', 'Teacher', false),
    );
    const request = http.expectOne(
      '/api/speed-reading/institutions/institution-1/members/teacher-1',
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ role: 'Teacher', isActive: false });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await expect(response).resolves.toBeNull();
  });

  it('loads all student detail analytics through institution-scoped routes', async () => {
    const from = new Date('2026-09-01T00:00:00.000Z');
    const to = new Date('2026-09-30T00:00:00.000Z');
    const response = firstValueFrom(service.getStudentReport('institution-1', 'student-1', from, to));
    const paths = [
      'summary',
      'reading-speed',
      'comprehension',
      'activity'
    ];
    const requests = paths.map(path => http.expectOne(request =>
      request.url === `/api/speed-reading/analytics/institutions/institution-1/students/student-1/${path}`
        && request.params.get('dateFrom') === from.toISOString()
        && request.params.get('dateTo') === to.toISOString()));
    expect(requests.every(request => request.request.method === 'GET')).toBe(true);
    requests[0].flush({ readingSessions: 6, averageWpm: 238 });
    requests[1].flush({ averageWpm: 238, trend: [], recommendations: [], benchmark: {} });
    requests[2].flush({ averageComprehension: 82, weakAreas: [], strongAreas: [], benchmark: {} });
    requests[3].flush({ dataAvailable: true, recentActivities: [], currentStreak: {}, studyTime: {} });

    await expect(response).resolves.toMatchObject({
      summary: { readingSessions: 6 },
      readingSpeed: { averageWpm: 238 },
      comprehension: { averageComprehension: 82 },
      activity: { dataAvailable: true }
    });
  });
});
