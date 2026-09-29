import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherAssignmentsService } from './coaching-teacher-assignments.service';
import { CoachingTeacherSessionsService } from './coaching-teacher-sessions.service';
import { CoachingTeacherGoalsService } from './coaching-teacher-goals.service';
import { CoachingTeacherExamsService } from './coaching-teacher-exams.service';
import { CoachingTeacherWorkspaceComponent } from './coaching-teacher-workspace.component';

describe('CoachingTeacherWorkspaceComponent', () => {
  it('switches between student reports and teacher assignments in the Coaching workspace', async () => {
    const assignments = {
      getTeacherAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
      cancelAssignment: vi.fn(() => of({ message: 'ok' }))
    };
    await TestBed.configureTestingModule({
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
        { provide: CoachingTeacherAssignmentsService, useValue: assignments },
        { provide: CoachingTeacherGoalsService, useValue: {
          getTeacherGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
          createGoal: vi.fn(() => of({ goalId: 'goal-1' })),
          updateGoal: vi.fn(() => of({ goalId: 'goal-1', title: 'ok' }))
        } },
        { provide: CoachingTeacherExamsService, useValue: {
          getTeacherExams: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
          createExam: vi.fn(() => of({ examId: 'exam-1' })),
          updateExam: vi.fn(() => of({ examId: 'exam-1', examDate: '2030-01-01T00:00:00Z', maxScore: 100 })),
          getExamDetail: vi.fn(() => of({ id: 'exam-1', results: [], resultPageNumber: 1, resultPageSize: 25, resultTotalPages: 1 })),
          addExamResult: vi.fn(() => of({ message: 'ok' })),
          updateExamResult: vi.fn(() => of({ examId: 'exam-1', resultId: 'result-1', score: 90 }))
        } }
      ]
    }).compileComponents();
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

    (fixture.nativeElement.querySelector('[data-testid="goals-tab"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Hedeflerim');

    (fixture.nativeElement.querySelector('[data-testid="exams-tab"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sınavlarım');
  });
});
