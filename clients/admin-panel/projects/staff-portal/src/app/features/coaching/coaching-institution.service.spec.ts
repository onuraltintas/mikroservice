import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingInstitutionService } from './coaching-institution.service';

describe('CoachingInstitutionService', () => {
  it('loads and manages institution seats without accepting a client institution scope', () => {
    service.getSubscription().subscribe();
    const read = http.expectOne('/api/coaching/institution-subscription'); expect(read.request.method).toBe('GET'); read.flush(null);
    service.setSubscriptionSeat('subscription/1', 'student/1', false).subscribe();
    const assign = http.expectOne('/api/coaching/institution-subscription/subscription%2F1/students/student%2F1');
    expect(assign.request.method).toBe('PUT'); expect(assign.request.body).toEqual({ isSuspended: false }); assign.flush(null);
  });
  it('updates teacher assignment and grade through the current institution scope', () => {
    service.updateStudent('student/1', 8, 'teacher-1').subscribe();
    const update = http.expectOne('/api/coaching/institution/students/student%2F1');
    expect(update.request.method).toBe('PUT'); expect(update.request.body).toEqual({ gradeLevel: 8, teacherUserId: 'teacher-1' });
    update.flush(null);
    service.updateStudent('student/1', 8, null).subscribe();
    const unassign = http.expectOne('/api/coaching/institution/students/student%2F1');
    expect(unassign.request.body).toEqual({ gradeLevel: 8, teacherUserId: null }); unassign.flush(null);
  });

  it('removes current institution members through role-scoped endpoints', () => {
    service.removeStudent('student').subscribe();
    const student = http.expectOne('/api/coaching/institution/students/student'); expect(student.request.method).toBe('DELETE'); student.flush(null);
    service.removeTeacher('teacher').subscribe();
    const teacher = http.expectOne('/api/coaching/institution/teachers/teacher'); expect(teacher.request.method).toBe('DELETE'); teacher.flush(null);
  });

  it('asks how much handed-in work waits for the leaving teacher', () => {
    let count = -1; service.getTeacherRemovalPreview('teacher/1').subscribe(value => count = value.ungradedSubmissionCount);
    const preview = http.expectOne('/api/coaching/institution/teachers/teacher%2F1/removal-preview');
    expect(preview.request.method).toBe('GET'); preview.flush({ ungradedSubmissionCount: 3 }); expect(count).toBe(3);
  });

  it('hands a leaving teacher over to a replacement and ends a single student-teacher link', () => {
    service.removeTeacher('teacher-1', 'teacher/2').subscribe();
    const handover = http.expectOne(request => request.url === '/api/coaching/institution/teachers/teacher-1');
    expect(handover.request.method).toBe('DELETE'); expect(handover.request.params.get('replacementTeacherUserId')).toBe('teacher/2'); handover.flush(null);
    service.removeStudentTeacher('student/1', 'teacher/1').subscribe();
    const link = http.expectOne('/api/coaching/institution/students/student%2F1/teachers/teacher%2F1');
    expect(link.request.method).toBe('DELETE'); link.flush(null);
  });
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

  it('sends Coaching institution invitations through the authenticated institution endpoints', () => {
    service.inviteTeacher('teacher@example.test').subscribe();
    const teacherRequest = http.expectOne('/api/coaching/institution/invite-teacher');
    expect(teacherRequest.request.method).toBe('POST');
    expect(teacherRequest.request.body).toEqual({ teacherEmail: 'teacher@example.test' });
    teacherRequest.flush({ invitationId: 'teacher-invitation-1' });

    service.inviteStudent('student@example.test', 'teacher-1').subscribe();
    const studentRequest = http.expectOne('/api/coaching/institution/invite-student');
    expect(studentRequest.request.method).toBe('POST');
    expect(studentRequest.request.body).toEqual({
      studentEmail: 'student@example.test',
      teacherUserId: 'teacher-1'
    });
    studentRequest.flush({ invitationId: 'student-invitation-1' });
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

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
