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

  it('saves teacher attendance against the selected student', () => {
    service.updateAttendance('session-1', 'student-1', false).subscribe();

    const request = http.expectOne('/api/sessions/session-1/attendance');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      sessionId: 'session-1', studentId: 'student-1', attended: false, notes: null
    });
    request.flush({ message: 'Attendance updated successfully' });
  });

  it('downloads the teacher calendar as a calendar file', () => {
    service.downloadCalendarFeed().subscribe();

    const request = http.expectOne('/api/calendar/teacher.ics');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' }));
  });
});
