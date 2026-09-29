import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingInstitutionService } from './coaching-institution.service';

describe('CoachingInstitutionService', () => {
  let http: HttpTestingController;
  let service: CoachingInstitutionService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingInstitutionService);
  });

  afterEach(() => http.verify());

  it('loads the institution scope from the authenticated server session', () => {
    service.getReadScope().subscribe();
    const request = http.expectOne('/api/coaching-admin/scope');
    expect(request.request.method).toBe('GET');
    request.flush({ isGlobal: false, institutionId: 'institution-1' });
  });

  it('loads a scoped operations overview', () => {
    service.getOverview(10).subscribe();
    const request = http.expectOne('/api/coaching-admin/overview?recentLimit=10');
    expect(request.request.method).toBe('GET');
    request.flush({ totalAssignments: 4, totalExams: 2, recentAssignments: [] });
  });

  it('loads filtered, paged student roster without treating the client tenant id as authority', () => {
    service.getStudentRoster('institution-1', 2, 'Ada', 'teacher-1', 8).subscribe();
    const request = http.expectOne('/api/coaching-admin/institutions/institution-1/students?pageNumber=2&pageSize=25&search=Ada&teacherUserId=teacher-1&gradeLevel=8');
    expect(request.request.method).toBe('GET');
    request.flush({ students: [], totalCount: 0 });
  });

  it('loads a paged teacher roster with server-side search', () => {
    service.getTeacherRoster('institution-1', 3, 'Ayşe').subscribe();
    const request = http.expectOne('/api/coaching-admin/institutions/institution-1/teachers?pageNumber=3&pageSize=25&search=Ay%C5%9Fe');
    expect(request.request.method).toBe('GET');
    request.flush({ teachers: [], totalCount: 0 });
  });

  it('loads a teacher overview scoped by the server to the authenticated institution', () => {
    service.getTeacherOverview('teacher-1').subscribe();
    const request = http.expectOne('/api/coaching-admin/teachers/teacher-1/overview');
    expect(request.request.method).toBe('GET');
    request.flush({
      teacherId: 'teacher-1', totalAssignments: 4, totalAssignmentStudents: 18,
      submittedAssignmentStudents: 12, totalExams: 2, totalSessions: 5
    });
  });

  it('loads teacher analytics for the current and previous periods', () => {
    service.getTeacherAnalytics('teacher-1').subscribe();
    const request = http.expectOne('/api/coaching-admin/teachers/teacher-1/analytics');
    expect(request.request.method).toBe('GET');
    request.flush({
      teacherId: 'teacher-1', currentPeriod: { assignments: 2, exams: 1, sessions: 3 },
      previousPeriod: { assignments: 1, exams: 0, sessions: 2 }, lowResults: 1, mediumResults: 2, highResults: 4
    });
  });

  it('loads student details through the institution-scoped coaching admin API', () => {
    service.getStudentDetail('student-1').subscribe();
    const request = http.expectOne('/api/coaching-admin/students/student-1/detail');
    expect(request.request.method).toBe('GET');
    request.flush({
      studentId: 'student-1', totalAssignments: 4, submittedAssignments: 3,
      totalExams: 2, totalSessions: 5, totalGoals: 1, assignments: [], exams: []
    });
  });

  it('requests filtered student history with bounded pagination and encoded filters', () => {
    service.getStudentHistory('student-1', 'Goals', {
      pageNumber: 2, pageSize: 25, fromDate: '2026-01-01T00:00:00.000Z',
      toDate: '2026-01-31T23:59:59.999Z', status: 'Completed', search: '  çalışma  '
    }).subscribe();
    const request = http.expectOne('/api/coaching-admin/students/student-1/history?type=Goals&pageNumber=2&pageSize=25&fromDate=2026-01-01T00:00:00.000Z&toDate=2026-01-31T23:59:59.999Z&status=Completed&search=%C3%A7al%C4%B1%C5%9Fma');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], totalCount: 0 });
  });

  it('omits empty student history filters and bounds page values', () => {
    service.getStudentHistory('student-1', 'Assignments', { pageNumber: 5000, pageSize: 500, search: '  ' }).subscribe();
    const request = http.expectOne('/api/coaching-admin/students/student-1/history?type=Assignments&pageNumber=1000&pageSize=100');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], totalCount: 0 });
  });
});
