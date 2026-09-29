import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherSessionsService } from './coaching-teacher-sessions.service';
import { CoachingTeacherSessionFormComponent } from './coaching-teacher-session-form.component';

describe('CoachingTeacherSessionFormComponent', () => {
  it('creates a future session for the authenticated teacher and selected student', () => {
    const service = sessionsService();
    const students = studentService();
    const fixture = createFixture(service, students);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const saved = vi.spyOn(component.saved, 'emit');
    component.form.subject = '  Matematik  ';
    component.form.startTime = '2030-01-04T10:00';
    component.form.notesVisibility = 'GuardianVisible';
    component.toggleStudent('student-1');
    component.submit();

    expect(service.createSession).toHaveBeenCalledOnce();
    const [request, idempotencyKey] = service.createSession.mock.calls[0] as unknown as [Record<string, unknown>, string];
    expect(request).toMatchObject({
      teacherId: 'teacher-1', studentId: 'student-1', studentIds: ['student-1'],
      subject: 'Matematik', teacherNotesVisibility: 'GuardianVisible', type: 'OneOnOne'
    });
    expect(idempotencyKey).toEqual(expect.any(String));
    expect(component.successMessage()).toBe('Seans planlandı.');
    expect(saved).toHaveBeenCalledOnce();
  });

  it('rejects missing participants, past dates, invalid durations and unsafe meeting links', () => {
    const service = sessionsService();
    const fixture = createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.form.startTime = '2000-01-01T10:00';
    component.submit();
    expect(component.errorMessage()).toContain('gelecekte');

    component.form.startTime = '2030-01-04T10:00';
    component.form.durationMinutes = 241;
    component.toggleStudent('student-1');
    component.submit();
    expect(component.errorMessage()).toContain('1 ile 240');

    component.form.durationMinutes = 45;
    component.form.meetingLink = 'javascript:alert(1)';
    component.submit();
    expect(component.errorMessage()).toContain('HTTP(S)');
    expect(service.createSession).not.toHaveBeenCalled();
  });

  it('requires at least two selected students for a group session', () => {
    const fixture = createFixture();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.startTime = '2030-01-04T10:00';
    component.form.type = 'Group';
    component.toggleStudent('student-1');
    component.submit();

    expect(component.errorMessage()).toContain('2 ile 100');
  });

  it('loads an existing session and keeps its participants read-only during edit', () => {
    const service = sessionsService();
    const fixture = createFixture(service, studentService(), 'session-1');
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(service.getSession).toHaveBeenCalledWith('session-1');
    expect(component.form.subject).toBe('Matematik');
    expect(fixture.nativeElement.textContent).toContain('Katılımcılar düzenleme sırasında değiştirilemez');
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');

    component.form.startTime = '2030-01-05T10:00';
    component.submit();

    expect(service.updateSession).toHaveBeenCalledOnce();
    const [updatedId, request] = service.updateSession.mock.calls[0] as unknown as [string, Record<string, unknown>];
    expect(updatedId).toBe('session-1');
    expect(request).toMatchObject({ title: 'Matematik', teacherNotesVisibility: 'StudentVisible' });
    expect(request['studentIds']).toBeUndefined();
    expect(service.createSession).not.toHaveBeenCalled();
  });

  it('paginates and searches the authenticated teacher roster', () => {
    const service = sessionsService();
    const students = studentService();
    students.getMyStudents.mockReturnValue(of({
      items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 2
    }));
    const fixture = createFixture(service, students);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.setStudentSearch(' Zeynep ');
    component.nextStudentsPage();

    expect(students.getMyStudents).toHaveBeenLastCalledWith(2, 100, 'Zeynep');
  });
});

function createFixture(service = sessionsService(), students = studentService(), sessionId: string | null = null) {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherSessionFormComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => 'teacher-1' } },
      { provide: CoachingTeacherSessionsService, useValue: service },
      { provide: CoachingTeacherStudentsService, useValue: students }
    ]
  });
  const fixture = TestBed.createComponent(CoachingTeacherSessionFormComponent);
  fixture.componentRef.setInput('sessionId', sessionId);
  return fixture;
}

function sessionsService() {
  return {
    getSession: vi.fn(() => of({
      id: 'session-1', studentId: 'student-1', startTime: '2030-01-04T10:00:00Z',
      endTime: '2030-01-04T10:45:00Z', durationMinutes: 45, subject: 'Matematik',
      status: 'Scheduled', type: 'OneOnOne', studentIds: ['student-1'],
      teacherNotes: 'Koç notu korunmalı', teacherNotesVisibility: 'StudentVisible'
    })),
    createSession: vi.fn(() => of({ sessionId: 'session-new' })),
    updateSession: vi.fn(() => of({ sessionId: 'session-1', scheduledDate: '2030-01-05T10:00:00Z' }))
  };
}

function studentService() {
  return {
    getMyStudents: vi.fn(() => of({
      items: [{ userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz', gradeLevel: 8, assignmentStartDate: '2030-01-01T00:00:00Z' }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    }))
  };
}
