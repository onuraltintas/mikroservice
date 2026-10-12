import { signal } from '@angular/core';
import { of, Subject } from 'rxjs';
import { DashboardNewComponent } from './dashboard-new.component';

describe('program continuation choices and historical followups', () => {
  function dashboard(managed = false) {
    return {
      nextProgramRecommendation: signal({ templateId: 'normal', sourceProgressId: 'old', assessmentAttemptId: 'post',
        requiresStaffApproval: managed, higherOption: { templateId: 'upper', level: 4 } }),
      overrideAcknowledged: false, nextProgramStarting: signal(false), nextProgramError: signal<string | null>(null),
      destroy$: new Subject<void>(), studentProgramService: {
        confirmNextProgram: jasmine.createSpy().and.returnValue(of({ success: true })),
        requestNextProgram: jasmine.createSpy().and.returnValue(of({ success: true }))
      }, loadDashboardStats: jasmine.createSpy(), loadNextProgramRecommendation: jasmine.createSpy(),
      router: { navigate: jasmine.createSpy() }
    };
  }

  it('requires an explicit warning acknowledgement for the higher choice', () => {
    const instance = dashboard();
    (DashboardNewComponent.prototype as any).confirmNextProgram.call(instance, true);
    expect(instance.studentProgramService.confirmNextProgram).not.toHaveBeenCalled();
    instance.overrideAcknowledged = true;
    (DashboardNewComponent.prototype as any).confirmNextProgram.call(instance, true);
    expect(instance.studentProgramService.confirmNextProgram).toHaveBeenCalledWith({
      templateId: 'upper', sourceProgressId: 'old', assessmentAttemptId: 'post', overrideAcknowledged: true
    });
  });

  it('sends a managed students choice for approval instead of starting it', () => {
    const instance = dashboard(true);
    instance.overrideAcknowledged = true;
    (DashboardNewComponent.prototype as any).requestNextProgram.call(instance, true);
    expect(instance.studentProgramService.requestNextProgram).toHaveBeenCalledWith({
      templateId: 'upper', sourceProgressId: 'old', assessmentAttemptId: 'post', overrideAcknowledged: true
    });
    expect(instance.studentProgramService.confirmNextProgram).not.toHaveBeenCalled();
  });

  it('links a followup to the completed program rather than the active program', () => {
    const instance = dashboard();
    (DashboardNewComponent.prototype as any).startFollowUp.call(instance, { programProgressId: 'old', plan: { nextPhase: 3 } });
    expect(instance.router.navigate).toHaveBeenCalledWith(['/student/assessment'], { queryParams: { phase: 3, programProgressId: 'old' } });
  });
});
