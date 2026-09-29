import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';

describe('CoachingTeacherStudentsService', () => {
  let http: HttpTestingController;
  let service: CoachingTeacherStudentsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CoachingTeacherStudentsService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingTeacherStudentsService);
  });

  afterEach(() => http.verify());

  it('requests a teacher-scoped page and trims the optional search term', async () => {
    const response = firstValueFrom(service.getMyStudents(2, 10, '  Ayşe  '));
    const request = http.expectOne('/api/teachers/me/students?pageNumber=2&pageSize=10&searchTerm=Ay%C5%9Fe');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 1 });

    await expect(response).resolves.toMatchObject({ pageNumber: 2, totalCount: 0 });
  });

  it('omits a blank search term and clamps invalid pagination', async () => {
    const response = firstValueFrom(service.getMyStudents(0, 1000, '   '));
    const request = http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=100');
    request.flush({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 1 });

    await response;
  });

  it('loads the authorized student progress summary', async () => {
    const response = firstValueFrom(service.getStudentProgress('student-1'));
    const request = http.expectOne('/api/reports/student/student-1/progress');
    expect(request.request.method).toBe('GET');
    request.flush({ studentId: 'student-1', totalAssignments: 4 });

    await expect(response).resolves.toMatchObject({ studentId: 'student-1', totalAssignments: 4 });
  });

  it('loads a paged history category with normalized filters', async () => {
    const response = firstValueFrom(service.getStudentHistory('student-1', 'Goals', 2, 10, {
      fromDate: '2026-09-01T00:00:00.000Z',
      status: 'Completed',
      search: '  hedef  '
    }));
    const request = http.expectOne(candidate =>
      candidate.url === '/api/reports/student/student-1/history'
        && candidate.params.get('type') === 'Goals'
        && candidate.params.get('pageNumber') === '2'
        && candidate.params.get('pageSize') === '10'
        && candidate.params.get('fromDate') === '2026-09-01T00:00:00.000Z'
        && candidate.params.get('status') === 'Completed'
        && candidate.params.get('search') === 'hedef');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 });

    await expect(response).resolves.toMatchObject({ pageNumber: 2, pageSize: 10 });
  });
});
