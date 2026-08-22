import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService, ExamResult, Goal } from '../../../core/services/coaching-portal.service';
import { CoachingPortalProgressComponent } from './coaching-portal-progress.component';

describe('CoachingPortalProgressComponent', () => {
  const profile = signal<UserProfile | null>(null);
  let service: {
    getStudentProgress: ReturnType<typeof vi.fn>;
    getStudentGoals: ReturnType<typeof vi.fn>;
    getStudentExamResults: ReturnType<typeof vi.fn>;
    createStudentGoal: ReturnType<typeof vi.fn>;
    updateGoalProgress: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    profile.set(user('Student'));
    service = {
      getStudentProgress: vi.fn(() => of({
        studentId: 'user-1', totalAssignments: 0, submittedAssignments: 0, gradedAssignments: 0,
        totalExams: 0, totalGoals: 0, completedGoals: 0, averageGoalProgress: 0,
        totalSessions: 0, upcomingSessions: 0, attendedSessions: 0
      })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })),
      createStudentGoal: vi.fn(() => of({ goalId: 'goal-1' })),
      updateGoalProgress: vi.fn(() => of({ message: 'ok' }))
    };

    await TestBed.configureTestingModule({
      imports: [CoachingPortalProgressComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service }
      ]
    }).compileComponents();
  });

  it('creates a self-managed goal with a generated idempotency key', () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    component.ngOnInit();
    component.toggleGoalForm();
    component.goalForm.setValue({
      title: 'Matematik neti',
      category: 1,
      description: 'Haftalık tekrar',
      targetDate: '2030-01-01',
      targetScore: 80
    });

    component.createGoal();

    expect(service.createStudentGoal).toHaveBeenCalledWith(
      'user-1',
      expect.objectContaining({ title: 'Matematik neti', category: 1, targetScore: 80 }),
      expect.any(String)
    );
    expect(component.showGoalForm()).toBe(false);
  });

  it('updates only the selected goal progress after a successful save', () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    const goal: Goal = {
      id: 'goal-1',
      title: 'Matematik',
      category: 'SubjectMastery',
      progress: 20,
      isCompleted: false
    };
    component.goals.set([goal]);
    const input = document.createElement('input');
    input.value = '75';

    component.updateProgress(goal, { target: input } as unknown as Event);

    expect(service.updateGoalProgress).toHaveBeenCalledWith('goal-1', 75);
    expect(component.goals()[0]).toMatchObject({ progress: 75, isCompleted: false });
  });

  it('keeps the exam trend chronological and loads the next result page', () => {
    const component = TestBed.createComponent(CoachingPortalProgressComponent).componentInstance;
    const first: ExamResult = { examId: 'exam-1', examTitle: 'İlk deneme', examDate: '2030-02-01', examType: 'Mock', score: 60, maxScore: 100 };
    const second: ExamResult = { examId: 'exam-2', examTitle: 'Son deneme', examDate: '2030-03-01', examType: 'Mock', score: 80, maxScore: 100 };
    component.examResults.set([second, first]);
    component.examPageNumber.set(1);
    component.examTotalPages.set(2);
    service.getStudentExamResults.mockReturnValue(of({ items: [second], pageNumber: 2, pageSize: 25, totalCount: 2, totalPages: 2 }));

    expect(component.examTrend().map(result => result.examId)).toEqual(['exam-1', 'exam-2']);
    expect(component.scorePercentage(second)).toBe(80);
    component.loadMoreExams();

    expect(service.getStudentExamResults).toHaveBeenLastCalledWith('user-1', 2, 25);
    expect(component.examPageNumber()).toBe(2);
    expect(component.examResults()).toHaveLength(3);
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
