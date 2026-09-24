import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InstitutionCoachingService } from './institution-coaching.service';

describe('InstitutionCoachingService', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()]
  }));

  it('assigns an institution student to a teacher through the scoped Identity route', () => {
    const service = TestBed.inject(InstitutionCoachingService);
    const http = TestBed.inject(HttpTestingController);
    service.assignTeacher({ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', gradeLevel: 8 }, 'teacher-1').subscribe();
    const request = http.expectOne(candidate => candidate.url.endsWith('/institution/students/student-1'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.teacherUserId).toBe('teacher-1');
    request.flush(null);
    http.verify();
  });

  it('creates assignments and sessions through institution-only write routes', () => {
    const service = TestBed.inject(InstitutionCoachingService);
    const http = TestBed.inject(HttpTestingController);
    service.createAssignment({ teacherId: 'teacher-1', institutionId: 'own-institution', title: 'Test', dueDate: '2030-01-01', assignmentType: 'Individual', assignmentSource: 'Digital', studentIds: ['student-1'] }, 'key-1').subscribe();
    const assignment = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/assignments'));
    expect(assignment.request.headers.get('Idempotency-Key')).toBe('key-1');
    assignment.flush({ assignmentId: 'assignment-1' });
    service.createSession({ teacherId: 'teacher-1', studentId: 'student-1', startTime: '2030-01-01', durationMinutes: 45, type: 'OneOnOne' }, 'key-2').subscribe();
    const session = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/sessions'));
    expect(session.request.headers.get('Idempotency-Key')).toBe('key-2');
    session.flush({ sessionId: 'session-1' });
    service.cancelSession('session-1').subscribe();
    const cancellation = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/sessions/session-1/cancel'));
    expect(cancellation.request.method).toBe('POST');
    cancellation.flush(null);
    service.updateAssignment('assignment-1', { assignmentId: 'assignment-1', title: 'Düzenlendi', assignmentSource: 'Digital', dueDate: '2030-01-02' }).subscribe();
    const update = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/assignments/assignment-1'));
    expect(update.request.method).toBe('PUT');
    update.flush({ assignmentId: 'assignment-1' });
    service.gradeAssignment('assignment-1', 'student-1', 80).subscribe();
    const grade = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/assignments/assignment-1/grade'));
    expect(grade.request.body.studentId).toBe('student-1');
    grade.flush({ assignmentId: 'assignment-1' });
    service.updateAttendance('session-1', 'student-1', true).subscribe();
    const attendance = http.expectOne(candidate => candidate.url.endsWith('/coaching-admin/institution/sessions/session-1/attendance'));
    expect(attendance.request.body.attended).toBe(true);
    attendance.flush(null);
    http.verify();
  });
});
