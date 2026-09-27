import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { StudentsService } from './students.service';
import { AuthService } from './auth.service';
import { UsersService } from './users.service';

describe('StudentsService', () => {
  let service: StudentsService;
  let http: HttpTestingController;
  let auth: { currentUserValue: { institutionId?: string } | null };
  let users: jasmine.SpyObj<UsersService>;

  beforeEach(() => {
    auth = { currentUserValue: { institutionId: 'institution-1' } };
    users = jasmine.createSpyObj<UsersService>('UsersService', ['getMyProfile']);
    users.getMyProfile.and.returnValue(of({ institutionId: 'profile-institution-1' } as any));
    TestBed.configureTestingModule({
      providers: [
        StudentsService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: UsersService, useValue: users }
      ]
    });
    service = TestBed.inject(StudentsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the product-local institution roster with each student\'s assigned teacher', () => {
    service.getInstitutionStudents('Ada', 8, true).subscribe(students => {
      expect(students).toEqual([jasmine.objectContaining({
        id: 'student-1',
        gradeLevel: 8,
        currentLevel: 3,
        teacherId: 'teacher-1',
        teacherName: 'Ayşe Öğretmen'
      })]);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('role')).toBe('Student');
    expect(request.request.params.get('searchTerm')).toBe('Ada');
    expect(request.request.params.get('gradeLevel')).toBe('8');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        gradeLevel: 8,
        currentLevel: 3,
        isActive: true,
        teacherUserId: 'teacher-1',
        teacherName: 'Ayşe Öğretmen'
      }]
    });
  });

  it('filters the institution roster by the selected teacher when requested', () => {
    service.getInstitutionStudents(undefined, undefined, undefined, 'teacher-1').subscribe();

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('teacherUserId')).toBe('teacher-1');
    expect(request.request.params.get('role')).toBe('Student');
    request.flush({ items: [] });
  });

  it('loads an institution roster page without dropping pagination metadata', () => {
    service.getInstitutionStudentsPage(3, 25, 'Ada', 8, true, 'teacher-1').subscribe(page => {
      expect(page.items).toEqual([jasmine.objectContaining({ id: 'student-3', gradeLevel: 8, currentLevel: 4 })]);
      expect(page.totalCount).toBe(61);
      expect(page.pageNumber).toBe(3);
      expect(page.pageSize).toBe(25);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('pageNumber')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('searchTerm')).toBe('Ada');
    expect(request.request.params.get('gradeLevel')).toBe('8');
    expect(request.request.params.get('isActive')).toBe('true');
    expect(request.request.params.get('teacherUserId')).toBe('teacher-1');
    request.flush({
      items: [{ userId: 'student-3', firstName: 'Ada', lastName: 'Yılmaz', gradeLevel: 8, currentLevel: 4, isActive: true }],
      totalCount: 61,
      pageNumber: 3,
      pageSize: 25
    });
  });

  it('loads one institution student by membership id without paging through the roster', () => {
    service.getInstitutionStudentById('student-1', 'teacher-1').subscribe(student => {
      expect(student?.id).toBe('student-1');
      expect(student?.gradeLevel).toBe(8);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('1');
    expect(request.request.params.get('role')).toBe('Student');
    expect(request.request.params.get('teacherUserId')).toBe('teacher-1');
    expect(request.request.params.get('memberUserId')).toBe('student-1');
    request.flush({ items: [{ userId: 'student-1', gradeLevel: 8, currentLevel: 3 }], totalCount: 1, pageNumber: 1, pageSize: 1 });
  });

  it('resolves the institution from the authenticated profile when the token has no institution claim', () => {
    auth.currentUserValue = null;
    service.getInstitutionStudentsPage().subscribe();

    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/institutions/profile-institution-1/members');
    expect(users.getMyProfile).toHaveBeenCalled();
    request.flush({ items: [] });
  });

  it('removes a student from only the Speed Reading institution membership', () => {
    service.unlinkStudentFromInstitution('student-1').subscribe();

    const request = http.expectOne('/api/speed-reading/institutions/institution-1/members/student-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ role: 1, isActive: false });
    request.flush(null);
  });

  it('sends a product-local institution invitation with the selected teacher', () => {
    service.linkStudent('ada@example.test', 'institution-1', 'teacher-1').subscribe();

    const request = http.expectOne('/api/speed-reading/invitations/institutions/institution-1');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'ada@example.test',
      role: 1,
      teacherUserId: 'teacher-1'
    });
    request.flush({ invitationId: 'invitation-1' });
  });

  it('updates school grade and teacher assignment through the Speed Reading service', () => {
    service.updateInstitutionStudent('student-1', 9, 'teacher-2').subscribe();

    const request = http.expectOne('/api/speed-reading/institutions/institution-1/members/student-1/student-profile');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ gradeLevel: 9, teacherUserId: 'teacher-2' });
    request.flush(null);
  });
});
