import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { TeachersService } from './teachers.service';
import { AuthService } from './auth.service';
import { UsersService } from './users.service';

describe('TeachersService', () => {
  let service: TeachersService;
  let http: HttpTestingController;
  let auth: { currentUserValue: { institutionId?: string } | null };
  let users: jasmine.SpyObj<UsersService>;

  beforeEach(() => {
    auth = { currentUserValue: { institutionId: 'institution-1' } };
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getMyProfile']);
    users.getMyProfile.and.returnValue(of({ institutionId: 'profile-institution-1' } as any));
    TestBed.configureTestingModule({
      providers: [
        TeachersService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: UsersService, useValue: users }
      ]
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

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/teachers/me/students');
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

  it('loads one assigned student by id without paging through the full roster', () => {
    service.getMyStudentById('student-1').subscribe(student => {
      expect(student?.id).toBe('student-1');
      expect(student?.gradeLevel).toBe(8);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/teachers/me/students');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('1');
    expect(request.request.params.get('studentUserId')).toBe('student-1');
    request.flush({ items: [{ userId: 'student-1', gradeLevel: 8 }], totalCount: 1, pageNumber: 1, pageSize: 1 });
  });

  it('loads the signed-in institution teacher directory from product-local memberships', () => {
    service.getTeachers('Ayşe', undefined, true).subscribe(teachers => {
      expect(teachers).toEqual([jasmine.objectContaining({
        id: 'teacher-1',
        firstName: 'Ayşe',
        lastName: 'Öğretmen',
        email: 'ayse@example.test',
        studentCount: 3
      })]);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('role')).toBe('Teacher');
    expect(request.request.params.get('searchTerm')).toBe('Ayşe');
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

  it('loads a server-paged institution teacher list with search and status filters', () => {
    service.getTeachersPage(3, 25, 'Ayşe', undefined, false).subscribe(page => {
      expect(page.items).toEqual([jasmine.objectContaining({ id: 'teacher-2', isActive: false })]);
      expect(page.totalCount).toBe(51);
      expect(page.pageNumber).toBe(3);
      expect(page.pageSize).toBe(25);
      expect(page.totalPages).toBe(3);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('pageNumber')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('role')).toBe('Teacher');
    expect(request.request.params.get('searchTerm')).toBe('Ayşe');
    expect(request.request.params.get('isActive')).toBe('false');
    request.flush({
      items: [{ userId: 'teacher-2', isActive: false }],
      totalCount: 51,
      pageNumber: 3,
      pageSize: 25
    });
  });

  it('looks up one teacher by product-local membership id without walking pages', () => {
    service.getTeacherById('teacher-101').subscribe(teacher => {
      expect(teacher?.id).toBe('teacher-101');
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('1');
    expect(request.request.params.get('memberUserId')).toBe('teacher-101');
    expect(request.request.params.get('role')).toBe('Teacher');
    request.flush({ items: [{ userId: 'teacher-101', firstName: 'Ayşe', lastName: 'Yılmaz' }], totalCount: 1 });
  });

  it('resolves the institution from the authenticated profile when the token has no institution claim', () => {
    auth.currentUserValue = null;
    service.getTeachers().subscribe();

    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/institutions/profile-institution-1/members');
    expect(users.getMyProfile).toHaveBeenCalled();
    request.flush({ items: [] });
  });

  it('sends student invitations through the supported teacher endpoint', () => {
    service.linkStudent('ada@example.test').subscribe();

    const request = http.expectOne('/api/speed-reading/invitations/teachers/me');
    expect(request.request.body).toEqual({ email: 'ada@example.test' });
    request.flush({ invitationId: 'invitation-1' });
  });

  it('removes only the selected product-local teacher-student relationship', () => {
    service.unlinkStudent('student-1', 'institution-1').subscribe();

    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/teachers/me/students/student-1'
      && candidate.params.get('institutionId') === 'institution-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('removes a teacher from the Speed Reading institution without deleting the identity', () => {
    service.deleteTeacher('teacher-1').subscribe();

    const request = http.expectOne('/api/speed-reading/institutions/institution-1/members/teacher-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ role: 2, isActive: false });
    request.flush(null);
  });

  it('sends institution teacher invitations through the product-local invitation endpoint', () => {
    service.inviteTeacher('ayse@example.test').subscribe();

    const request = http.expectOne('/api/speed-reading/invitations/institutions/institution-1');
    expect(request.request.body).toEqual({ email: 'ayse@example.test', role: 2 });
    request.flush({ invitationId: 'invitation-1' });
  });

  it('loads a filtered teacher roster page with server pagination metadata', () => {
    service.getMyStudentsPage(2, 25, 'Ada', 8, false).subscribe(page => {
      expect(page.items).toEqual([jasmine.objectContaining({
        id: 'student-2',
        email: 'ada@example.test',
        gradeLevel: 8,
        currentLevel: 4,
        isActive: false
      })]);
      expect(page.totalCount).toBe(41);
      expect(page.pageNumber).toBe(2);
      expect(page.pageSize).toBe(25);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/teachers/me/students');
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
        currentLevel: 4,
        isActive: false
      }],
      totalCount: 41,
      pageNumber: 2,
      pageSize: 25
    });
  });
});
