import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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

    fixture.componentInstance.saveAttendance(fixture.componentInstance.sessions()[0], reflection, 'Excused');

    expect(sessions.updateAttendance).toHaveBeenCalledWith('session-1', 'student-1', 'Excused');
    expect(fixture.componentInstance.sessions()[0].studentReflections![0].attendanceStatus).toBe('Excused');
    expect(fixture.componentInstance.sessions()[0].status).toBe('Completed');
  });

  it('keeps the session unchanged and shows an error when attendance save fails', () => {
    const sessions = sessionService([session('session-1')]);
    sessions.updateAttendance.mockReturnValue(throwError(() => new Error('forbidden')));
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const current = fixture.componentInstance.sessions()[0];

    fixture.componentInstance.saveAttendance(current, current.studentReflections![0], 'Absent');

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
    expect(sessions.cancelSession).toHaveBeenCalledWith('session-1', false);
    expect(component.sessions()[0].status).toBe('Cancelled');
    expect(component.canCancel(component.sessions()[0])).toBe(false);
    expect(component.canCancel({ ...scheduled, startTime: '2020-01-01T10:00:00Z' })).toBe(false);
    expect(component.canCancel({ ...scheduled, status: 'Completed' })).toBe(false);
  });

  it('offers attendance with late and excused only for sessions that have started and were not cancelled', () => {
    const past = { ...session('session-1'), startTime: '2020-01-01T10:00:00Z', endTime: '2020-01-01T10:45:00Z' };
    const sessions = sessionService([past, { ...session('session-2'), status: 'Cancelled', startTime: '2020-01-02T10:00:00Z' }, session('session-3')]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const groups = fixture.nativeElement.querySelectorAll('.attendance-actions');
    expect(groups.length).toBe(1);
    expect(groups[0].textContent).toContain('Geç geldi');
    expect(groups[0].textContent).toContain('Mazeretli');
    expect(fixture.componentInstance.canRecordAttendance(fixture.componentInstance.sessions()[2])).toBe(false);
  });

  it('shows the server explanation when attendance is refused', () => {
    const sessions = sessionService([session('session-1')]);
    sessions.updateAttendance.mockReturnValue(throwError(() => ({ status: 409, error: { error: 'Seans başlamadan katılım girilemez.' } })));
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const current = fixture.componentInstance.sessions()[0];
    fixture.componentInstance.saveAttendance(current, current.studentReflections![0], 'Present');
    expect(fixture.componentInstance.errorMessage()).toBe('Seans başlamadan katılım girilemez.');
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

  it('cancels this and the following planned sessions of a series (roadmap O6)', () => {
    const first = { ...session('session-1'), seriesId: 'series-1' };
    const second = { ...session('session-2'), seriesId: 'series-1', startTime: '2030-01-08T10:00:00Z' };
    const earlier = { ...session('session-0'), seriesId: 'series-1', startTime: '2029-12-25T10:00:00Z' };
    const other = session('session-3');
    const sessions = sessionService([earlier, first, second, other]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.textContent).toContain('Tekrarlı');

    component.requestCancellation('session-1');
    fixture.detectChanges();
    const following = fixture.nativeElement.querySelector('[data-testid="cancel-following-session-1"]') as HTMLButtonElement;
    expect(following).not.toBeNull();
    following.click();

    expect(sessions.cancelSession).toHaveBeenCalledWith('session-1', true);
    expect(component.sessions().map(item => [item.id, item.status])).toEqual([
      ['session-0', 'Scheduled'], ['session-1', 'Cancelled'], ['session-2', 'Cancelled'], ['session-3', 'Scheduled']
    ]);
  });

  it('offers the following-sessions cancel only for series sessions', () => {
    const sessions = sessionService([session('session-1')]);
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    fixture.componentInstance.requestCancellation('session-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="cancel-following-session-1"]')).toBeNull();
  });

  it('shows a Monday-based weekly agenda grouped by day and moves between weeks (roadmap O6)', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2030-01-03T09:00:00Z')); // Thursday
    try {
      const sessions = sessionService([session('session-1')]);
      sessions.getTeacherAgenda.mockReturnValue(of({ weekStart: '2029-12-30', sessions: [
        { ...session('a'), startTime: '2029-12-31T07:00:00Z' },
        { ...session('b'), startTime: '2030-01-02T21:30:00Z', subject: 'Gece etüdü' }
      ] }));
      const fixture = createFixture(sessions, studentService());
      fixture.detectChanges();
      const component = fixture.componentInstance;

      component.showAgenda();
      fixture.detectChanges();
      expect(sessions.getTeacherAgenda).toHaveBeenCalledWith('teacher-1', '2029-12-31');
      const days = component.agendaDays();
      expect(days.map(day => day.date)).toEqual(['2029-12-31', '2030-01-01', '2030-01-02', '2030-01-03', '2030-01-04', '2030-01-05', '2030-01-06']);
      expect(days[0].sessions.map(item => item.id)).toEqual(['a']);
      // 21:30 UTC on 2 January is 00:30 on 3 January in Turkey.
      expect(days[3].sessions.map(item => item.id)).toEqual(['b']);
      expect(fixture.nativeElement.textContent).toContain('Gece etüdü');

      component.shiftAgendaWeek(1);
      expect(sessions.getTeacherAgenda).toHaveBeenLastCalledWith('teacher-1', '2030-01-07');
      component.shiftAgendaWeek(-2);
      expect(sessions.getTeacherAgenda).toHaveBeenLastCalledWith('teacher-1', '2029-12-24');
      component.showList();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.session-list')).not.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('shows an agenda error without losing the list', () => {
    const sessions = sessionService([session('session-1')]);
    sessions.getTeacherAgenda.mockReturnValue(throwError(() => new Error('down')));
    const fixture = createFixture(sessions, studentService());
    fixture.detectChanges();
    fixture.componentInstance.showAgenda();
    fixture.detectChanges();
    expect(fixture.componentInstance.agendaError()).toContain('yüklenemedi');
    expect(fixture.componentInstance.sessions().length).toBe(1);
  });

  it('offers the personal calendar subscription for the teacher role (roadmap O6)', () => {
    const fixture = createFixture(sessionService([session('session-1')]), studentService());
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('edu-coaching-calendar-subscription')).not.toBeNull();
    const request = TestBed.inject(HttpTestingController).expectOne(r => r.url === '/api/calendar/subscription');
    expect(request.request.params.get('role')).toBe('teacher');
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
      provideHttpClient(),
      provideHttpClientTesting(),
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
    cancelSession: vi.fn((_id: string, _following?: boolean) => of({ message: 'ok' })),
    getTeacherAgenda: vi.fn((): Observable<{ weekStart: string; sessions: CoachingTeacherSession[] }> => of({ weekStart: '2030-01-01', sessions: [] })),
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

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
