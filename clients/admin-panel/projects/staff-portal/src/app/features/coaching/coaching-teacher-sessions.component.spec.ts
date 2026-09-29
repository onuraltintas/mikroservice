import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherSessionsService, CoachingTeacherSession } from './coaching-teacher-sessions.service';
import { CoachingTeacherSessionsComponent } from './coaching-teacher-sessions.component';

describe('CoachingTeacherSessionsComponent', () => {
  it('loads teacher-owned sessions, student names and reflection details', () => {
    const sessions = sessionService([session('session-1')]);
    const students = studentService();
    const fixture = createFixture(sessions, students);
    fixture.detectChanges();

    expect(sessions.getTeacherSessions).toHaveBeenCalledWith('teacher-1', 1, 25);
    expect(students.getMyStudents).toHaveBeenCalledWith(1, 1, undefined, ['student-1']);
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Bu hafta hedefimi tamamladım.');
    expect(fixture.nativeElement.textContent).toContain('Yalnız koç');
    expect(fixture.nativeElement.textContent).toContain('Takvime aktar');
  });

  it('does not request sessions when the authenticated teacher id is unavailable', () => {
    const sessions = sessionService([]);
    const fixture = createFixture(sessions, studentService(), null);
    fixture.detectChanges();

    expect(sessions.getTeacherSessions).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Öğretmen oturumu bulunamadı');
  });

  it('updates attendance only after the API confirms the change', () => {
    const sessions = sessionService([session('session-1')]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const reflection = fixture.componentInstance.sessions()[0].studentReflections![0];

    fixture.componentInstance.saveAttendance(fixture.componentInstance.sessions()[0], reflection, false);

    expect(sessions.updateAttendance).toHaveBeenCalledWith('session-1', 'student-1', false);
    expect(fixture.componentInstance.sessions()[0].studentReflections![0].attendanceStatus).toBe('Absent');
    expect(fixture.componentInstance.sessions()[0].status).toBe('Completed');
  });

  it('keeps the session unchanged and shows an error when attendance save fails', () => {
    const sessions = sessionService([session('session-1')]);
    sessions.updateAttendance.mockReturnValue(throwError(() => new Error('forbidden')));
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const current = fixture.componentInstance.sessions()[0];

    fixture.componentInstance.saveAttendance(current, current.studentReflections![0], false);

    expect(fixture.componentInstance.sessions()[0].studentReflections![0].attendanceStatus).toBe('Present');
    expect(fixture.componentInstance.errorMessage()).toBe('Yoklama kaydedilemedi. Lütfen tekrar deneyin.');
  });

  it('cancels only future non-terminal sessions and refreshes their visible status', () => {
    const sessions = sessionService([session('session-1')]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const scheduled = component.sessions()[0];

    expect(component.canCancel(scheduled)).toBe(true);
    component.requestCancellation(scheduled.id);
    expect(sessions.cancelSession).not.toHaveBeenCalled();
    component.confirmCancellation(scheduled);
    expect(sessions.cancelSession).toHaveBeenCalledWith('session-1');
    expect(component.sessions()[0].status).toBe('Cancelled');
    expect(component.canCancel(component.sessions()[0])).toBe(false);
    expect(component.canCancel({ ...scheduled, startTime: '2020-01-01T10:00:00Z' })).toBe(false);
    expect(component.canCancel({ ...scheduled, status: 'Completed' })).toBe(false);
  });

  it('appends later pages without losing the first page', () => {
    const sessions = sessionService([session('session-1')]);
    sessions.getTeacherSessions
      .mockReturnValueOnce(of(page([session('session-1')], 1, 2)))
      .mockReturnValueOnce(of(page([session('session-2')], 2, 2)));
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();

    fixture.componentInstance.loadMore();

    expect(sessions.getTeacherSessions).toHaveBeenNthCalledWith(2, 'teacher-1', 2, 25);
    expect(fixture.componentInstance.sessions().map(item => item.id)).toEqual(['session-1', 'session-2']);
  });

  it('opens the create and edit forms from the teacher session list', () => {
    const sessions = sessionService([session('session-1')]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="create-session"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Yeni seans planla');

    (fixture.nativeElement.querySelector('[data-testid="close-session-form"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="edit-session-session-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(sessions.getSession).toHaveBeenCalledWith('session-1');
    expect(fixture.nativeElement.textContent).toContain('Seansı düzenle');
  });
});

function createFixture(
  sessions = sessionService([]),
  students = studentService(),
  teacherId: string | null = 'teacher-1'
) {
  TestBed.configureTestingModule({
    imports: [CoachingTeacherSessionsComponent],
    providers: [
      { provide: StaffAuthService, useValue: { getCurrentUserId: () => teacherId } },
      { provide: CoachingTeacherSessionsService, useValue: sessions },
      { provide: CoachingTeacherStudentsService, useValue: students }
    ]
  });
  return TestBed.createComponent(CoachingTeacherSessionsComponent);
}

function session(id: string): CoachingTeacherSession {
  return {
    id,
    studentId: 'student-1',
    startTime: '2030-01-01T10:00:00Z',
    endTime: '2030-01-01T10:45:00Z',
    durationMinutes: 45,
    subject: 'Matematik',
    status: 'Scheduled',
    type: 'OneOnOne',
    studentIds: ['student-1'],
    studentReflections: [{ studentId: 'student-1', note: 'Bu hafta hedefimi tamamladım.', attendanceStatus: 'Present' }],
    teacherNotes: 'Düzenli tekrar önerildi.',
    teacherNotesVisibility: 'CoachPrivate'
  };
}

function page(items: CoachingTeacherSession[], pageNumber = 1, totalPages = 1) {
  return { items, pageNumber, pageSize: 25, totalCount: items.length, totalPages };
}

function sessionService(items: CoachingTeacherSession[]) {
  return {
    getTeacherSessions: vi.fn(() => of(page(items))),
    getSession: vi.fn(() => of(session('session-1'))),
    createSession: vi.fn(() => of({ sessionId: 'session-new' })),
    updateSession: vi.fn(() => of({ sessionId: 'session-1', scheduledDate: '2030-01-02T10:00:00Z' })),
    cancelSession: vi.fn(() => of({ message: 'ok' })),
    updateAttendance: vi.fn(() => of({ message: 'ok' })),
    downloadCalendarFeed: vi.fn(() => of(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' })))
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
