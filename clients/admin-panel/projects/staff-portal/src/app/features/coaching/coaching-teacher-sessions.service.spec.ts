import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CoachingTeacherSessionsService } from './coaching-teacher-sessions.service';

describe('CoachingTeacherSessionsService', () => {
  let http: HttpTestingController;
  let service: CoachingTeacherSessionsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CoachingTeacherSessionsService);
  });

  afterEach(() => http.verify());

  it('loads a bounded page of sessions for the current teacher', () => {
    service.getTeacherSessions('teacher-1', 2, 10).subscribe();

    const request = http.expectOne('/api/sessions/teacher/teacher-1?pageNumber=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('cancels a session using the Coaching API route', () => {
    service.cancelSession('session-1').subscribe();

    const request = http.expectOne('/api/sessions/session-1/cancel');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ message: 'Session cancelled successfully' });
  });

  it('loads an authorized session detail for editing', () => {
    service.getSession('session-1').subscribe();

    const request = http.expectOne('/api/sessions/session-1');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'session-1' });
  });

  it('creates a session with an idempotency key and normalized participant ids', () => {
    service.createSession({
      teacherId: 'teacher-1', studentId: 'student-1', studentIds: ['student-1', ' student-1 '],
      startTime: '2030-01-01T10:00:00Z', durationMinutes: 45, subject: '  Matematik  ',
      notes: '  Not  ', type: 'OneOnOne', meetingLink: '  https://meet.example.test  ',
      teacherNotesVisibility: 'StudentVisible'
    }, 'idempotency-key').subscribe();

    const request = http.expectOne('/api/sessions');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('idempotency-key');
    expect(request.request.body).toMatchObject({
      teacherId: 'teacher-1', studentId: 'student-1', studentIds: ['student-1'],
      subject: 'Matematik', notes: 'Not', meetingLink: 'https://meet.example.test',
      teacherNotesVisibility: 'StudentVisible'
    });
    request.flush({ sessionId: 'session-1' });
  });

  it('updates editable session details without pretending participant assignments are editable', () => {
    service.updateSession('session-1', {
      sessionId: 'session-1', title: '  Yeni başlık  ', description: '  Açıklama  ',
      scheduledDate: '2030-01-02T10:00:00Z', durationMinutes: 60,
      meetingLink: '  https://meet.example.test  ', teacherNotes: '  Koç notu  ',
      teacherNotesVisibility: 'CoachPrivate'
    }).subscribe();

    const request = http.expectOne('/api/sessions/session-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toMatchObject({
      sessionId: 'session-1', title: 'Yeni başlık', description: 'Açıklama',
      teacherNotes: 'Koç notu', meetingLink: 'https://meet.example.test'
    });
    expect(request.request.body['studentIds']).toBeUndefined();
    request.flush({ sessionId: 'session-1', scheduledDate: '2030-01-02T10:00:00Z' });
  });

  it('saves teacher attendance against the selected student', () => {
    service.updateAttendance('session-1', 'student-1', 'Late').subscribe();

    const request = http.expectOne('/api/sessions/session-1/attendance');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      sessionId: 'session-1', studentId: 'student-1', attended: true, status: 'Late', notes: null
    });
    request.flush({ message: 'Attendance updated successfully' });
  });

  it('cancels the following series sessions, changes participants and loads the weekly agenda (roadmap O6)', () => {
    service.cancelSession('session-1', true).subscribe();
    const cancel = http.expectOne(r => r.url === '/api/sessions/session-1/cancel');
    expect(cancel.request.params.get('following')).toBe('true');
    cancel.flush({ message: 'ok' });

    service.updateParticipants('session-1', [' student-1 ', 'student-2', 'student-1'], true).subscribe();
    const participants = http.expectOne('/api/sessions/session-1/participants');
    expect(participants.request.method).toBe('PUT');
    expect(participants.request.body).toEqual({ studentIds: ['student-1', 'student-2'], includeFollowing: true });
    participants.flush({ message: 'ok' });

    service.getTeacherAgenda('teacher-1', '2030-01-07').subscribe();
    const agenda = http.expectOne('/api/sessions/teacher/teacher-1/agenda?weekStart=2030-01-07');
    expect(agenda.request.method).toBe('GET');
    agenda.flush({ weekStart: '2030-01-07', sessions: [] });
  });

  it('sends the recurrence of a new series', () => {
    service.createSession({
      teacherId: 't', studentId: 's', studentIds: ['s'], startTime: '2030-01-07T10:00:00Z', durationMinutes: 45,
      subject: null, notes: null, type: 'OneOnOne', meetingLink: null, teacherNotesVisibility: 'CoachPrivate',
      recurrence: { intervalWeeks: 1, count: 8 }
    }, 'key-1').subscribe();
    const request = http.expectOne('/api/sessions');
    expect(request.request.body.recurrence).toEqual({ intervalWeeks: 1, count: 8 });
    expect(request.request.headers.get('Idempotency-Key')).toBe('key-1');
    request.flush({ sessionId: 'x', seriesId: 'y', count: 8 });
  });

  it('downloads the teacher calendar as a calendar file', () => {
    service.downloadCalendarFeed().subscribe();

    const request = http.expectOne('/api/calendar/teacher.ics');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' }));
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
