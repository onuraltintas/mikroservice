import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
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
        { provide: CoachingPortalService, useValue: service }
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
