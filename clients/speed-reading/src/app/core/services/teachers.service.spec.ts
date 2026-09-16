import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TeachersService } from './teachers.service';

describe('TeachersService', () => {
  let service: TeachersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TeachersService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TeachersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps the authenticated teacher student roster to the shared student model', () => {
    service.getMyStudents().subscribe(students => {
      expect(students).toHaveSize(1);
      expect(students[0]).toEqual(jasmine.objectContaining({
        id: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        dailyGoalMinutes: 30,
        isActive: true
      }));
    });

    const request = http.expectOne(candidate => candidate.url === '/api/teachers/me/students');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        institutionId: 'institution-1',
        institutionName: 'Örnek Kolej',
        dailyGoalMinutes: 30,
        isActive: true
      }]
    });
  });

  it('loads the signed-in institution teacher directory through the scoped users endpoint', () => {
    service.getTeachers('Ayşe', undefined, true).subscribe(teachers => {
      expect(teachers).toEqual([jasmine.objectContaining({
        id: 'teacher-1',
        firstName: 'Ayşe',
        lastName: 'Öğretmen',
        email: 'ayse@example.test',
        studentCount: 3
      })]);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/users');
    expect(request.request.params.get('role')).toBe('Teacher');
    expect(request.request.params.get('search')).toBe('Ayşe');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({
      items: [{
        userId: 'teacher-1',
        firstName: 'Ayşe',
        lastName: 'Öğretmen',
        email: 'ayse@example.test',
        studentCount: 3,
        isActive: true
      }]
    });
  });

  it('sends student invitations through the supported teacher endpoint', () => {
    service.linkStudent('ada@example.test').subscribe();

    const request = http.expectOne('/api/teachers/invite-student');
    expect(request.request.body).toEqual({ studentEmail: 'ada@example.test' });
    request.flush({ invitationId: 'invitation-1' });
  });

  it('loads a filtered teacher roster page with server pagination metadata', () => {
    service.getMyStudentsPage(2, 25, 'Ada', 8, false).subscribe(page => {
      expect(page.items).toEqual([jasmine.objectContaining({
        id: 'student-2',
        email: 'ada@example.test',
        currentLevel: 8,
        isActive: false
      })]);
      expect(page.totalCount).toBe(41);
      expect(page.pageNumber).toBe(2);
      expect(page.pageSize).toBe(25);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/teachers/me/students');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('searchTerm')).toBe('Ada');
    expect(request.request.params.get('gradeLevel')).toBe('8');
    expect(request.request.params.get('isActive')).toBe('false');
    request.flush({
      items: [{
        userId: 'student-2',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        gradeLevel: 8,
        isActive: false
      }],
      totalCount: 41,
      pageNumber: 2,
      pageSize: 25
    });
  });
});
