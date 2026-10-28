import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';
import { CoachingTeacherGoalsComponent } from './coaching-teacher-goals.component';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherAssignmentsService } from './coaching-teacher-assignments.service';
import { CoachingTeacherSessionsService } from './coaching-teacher-sessions.service';
import { CoachingTeacherGoalsService } from './coaching-teacher-goals.service';
import { CoachingTeacherExamsService } from './coaching-teacher-exams.service';
import { CoachingTeacherSubscriptionService } from './coaching-teacher-subscription.service';
import { CoachingTeacherWorkspaceComponent } from './coaching-teacher-workspace.component';
import { CoachingTeacherTrackingComponent } from './coaching-teacher-tracking.component';
import { CoachingTeacherTrackingService } from './coaching-teacher-tracking.service';

describe('CoachingTeacherWorkspaceComponent', () => {
  it('opens weekly notifications in tracking while retaining guarded sections',async()=>{
    await TestBed.configureTestingModule({imports:[CoachingTeacherWorkspaceComponent]}).compileComponents();const c=TestBed.createComponent(CoachingTeacherWorkspaceComponent).componentInstance;
    const target={studentId:'student',weekStart:'2026-10-12',decisionId:null};c.activeSection.set('finance');c.financePanel={canLeavePage:async () =>false};
    expect((await c.openWeeklyFromNotification(target))).toBe(false);expect(c.activeSection()).toBe('finance');
    c.financePanel={canLeavePage:async () =>true};expect((await c.openWeeklyFromNotification(target))).toBe(true);expect(c.activeSection()).toBe('tracking');expect(c.notificationWeeklyTarget()).toEqual(target);
  });
  it('keeps a guarded section and opens the exact notification assignment once it is safe',async()=>{
    await TestBed.configureTestingModule({imports:[CoachingTeacherWorkspaceComponent]}).compileComponents();
    const component=TestBed.createComponent(CoachingTeacherWorkspaceComponent).componentInstance;
    component.activeSection.set('finance');component.financePanel={canLeavePage:async () =>false};
    expect((await component.openAssignmentFromNotification('assignment-id'))).toBe(false);
    expect(component.activeSection()).toBe('finance');expect(component.notificationAssignmentId()).toBeNull();
    component.financePanel={canLeavePage:async () =>true};
    expect((await component.openAssignmentFromNotification('assignment-id'))).toBe(true);
    expect(component.activeSection()).toBe('assignments');expect(component.notificationAssignmentId()).toBe('assignment-id');
    (await component.selectSection('students'));(await component.selectSection('assignments'));
    expect(component.notificationAssignmentId()).toBeNull();
  });
  it('retains the finance section while its operation is pending', async () => {
    await TestBed.configureTestingModule({ imports: [CoachingTeacherWorkspaceComponent] }).compileComponents();
    const component = TestBed.createComponent(CoachingTeacherWorkspaceComponent).componentInstance;
    component.activeSection.set('finance'); component.financePanel = { canLeavePage: async () => false };
    (await component.selectSection('students')); expect(component.activeSection()).toBe('finance'); expect((await component.canLeavePage())).toBe(false);
  });
  it('switches between student reports and teacher assignments in the Coaching workspace', async () => {
    const assignments = {
      getTeacherAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
      cancelAssignment: vi.fn(() => of({ message: 'ok' }))
    };
    await TestBed.configureTestingModule({
      imports: [CoachingTeacherWorkspaceComponent],
      providers: [
        { provide: CoachingTeacherTrackingService, useValue: { get: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, overdueStudyCount: 0, overdueAssignmentCount: 0, awaitingReviewCount: 0 })) } },
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
          profiles: vi.fn(() => of([])),
          getTeacherExams: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1 })),
          createExam: vi.fn(() => of({ examId: 'exam-1' })),
          updateExam: vi.fn(() => of({ examId: 'exam-1', examDate: '2030-01-01T00:00:00Z', maxScore: 100 })),
          getExamDetail: vi.fn(() => of({ id: 'exam-1', results: [], resultPageNumber: 1, resultPageSize: 25, resultTotalPages: 1 })),
          addExamResult: vi.fn(() => of({ message: 'ok' })),
          updateExamResult: vi.fn(() => of({ examId: 'exam-1', resultId: 'result-1', score: 90 }))
        } },
        { provide: CoachingTeacherSubscriptionService, useValue: {
          getTeacherPlans: vi.fn(() => of([])),
          getBankTransferSettings: vi.fn(() => of(null)),
          getMyBankTransferRequests: vi.fn(() => of([])),
          getMySeatSummary: vi.fn(() => of(null)),
          assignStudent: vi.fn(() => of(void 0)),
          removeStudent: vi.fn(() => of(void 0))
        } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(CoachingTeacherWorkspaceComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.activeSection()).toBe('tracking');
    await fixture.componentInstance.selectSection('students');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrencilerim');
    expect(assignments.getTeacherAssignments).not.toHaveBeenCalled();

    const roster = fixture.debugElement.query(By.directive(CoachingTeacherStudentsComponent)).componentInstance;
    const guard = vi.spyOn(roster, 'canLeavePage').mockReturnValue(false);
    await fixture.componentInstance.selectSection('assignments');
    fixture.detectChanges();
    expect(fixture.componentInstance.activeSection()).toBe('students');
    expect(assignments.getTeacherAssignments).not.toHaveBeenCalled();
    guard.mockReturnValue(true);

    await fixture.componentInstance.selectSection('assignments');
    fixture.detectChanges();

    expect(assignments.getTeacherAssignments).toHaveBeenCalledWith('teacher-1', 1, 25, undefined);
    expect(fixture.nativeElement.textContent).toContain('Ödevlerim');

    await fixture.componentInstance.selectSection('sessions');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seanslarım');

    await fixture.componentInstance.selectSection('goals');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Hedeflerim');
    const goals = fixture.debugElement.query(By.directive(CoachingTeacherGoalsComponent)).componentInstance;
    const goalGuard = vi.spyOn(goals, 'canLeavePage').mockReturnValue(false);
    (await fixture.componentInstance.selectSection('exams'));
    expect(fixture.componentInstance.activeSection()).toBe('goals');
    goalGuard.mockReturnValue(true);

    await fixture.componentInstance.selectSection('exams');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sınavlarım');

    const subscriptionTab = fixture.nativeElement.querySelector('[data-testid="teacher-subscription-tab"]') as HTMLButtonElement;
    expect(subscriptionTab).toBeTruthy();
    await fixture.componentInstance.selectSection('subscription');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Öğretmen planları');
    await fixture.componentInstance.selectSection('tracking');
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('İşlem bekleyen çalışmaları');
    const tracking = fixture.debugElement.query(By.directive(CoachingTeacherTrackingComponent)).componentInstance;
    const trackingGuard = vi.spyOn(tracking, 'canLeavePage').mockReturnValue(false);
    (await fixture.componentInstance.selectSection('assignments')); expect(fixture.componentInstance.activeSection()).toBe('tracking');
    expect((await fixture.componentInstance.openAssignmentFromNotification('exact-assignment'))).toBe(false);
    trackingGuard.mockReturnValue(true); expect((await fixture.componentInstance.openAssignmentFromNotification('exact-assignment'))).toBe(true);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
