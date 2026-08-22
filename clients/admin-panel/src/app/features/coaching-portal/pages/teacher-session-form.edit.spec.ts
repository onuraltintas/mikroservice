import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { TeacherSessionFormComponent } from './teacher-session-form.component';

describe('TeacherSessionFormComponent edit mode', () => {
  it('loads and reschedules the selected teacher session', () => {
    const session = {
      id: 'session-1',
      studentId: 'student-1',
      startTime: '2030-01-04T10:00:00Z',
      endTime: '2030-01-04T10:45:00Z',
      durationMinutes: 45,
      subject: 'Matematik',
      status: 'Scheduled',
      type: 'OneOnOne',
      studentIds: ['student-1'],
      meetingLink: 'https://meet.example.test/old'
    };
    const service = {
      getTeacherStudents: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      getTeacherSession: vi.fn(() => of(session)),
      updateTeacherSession: vi.fn(() => of({ sessionId: 'session-1', scheduledDate: '2030-01-05T10:00:00Z' }))
    };
    const router = { navigate: vi.fn(() => Promise.resolve(true)) };

    TestBed.configureTestingModule({
      imports: [TeacherSessionFormComponent],
      providers: [
        { provide: CoachingPortalService, useValue: service },
        { provide: AuthService, useValue: { userProfile: signal({ id: 'teacher-1', role: 'Teacher' }).asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'session-1' } } } },
        { provide: Router, useValue: router }
      ]
    });
    const component = TestBed.createComponent(TeacherSessionFormComponent).componentInstance;
    component.ngOnInit();

    expect(component.isEditing).toBe(true);
    expect(service.getTeacherSession).toHaveBeenCalledWith('session-1');
    expect(component.form.subject).toBe('Matematik');
    component.form.startTime = '2030-01-05T10:00';
    component.submit();

    expect(service.updateTeacherSession).toHaveBeenCalledOnce();
    expect(router.navigate).toHaveBeenCalledWith(['/coaching-portal/sessions']);
  });
});
