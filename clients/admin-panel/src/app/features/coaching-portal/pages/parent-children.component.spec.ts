import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, Subject } from 'rxjs';
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
  const agreementStatus = {
    documentId: 'document-1', documentVersion: '2026.1', locale: 'tr-TR',
    title: 'Koçluk Anlaşması', documentReference: 'https://legal.example.test/coaching',
    contentSha256: 'a'.repeat(64), effectiveAt: '2026-09-21T00:00:00Z',
    partyRole: 'Parent' as const, acknowledgedByCurrentRepresentative: false,
    acknowledgementId: null, acknowledgedAt: null
  };

  it('ignores the previous child response after another child is selected', () => {
    const pending = new Subject<any>();
    const empty = { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 };
    const service = {
      getStudentAssignments: vi.fn().mockReturnValueOnce(pending).mockReturnValue(of(empty)),
      getStudentGoals: () => of(empty), getStudentExamResults: () => of(empty),
      getStudentSessions: () => of(empty), getStudentProgress: () => of(null),
      getCurrentRepresentativeAgreement: () => of(agreementStatus)
    };
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: {} }, { provide: CoachingPortalService, useValue: service },
      { provide: ActivatedRoute, useValue: {} }
    ] });
    const component = TestBed.createComponent(ParentChildrenComponent).componentInstance;
    const child = { relationshipId: 'rel-a', relationship: 'Mother' as const, userId: 'a', firstName: 'A', lastName: 'Test', fullName: 'A Test' };
    component.selectChild(child);
    component.selectChild({ ...child, userId: 'b' });
    pending.next({ ...empty, items: [{ id: 'old' }] }); pending.complete();
    expect(component.assignments()).toEqual([]);
    expect(component.submittedAssignments()).toBeNull();
  });

  it('filters assignments for parent review without mutating the loaded list', () => {
    const service = {
      getMyChildren: vi.fn(() => of([])),
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(null)),
      getCurrentRepresentativeAgreement: vi.fn(() => of(agreementStatus))
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
      relationshipId: 'rel-1', relationship: 'Mother',
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
      getStudentProgress: vi.fn(() => of(summary)),
      getCurrentRepresentativeAgreement: vi.fn(() => of(agreementStatus))
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
      relationshipId: 'rel-1', relationship: 'Mother',
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
      getStudentProgress: vi.fn(() => of(null)),
      getCurrentRepresentativeAgreement: vi.fn(() => of(agreementStatus))
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
    const child: ChildSummary = { relationshipId: 'rel-1', relationship: 'Mother', userId: 'child-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz' };
    const result: ExamResult = { examId: 'exam-1', examTitle: 'LGS', examDate: '2030-01-01', examType: 'LGS', score: 420, maxScore: 500, subjectScores: { Matematik: 90 } };
    const service = {
      getMyChildren: vi.fn(() => of([])),
      getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentExamResults: vi.fn().mockReturnValueOnce(of({ items: [result], pageNumber: 1, pageSize: 25, totalCount: 2, totalPages: 2 })).mockReturnValueOnce(of({ items: [{ ...result, examId: 'exam-2' }], pageNumber: 2, pageSize: 25, totalCount: 2, totalPages: 2 })),
      getStudentSessions: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })),
      getStudentProgress: vi.fn(() => of(null)),
      getCurrentRepresentativeAgreement: vi.fn(() => of(agreementStatus))
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

  it('requires review before accepting and can withdraw its own representative evidence', () => {
    const child: ChildSummary = {
      relationshipId: 'rel-1', relationship: 'Guardian', userId: 'child-1',
      firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz'
    };
    const empty = { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 };
    const service = {
      getStudentAssignments: vi.fn(() => of(empty)), getStudentGoals: vi.fn(() => of(empty)),
      getStudentExamResults: vi.fn(() => of(empty)), getStudentSessions: vi.fn(() => of(empty)),
      getStudentProgress: vi.fn(() => of(null)),
      getCurrentRepresentativeAgreement: vi.fn(() => of({ ...agreementStatus, partyRole: 'LegalGuardian' as const })),
      acknowledgeAgreementAsRepresentative: vi.fn(() => of({
        acknowledgementId: 'ack-1', agreementDocumentId: 'document-1', locale: 'tr-TR',
        acknowledgedAt: '2026-09-21T12:00:00Z', withdrawnAt: null
      })),
      withdrawRepresentativeAcknowledgement: vi.fn(() => of({
        acknowledgementId: 'ack-1', agreementDocumentId: 'document-1', locale: 'tr-TR',
        acknowledgedAt: '2026-09-21T12:00:00Z', withdrawnAt: '2026-09-21T12:05:00Z'
      }))
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

    component.acceptAgreement();
    expect(service.acknowledgeAgreementAsRepresentative).not.toHaveBeenCalled();

    component.agreementReviewConfirmed.set(true);
    component.acceptAgreement();
    expect(service.acknowledgeAgreementAsRepresentative).toHaveBeenCalledWith('document-1', 'child-1');
    expect(component.agreement()?.acknowledgedByCurrentRepresentative).toBe(true);

    component.withdrawAgreement();
    expect(service.withdrawRepresentativeAcknowledgement).toHaveBeenCalledWith('ack-1');
    expect(component.agreement()?.acknowledgedByCurrentRepresentative).toBe(false);
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
