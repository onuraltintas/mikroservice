import { signal } from '@angular/core';
import { of, Subject } from 'rxjs';
import { DashboardNewComponent } from './dashboard-new.component';

describe('student next program confirmation', () => {
  const suggestion = { templateId: 'next', sourceProgressId: 'completed', assessmentAttemptId: 'measured',
    templateName: 'Next program', totalDays: 28, requiresStaffApproval: false };

  function dashboard(managed = false) {
    return {
      nextProgramRecommendation: signal({ ...suggestion, requiresStaffApproval: managed }),
      nextProgramStarting: signal(false), nextProgramError: signal<string | null>(null), destroy$: new Subject<void>(),
      studentProgramService: { confirmNextProgram: jasmine.createSpy().and.returnValue(of({ success: true })) },
      loadDashboardStats: jasmine.createSpy()
    };
  }

  it('submits the displayed program and measurement references only after explicit confirmation', () => {
    const instance = dashboard();
    (DashboardNewComponent.prototype as any).confirmNextProgram.call(instance);
    expect(instance.studentProgramService.confirmNextProgram).toHaveBeenCalledWith({
      templateId: 'next', sourceProgressId: 'completed', assessmentAttemptId: 'measured'
    });
    expect(instance.loadDashboardStats).toHaveBeenCalledTimes(1);
    expect(instance.nextProgramStarting()).toBeFalse();
  });

  it('does not let a managed student bypass staff approval', () => {
    const instance = dashboard(true);
    (DashboardNewComponent.prototype as any).confirmNextProgram.call(instance);
    expect(instance.studentProgramService.confirmNextProgram).not.toHaveBeenCalled();
  });
});
