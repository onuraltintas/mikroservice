import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StudentsService } from './students.service';

describe('StudentsService', () => {
  let service: StudentsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StudentsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(StudentsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the signed-in institution roster through the scoped users endpoint', () => {
    service.getStudents('Ada', undefined, 8, true).subscribe(students => {
      expect(students).toEqual([jasmine.objectContaining({
        id: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        currentLevel: 8,
        isActive: true
      })]);
    });

    const request = http.expectOne(candidate => candidate.url === '/api/users');
    expect(request.request.params.get('role')).toBe('Student');
    expect(request.request.params.get('search')).toBe('Ada');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        email: 'ada@example.test',
        isActive: true,
        studentDetails: { gradeLevel: 8 }
      }]
    });
  });

  it('creates a student through the institution endpoint with the selected teacher', () => {
    service.createStudent({
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.test',
      gradeLevel: 8,
      teacherUserId: 'teacher-1'
    }).subscribe();

    const request = http.expectOne('/api/institution/students');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      firstName: 'Ada',
      lastName: 'Yılmaz',
      email: 'ada@example.test',
      gradeLevel: 8,
      teacherUserId: 'teacher-1'
    });
    request.flush({ studentId: 'student-1' });
  });

  it('sends an institution invitation with the selected teacher', () => {
    service.linkStudent('ada@example.test', 'teacher-1').subscribe();

    const request = http.expectOne('/api/institution/invite-student');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      studentEmail: 'ada@example.test',
      teacherUserId: 'teacher-1'
    });
    request.flush({ invitationId: 'invitation-1' });
  });
});
