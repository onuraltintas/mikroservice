import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import {
  ChildSummary,
  CoachingPortalService,
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
