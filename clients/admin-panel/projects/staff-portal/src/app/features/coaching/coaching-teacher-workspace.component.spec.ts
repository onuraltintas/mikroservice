import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherAssignmentsService } from './coaching-teacher-assignments.service';
import { CoachingTeacherSessionsService } from './coaching-teacher-sessions.service';
import { CoachingTeacherWorkspaceComponent } from './coaching-teacher-workspace.component';

describe('CoachingTeacherWorkspaceComponent', () => {
  it('switches between student reports and teacher assignments in the Coaching workspace', () => {
    const assignments = {
      getTeacherAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
      cancelAssignment: vi.fn(() => of({ message: 'ok' }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingTeacherWorkspaceComponent],
      providers: [
        { provide: StaffAuthService, useValue: { getCurrentUserId: () => 'teacher-1' } },
        { provide: CoachingTeacherSessionsService, useValue: {
          getTeacherSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
          cancelSession: vi.fn(() => of({ message: 'ok' })),
          updateAttendance: vi.fn(() => of({ message: 'ok' })),
          downloadCalendarFeed: vi.fn(() => of(new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' })))
        } },
        { provide: CoachingTeacherStudentsService, useValue: {
          getMyStudents: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
          getStudentProgress: vi.fn(),
          getStudentHistory: vi.fn()
        } },
        { provide: CoachingTeacherAssignmentsService, useValue: assignments }
      ]
    });
    const fixture = TestBed.createComponent(CoachingTeacherWorkspaceComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrencilerim');
    expect(assignments.getTeacherAssignments).not.toHaveBeenCalled();

    (fixture.nativeElement.querySelector('[data-testid="assignments-tab"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(assignments.getTeacherAssignments).toHaveBeenCalledWith('teacher-1', 1, 25, undefined);
    expect(fixture.nativeElement.textContent).toContain('Ödevlerim');

    (fixture.nativeElement.querySelector('[data-testid="sessions-tab"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seanslarım');
  });
});
