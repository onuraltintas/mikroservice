import { signal } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { DashboardNewComponent } from './dashboard-new.component';

describe('persisted program completion assessment action', () => {
  function dashboard(plan: any) {
    return {
      assessmentService: { getPhasePlan: jasmine.createSpy().and.returnValue(of(plan)) },
      destroy$: new Subject<void>(), nextAssessmentPhase: signal<number | null>(2),
      assessmentPlanError: signal(false), assessmentWaitUntil: signal<string | null>(null)
    };
  }

  it('uses the server next phase instead of repeating the completed post-training measurement', () => {
    const instance = dashboard({ nextPhase: 3, phases: [] });
    (DashboardNewComponent.prototype as any).loadCompletionAssessmentPlan.call(instance);
    expect(instance.nextAssessmentPhase()).toBe(3);
  });

  it('shows a waiting date instead of an unavailable measurement action', () => {
    const instance = dashboard({ nextPhase: null,
      phases: [{ phase: 3, status: 1, availableAt: '2026-10-08T00:00:00Z' }] });
    (DashboardNewComponent.prototype as any).loadCompletionAssessmentPlan.call(instance);
    expect(instance.nextAssessmentPhase()).toBeNull();
    expect(instance.assessmentWaitUntil()).toBe('2026-10-08T00:00:00Z');
  });

  it('does not offer a guessed phase when the phase plan cannot be loaded', () => {
    const instance = dashboard({});
    instance.assessmentService.getPhasePlan.and.returnValue(throwError(() => new Error('offline')));
    (DashboardNewComponent.prototype as any).loadCompletionAssessmentPlan.call(instance);
    expect(instance.nextAssessmentPhase()).toBeNull();
    expect(instance.assessmentPlanError()).toBeTrue();
  });
});
