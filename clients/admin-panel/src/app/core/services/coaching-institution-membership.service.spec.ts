import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingInstitutionMembershipService } from './coaching-institution-membership.service';

describe('CoachingInstitutionMembershipService', () => {
  let http: HttpTestingController;
  let service: CoachingInstitutionMembershipService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CoachingInstitutionMembershipService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingInstitutionMembershipService);
  });

  afterEach(() => http.verify());

  it('targets the selected coaching institution for system admin operations', () => {
    service.inviteTeacher({ teacherEmail: 'teacher@example.com' }, 'school-1').subscribe();
    http.expectOne('/api/institutions/school-1/coaching/invite-teacher').flush({ invitationId: 'invite' });
    service.updateStudent('student-1', { gradeLevel: 8, teacherUserId: null }, 'school-1').subscribe();
    http.expectOne('/api/institutions/school-1/coaching/students/student-1').flush(null);
    service.removeTeacher('teacher-1', 'school-1').subscribe();
    http.expectOne('/api/institutions/school-1/coaching/teachers/teacher-1').flush(null);
  });

  it('invites coaching teachers through the authenticated institution endpoint', () => {
    service.inviteTeacher({ teacherEmail: 'teacher@example.com', message: 'Hoş geldiniz' }).subscribe();

    const request = http.expectOne('/api/institution/invite-teacher');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ teacherEmail: 'teacher@example.com', message: 'Hoş geldiniz' });
    request.flush({ invitationId: 'invitation-1' });
  });

  it('invites a coaching student with an optional institution teacher assignment', () => {
    service.inviteStudent({ studentEmail: 'student@example.com', teacherUserId: 'teacher-1' }).subscribe();

    const request = http.expectOne('/api/institution/invite-student');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ studentEmail: 'student@example.com', teacherUserId: 'teacher-1' });
    request.flush({ invitationId: 'invitation-2' });
  });

  it('updates only student membership data without accepting identity names or an institution id', () => {
    service.updateStudent('student/1', {
      gradeLevel: 8, teacherUserId: 'teacher-1'
    }).subscribe();

    const request = http.expectOne('/api/institution/students/student%2F1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ gradeLevel: 8, teacherUserId: 'teacher-1' });
    request.flush(null);
  });

  it('removes only the institution membership for the selected coaching teacher or student', () => {
    service.removeTeacher('teacher-1').subscribe();
    http.expectOne('/api/institution/teachers/teacher-1').flush(null);

    service.removeStudent('student-1').subscribe();
    http.expectOne('/api/institution/students/student-1').flush(null);
  });
});
