import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService, CoachingSession } from '../../../core/services/coaching-portal.service';
import { CoachingSessionsComponent } from './coaching-sessions.component';
import { CoachingPortalViewService } from '../coaching-portal-view.service';

describe('CoachingSessionsComponent', () => {
  it('loads own sessions rather than teacher sessions in the selected student view', async () => {
    const profile = signal<UserProfile | null>({ ...user('Teacher'), roles: ['Teacher', 'Student'] });
    const service = {
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getTeacherSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }))
    };
    TestBed.configureTestingModule({ imports: [CoachingSessionsComponent], providers: [
      { provide: AuthService, useValue: { userProfile: profile } },
      { provide: CoachingPortalService, useValue: service },
      { provide: ActivatedRoute, useValue: {} }
    ] });
    TestBed.inject(CoachingPortalViewService).select('Student');
    const component = TestBed.createComponent(CoachingSessionsComponent).componentInstance;
    await component.ngOnInit();
    expect(service.getStudentSessions).toHaveBeenCalledWith('user-1', 1, 25);
    expect(service.getTeacherSessions).not.toHaveBeenCalled();
  });
  it('loads a student session and saves the student reflection', async () => {
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
      studentNote: '',
      sharedNotes: 'Bu hafta çalışma planını ailene göster.',
      sharedNotesVisibility: 'StudentVisible'
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
    const fixture = TestBed.createComponent(CoachingSessionsComponent);
    const component = fixture.componentInstance;
    await component.ngOnInit();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Seninle paylaşılan öğretmen notu');
    expect(fixture.nativeElement.textContent).toContain('Bu hafta çalışma planını ailene göster.');
    component.setNote('session-1', 'Bugün hedefimi netleştirdim.');

    component.saveNote(session);

    expect(service.updateStudentSessionNote).toHaveBeenCalledWith(
      'session-1',
      'user-1',
      'Bugün hedefimi netleştirdim.'
    );
  });

  it('shows no teacher tools to a teacher who reaches the student calendar (Roadmap D3)', async () => {
    const service = { getStudentSessions: vi.fn() };
    TestBed.configureTestingModule({ imports: [CoachingSessionsComponent], providers: [
      { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>(user('Teacher')) } },
      { provide: CoachingPortalService, useValue: service },
      { provide: ActivatedRoute, useValue: {} }
    ] });
    const fixture = TestBed.createComponent(CoachingSessionsComponent);
    await fixture.componentInstance.ngOnInit(); fixture.detectChanges();
    expect(service.getStudentSessions).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('öğrenci profili');
    expect(fixture.nativeElement.querySelector('[data-testid="calendar-export"]')).toBeNull();
  });

  it('loads additional session pages without replacing the first page', async () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const firstSession = { ...sessionFor('session-page-1'), subject: 'Matematik' };
    const secondSession = { ...sessionFor('session-page-2'), subject: 'Fizik' };
    const service = {
      getStudentSessions: vi.fn()
        .mockReturnValueOnce(of({ items: [firstSession], pageNumber: 1, pageSize: 1, totalCount: 2, totalPages: 2 }))
        .mockReturnValueOnce(of({ items: [secondSession], pageNumber: 2, pageSize: 1, totalCount: 2, totalPages: 2 }))
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
    await component.ngOnInit();
    component.loadMore();

    expect(service.getStudentSessions).toHaveBeenNthCalledWith(2, 'user-1', 2, 25);
    expect(component.sessions().map(session => session.id)).toEqual(['session-page-1', 'session-page-2']);
    expect(component.totalCount()).toBe(2);
  });
});

function sessionFor(id: string): CoachingSession {
  return {
    id,
    studentId: 'student-1',
    startTime: '2030-01-01T10:00:00Z',
    endTime: '2030-01-01T11:00:00Z',
    durationMinutes: 60,
    status: 'Scheduled',
    type: 'OneOnOne',
    studentIds: ['student-1']
  };
}

function user(role: string): UserProfile {
  return {
    id: 'user-1',
    email: 'student@example.test',
    firstName: 'Test',
    lastName: 'Student',
    username: 'student@example.test',
    roles: [role],
    product: 'coaching',
    role,
    permissions: []
  };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
