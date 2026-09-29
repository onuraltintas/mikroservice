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
});
