import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import {
  ChildSummary,
  CoachingPortalService,
  ExamResult,
  StudentProgressSummary
} from '../../../core/services/coaching-portal.service';
import { ParentChildrenComponent } from './parent-children.component';

describe('ParentChildrenComponent', () => {
  it('filters assignments for parent review without mutating the loaded list', () => {
    const service = {
      getMyChildren: vi.fn(() => of([])),
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(null))
    };
    TestBed.configureTestingModule({
      imports: [ParentChildrenComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>(parentProfile()) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(ParentChildrenComponent).componentInstance;
    component.assignments.set([
      { id: 'pending', title: 'Bekleyen', dueDate: '2026-09-01', status: 'Assigned', isOverdue: false },
      { id: 'submitted', title: 'Teslim', dueDate: '2026-08-01', status: 'Submitted', submittedAt: '2026-08-01', isOverdue: false },
      { id: 'overdue', title: 'Geciken', dueDate: '2026-07-01', status: 'Assigned', isOverdue: true }
    ]);

    expect(component.visibleAssignments()).toHaveLength(3);
    component.setAssignmentFilter('overdue');
    expect(component.visibleAssignments().map(item => item.id)).toEqual(['overdue']);
    component.setAssignmentFilter('submitted');
    expect(component.visibleAssignments().map(item => item.id)).toEqual(['submitted']);
    expect(component.assignments()).toHaveLength(3);
  });

  it('loads the selected child progress summary with the scoped coaching data', () => {
    const child: ChildSummary = {
      userId: 'child-1',
      firstName: 'Ada',
      lastName: 'Yılmaz',
      fullName: 'Ada Yılmaz'
    };
    const summary: StudentProgressSummary = {
      studentId: 'child-1',
      totalAssignments: 10,
      submittedAssignments: 8,
      gradedAssignments: 7,
      totalExams: 3,
      totalGoals: 2,
      completedGoals: 1,
      averageGoalProgress: 75,
      totalSessions: 4,
      upcomingSessions: 1,
      attendedSessions: 3,
      attendancePercentage: 75
    };
    const service = {
      getMyChildren: vi.fn(() => of([child])),
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(summary))
    };
    const profile = signal<UserProfile | null>(parentProfile());

    TestBed.configureTestingModule({
      imports: [ParentChildrenComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(ParentChildrenComponent).componentInstance;
    component.ngOnInit();

    expect(service.getStudentProgress).toHaveBeenCalledWith('child-1');
    expect(component.progressSummary()).toEqual(summary);
    expect(component.submittedAssignments()).toBe(8);
    expect(component.completedGoals()).toBe(1);
  });

  it('loads the next assignment page without replacing the already visible records', () => {
    const child: ChildSummary = {
      userId: 'child-1',
      firstName: 'Ada',
      lastName: 'Yılmaz',
      fullName: 'Ada Yılmaz'
    };
    const page = (id: string, pageNumber: number) => ({
      items: [{ id, title: id, dueDate: '2030-01-01', status: 'Assigned', isOverdue: false }],
      pageNumber,
      pageSize: 25,
      totalCount: 2,
      totalPages: 2
    });
    const service = {
      getMyChildren: vi.fn(() => of([child])),
      getStudentAssignments: vi.fn()
        .mockReturnValueOnce(of(page('assignment-1', 1)))
        .mockReturnValueOnce(of(page('assignment-2', 2))),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(null))
    };
    TestBed.configureTestingModule({
      imports: [ParentChildrenComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>(parentProfile()) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(ParentChildrenComponent).componentInstance;
    component.ngOnInit();
    component.loadMoreAssignments();

    expect(service.getStudentAssignments).toHaveBeenLastCalledWith('child-1', 2, 25);
    expect(component.assignmentPageNumber()).toBe(2);
    expect(component.assignments().map(item => item.id)).toEqual(['assignment-1', 'assignment-2']);
  });

  it('loads additional exam results and formats the score details for parents', () => {
    const child: ChildSummary = { userId: 'child-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz' };
    const result: ExamResult = { examId: 'exam-1', examTitle: 'LGS', examDate: '2030-01-01', examType: 'LGS', score: 420, maxScore: 500, subjectScores: { Matematik: 90 } };
    const service = {
      getMyChildren: vi.fn(() => of([])),
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn().mockReturnValueOnce(of({ items: [result], pageNumber: 1, pageSize: 25, totalCount: 2, totalPages: 2 })).mockReturnValueOnce(of({ items: [{ ...result, examId: 'exam-2' }], pageNumber: 2, pageSize: 25, totalCount: 2, totalPages: 2 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(null))
    };
    TestBed.configureTestingModule({
      imports: [ParentChildrenComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>(parentProfile()) } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(ParentChildrenComponent).componentInstance;
    component.selectChild(child);
    component.loadMoreExams();

    expect(service.getStudentExamResults).toHaveBeenLastCalledWith('child-1', 2, 25);
    expect(component.examResults()).toHaveLength(2);
    expect(component.scorePercentage(result)).toBe(84);
    expect(component.subjectScoreLabel(result.subjectScores)).toBe('Matematik: 90');
  });
});

function parentProfile(): UserProfile {
  return {
    id: 'parent-1',
    email: 'parent@example.test',
    firstName: 'Veli',
    lastName: 'Test',
    username: 'parent@example.test',
    roles: ['Parent'],
    role: 'Parent',
    permissions: []
  };
}
