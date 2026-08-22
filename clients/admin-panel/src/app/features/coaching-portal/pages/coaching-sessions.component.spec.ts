import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService, CoachingSession } from '../../../core/services/coaching-portal.service';
import { CoachingSessionsComponent } from './coaching-sessions.component';

describe('CoachingSessionsComponent', () => {
  it('loads a student session and saves the student reflection', () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const session: CoachingSession = {
      id: 'session-1',
      studentId: 'user-1',
      startTime: '2030-01-01T10:00:00Z',
      endTime: '2030-01-01T11:00:00Z',
      durationMinutes: 60,
      status: 'Scheduled',
      type: 'OneOnOne',
      studentIds: ['user-1'],
      studentNote: ''
    };
    const service = {
      getStudentSessions: vi.fn(() => of({ items: [session], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      updateStudentSessionNote: vi.fn(() => of(undefined))
    };

    TestBed.configureTestingModule({
      imports: [CoachingSessionsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(CoachingSessionsComponent).componentInstance;
    component.ngOnInit();
    component.setNote('session-1', 'Bugün hedefimi netleştirdim.');

    component.saveNote(session);

    expect(service.updateStudentSessionNote).toHaveBeenCalledWith(
      'session-1',
      'user-1',
      'Bugün hedefimi netleştirdim.'
    );
  });

  it('loads student reflections for a teacher session without enabling student editing', () => {
    const profile = signal<UserProfile | null>(user('Teacher'));
    const session: CoachingSession = {
      id: 'session-2',
      studentId: 'student-1',
      startTime: '2030-01-01T10:00:00Z',
      endTime: '2030-01-01T11:00:00Z',
      durationMinutes: 60,
      status: 'Completed',
      type: 'OneOnOne',
      studentIds: ['student-1'],
      studentReflections: [{
        studentId: 'student-1',
        note: 'Bu hafta deneme analizini tamamladım.',
        attendanceStatus: 'Present'
      }]
    };
    const service = {
      getTeacherSessions: vi.fn(() => of({ items: [session], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      calendarFeedUrl: vi.fn(() => '/api/calendar/teacher.ics')
    };

    TestBed.configureTestingModule({
      imports: [CoachingSessionsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const fixture = TestBed.createComponent(CoachingSessionsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci yansımaları');
    expect(fixture.nativeElement.textContent).toContain('Bu hafta deneme analizini tamamladım.');
    expect(fixture.nativeElement.querySelector('textarea')).toBeNull();
  });

  it('exposes a teacher iCalendar export link', () => {
    const profile = signal<UserProfile | null>(user('Teacher'));
    const service = {
      getTeacherSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      downloadCalendarFeed: vi.fn(() => of(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' })))
    };

    TestBed.configureTestingModule({
      imports: [CoachingSessionsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const fixture = TestBed.createComponent(CoachingSessionsComponent);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('[data-testid="calendar-export"]') as HTMLButtonElement;
    expect(button).not.toBeNull();
    fixture.componentInstance.exportCalendar();
    expect(service.downloadCalendarFeed).toHaveBeenCalledWith('teacher');
  });

  it('lets a teacher update attendance for an assigned session student', () => {
    const profile = signal<UserProfile | null>(user('Teacher'));
    const reflection = {
      studentId: 'student-1',
      note: 'Not',
      attendanceStatus: 'Present'
    };
    const session: CoachingSession = {
      id: 'session-3',
      studentId: 'student-1',
      startTime: '2030-01-01T10:00:00Z',
      endTime: '2030-01-01T11:00:00Z',
      durationMinutes: 60,
      status: 'Scheduled',
      type: 'OneOnOne',
      studentIds: ['student-1'],
      studentReflections: [reflection]
    };
    const service = {
      getTeacherSessions: vi.fn(() => of({ items: [session], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      updateSessionAttendance: vi.fn(() => of({ message: 'Attendance updated successfully' }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingSessionsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(CoachingSessionsComponent).componentInstance;
    component.ngOnInit();
    component.saveAttendance(session, reflection, false);

    expect(service.updateSessionAttendance).toHaveBeenCalledWith('session-3', 'student-1', false);
    expect(session.studentReflections?.[0].attendanceStatus).toBe('Absent');
  });

  it('lets a teacher cancel a scheduled session and updates the local status', () => {
    const profile = signal<UserProfile | null>(user('Teacher'));
    const session: CoachingSession = {
      id: 'session-4',
      studentId: 'student-1',
      startTime: '2030-01-01T10:00:00Z',
      endTime: '2030-01-01T11:00:00Z',
      durationMinutes: 60,
      status: 'Scheduled',
      type: 'OneOnOne',
      studentIds: ['student-1']
    };
    const service = {
      getTeacherSessions: vi.fn(() => of({ items: [session], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      cancelTeacherSession: vi.fn(() => of({ message: 'Session cancelled successfully' }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingSessionsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(CoachingSessionsComponent).componentInstance;
    component.ngOnInit();
    component.cancelSession(session);

    expect(service.cancelTeacherSession).toHaveBeenCalledWith('session-4');
    expect(component.sessions()[0].status).toBe('Cancelled');
  });
});

function user(role: string): UserProfile {
  return {
    id: 'user-1',
    email: 'student@example.test',
    firstName: 'Test',
    lastName: 'Student',
    username: 'student@example.test',
    roles: [role],
    role,
    permissions: []
  };
}
