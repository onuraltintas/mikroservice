import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingPortalService } from './coaching-portal.service';

describe('CoachingPortalService', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [CoachingPortalService, provideHttpClient(), provideHttpClientTesting()]
    });

    return {
      service: TestBed.inject(CoachingPortalService),
      http: TestBed.inject(HttpTestingController)
    };
  }

  it('requests bounded student assignment pages with encoded student ids', () => {
    const { service, http } = setup();

    service.getStudentAssignments('student/1', 5000, 999).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/assignments/student/student%2F1'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1000');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({ items: [], pageNumber: 1000, pageSize: 100, totalCount: 0, totalPages: 0 });
    http.verify();
  });

  it('submits the assignment with a trimmed note and encoded id', () => {
    const { service, http } = setup();

    service.submitAssignment('assignment/1', 'student/1', '  tamamladım  ').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/submit'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      studentNote: 'tamamladım'
    });
    request.flush({});
    http.verify();
  });

  it('loads teacher assignment summaries and grades a student through the protected resource path', () => {
    const { service, http } = setup();

    service.getTeacherAssignments('teacher/1', 2, 25).subscribe();
    const list = http.expectOne(candidate => candidate.url.endsWith('/assignments/teacher/teacher%2F1'));
    expect(list.request.method).toBe('GET');
    expect(list.request.params.get('pageNumber')).toBe('2');
    list.flush({ items: [], pageNumber: 2, pageSize: 25, totalCount: 0, totalPages: 0 });

    service.gradeAssignment('assignment/1', 'student/1', 87.5, 'Güzel çalışma').subscribe();
    const grade = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/grade'));
    expect(grade.request.method).toBe('POST');
    expect(grade.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      score: 87.5,
      teacherFeedback: 'Güzel çalışma'
    });
    grade.flush({});
    http.verify();
  });

  it('loads the authenticated teacher student roster with bounded search paging', () => {
    const { service, http } = setup();

    service.getTeacherStudents(5000, 999, '  Ada  ').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/teachers/me/students'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1000');
    expect(request.request.params.get('pageSize')).toBe('100');
    expect(request.request.params.get('searchTerm')).toBe('Ada');
    request.flush({ items: [], pageNumber: 1000, pageSize: 100, totalCount: 0, totalPages: 0 });
    http.verify();
  });

  it('creates a teacher assignment with a deduplicated student set and idempotency key', () => {
    const { service, http } = setup();

    service.createTeacherAssignment({
      teacherId: 'teacher-1',
      title: '  Kitap tekrar ödevi  ',
      assignmentType: 'Individual',
      assignmentSource: 'Book',
      dueDate: '2030-01-02T10:00:00.000Z',
      studentIds: ['student-1', 'student-1', 'student-2'],
      bookTitle: '  Matematik  ' 
    }, 'assignment-key-123456').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/assignments'));
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('assignment-key-123456');
    expect(request.request.body).toEqual({
      teacherId: 'teacher-1',
      title: 'Kitap tekrar ödevi',
      assignmentType: 'Individual',
      assignmentSource: 'Book',
      dueDate: '2030-01-02T10:00:00.000Z',
      studentIds: ['student-1', 'student-2'],
      bookTitle: 'Matematik',
      description: null,
      subject: null
    });
    request.flush({ assignmentId: 'assignment-1', title: 'Kitap tekrar ödevi', dueDate: '2030-01-02T10:00:00Z', assignedStudentCount: 2 });
    http.verify();
  });

  it('updates and cancels a teacher assignment through the protected resource path', () => {
    const { service, http } = setup();

    service.updateTeacherAssignment('assignment/1', {
      assignmentId: 'assignment/1',
      title: '  Güncel ödev  ',
      assignmentSource: 'Digital',
      dueDate: '2030-01-03T10:00:00.000Z',
      studentIds: null
    }).subscribe();
    const update = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1'));
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.title).toBe('Güncel ödev');
    update.flush({ assignmentId: 'assignment/1', dueDate: '2030-01-03T10:00:00Z', assignedStudentCount: 2 });

    service.cancelTeacherAssignment('assignment/1').subscribe();
    const cancel = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/cancel'));
    expect(cancel.request.method).toBe('POST');
    cancel.flush({ message: 'Assignment cancelled successfully' });
    http.verify();
  });

  it('creates, reschedules, and records attendance for a teacher session', () => {
    const { service, http } = setup();

    service.createTeacherSession({
      teacherId: 'teacher-1',
      studentId: 'student-1',
      startTime: '2030-01-04T10:00:00.000Z',
      durationMinutes: 45,
      type: 'OneOnOne',
      subject: 'Matematik'
    }, 'session-key-123456').subscribe();
    const create = http.expectOne(candidate => candidate.url.endsWith('/sessions'));
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('Idempotency-Key')).toBe('session-key-123456');
    create.flush({ sessionId: 'session-1' });

    service.updateTeacherSession('session/1', {
      sessionId: 'session/1',
      title: '  Yeni seans  ',
      scheduledDate: '2030-01-05T10:00:00.000Z',
      durationMinutes: 60
    }).subscribe();
    const update = http.expectOne(candidate => candidate.url.endsWith('/sessions/session%2F1'));
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.title).toBe('Yeni seans');
    update.flush({ sessionId: 'session/1', scheduledDate: '2030-01-05T10:00:00Z' });

    service.updateSessionAttendance('session/1', 'student/1', true, '  Katıldı  ').subscribe();
    const attendance = http.expectOne(candidate => candidate.url.endsWith('/sessions/session%2F1/attendance'));
    expect(attendance.request.method).toBe('POST');
    expect(attendance.request.body).toEqual({
      sessionId: 'session/1',
      studentId: 'student/1',
      attended: true,
      notes: 'Katıldı'
    });
    attendance.flush({ message: 'Attendance updated successfully' });
    http.verify();
  });

  it('loads one teacher session through the protected detail route', () => {
    const { service, http } = setup();

    service.getTeacherSession('session/1').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/sessions/session%2F1'));
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'session/1', studentIds: [] });
    http.verify();
  });

  it('requests bounded student session pages with encoded student ids', () => {
    const { service, http } = setup();

    service.getStudentSessions('student/1', 2000, 200).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/sessions/student/student%2F1'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1000');
    expect(request.request.params.get('pageSize')).toBe('100');
    request.flush({ items: [], pageNumber: 1000, pageSize: 100, totalCount: 0, totalPages: 1 });
    http.verify();
  });

  it('requests the aggregate student progress report through the reports route', () => {
    const { service, http } = setup();

    service.getStudentProgress('student/1').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/reports/student/student%2F1/progress'));
    expect(request.request.method).toBe('GET');
    request.flush({ studentId: 'student/1', totalAssignments: 1 });
    http.verify();
  });

  it('creates a student goal with a required idempotency key', () => {
    const { service, http } = setup();

    service.createStudentGoal('student/1', {
      title: '  Matematik neti  ',
      category: 1,
      description: '  Haftalık tekrar  ',
      targetDate: '2030-01-01T23:59:59.000Z',
      targetScore: 80
    }, 'goal-key-123456').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/goals'));
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('goal-key-123456');
    expect(request.request.body).toEqual({
      studentId: 'student/1',
      title: 'Matematik neti',
      category: 1,
      teacherId: null,
      description: 'Haftalık tekrar',
      targetDate: '2030-01-01T23:59:59.000Z',
      targetScore: 80
    });
    request.flush({ goalId: 'goal-1' });
    http.verify();
  });

  it('updates goal progress through the protected goal resource', () => {
    const { service, http } = setup();

    service.updateGoalProgress('goal/1', 75).subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/goals/goal%2F1/progress'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ goalId: 'goal/1', progress: 75 });
    request.flush({ message: 'Progress updated successfully' });
    http.verify();
  });

  it('updates only the current student reflection for a session', () => {
    const { service, http } = setup();

    service.updateStudentSessionNote('session/1', 'student/1', '  Bugün fonksiyonları pekiştirdim.  ').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/sessions/session%2F1/student-note'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      sessionId: 'session/1',
      studentId: 'student/1',
      note: 'Bugün fonksiyonları pekiştirdim.'
    });
    request.flush(null);
    http.verify();
  });

  it('requests the parent child list from the identity-owned endpoint', () => {
    const { service, http } = setup();

    service.getMyChildren().subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/users/me/children'));
    expect(request.request.method).toBe('GET');
    request.flush([]);
    http.verify();
  });

  it('creates attachment metadata before uploading bytes with its hash', () => {
    const { service, http } = setup();
    const file = new File(['photo'], 'ödev.png', { type: 'image/png' });
    const hash = 'a'.repeat(64);

    service.createAttachment('assignment/1', 'student/1', file, hash).subscribe();
    const metadata = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/students/student%2F1/attachments'));
    expect(metadata.request.method).toBe('POST');
    expect(metadata.request.body).toEqual({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      fileName: 'ödev.png',
      contentType: 'image/png',
      sizeBytes: file.size,
      sha256: hash
    });
    metadata.flush({
      assignmentId: 'assignment/1',
      studentId: 'student/1',
      attachmentId: 'attachment/1',
      uploadUrl: '/api/assignments/assignment%2F1/students/student%2F1/attachments/attachment%2F1/content',
      uploadUrlExpiresAt: '2030-01-01T00:00:00Z',
      status: 'Pending'
    });

    service.uploadAttachment('assignment/1', 'student/1', 'attachment/1', file, hash).subscribe();
    const upload = http.expectOne(candidate => candidate.url.endsWith('/assignments/assignment%2F1/students/student%2F1/attachments/attachment%2F1/content'));
    expect(upload.request.method).toBe('PUT');
    expect(upload.request.headers.get('Content-Type')).toBe('image/png');
    expect(upload.request.headers.get('X-Content-SHA256')).toBe(hash);
    expect(upload.request.body).toBe(file);
    upload.flush({});
    http.verify();
  });

  it('builds a provider-independent calendar feed URL by audience', () => {
    TestBed.configureTestingModule({
      providers: [CoachingPortalService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingPortalService);

    expect(service.calendarFeedUrl('teacher')).toContain('/calendar/teacher.ics');
    expect(service.calendarFeedUrl('student')).toContain('/calendar/student.ics');
  });

  it('downloads the calendar feed through the authenticated HttpClient', () => {
    TestBed.configureTestingModule({
      providers: [CoachingPortalService, provideHttpClient(), provideHttpClientTesting()]
    });
    const service = TestBed.inject(CoachingPortalService);
    const http = TestBed.inject(HttpTestingController);

    service.downloadCalendarFeed('teacher').subscribe();

    const request = http.expectOne(candidate => candidate.url.endsWith('/calendar/teacher.ics'));
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' }));
    http.verify();
  });
});
