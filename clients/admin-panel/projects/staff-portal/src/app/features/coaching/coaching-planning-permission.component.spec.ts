import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CoachingPlanningPermissionService, PlanningPermission } from '../../../../../../src/app/core/services/coaching-planning-permission.service';
import { CoachingPlanningPermissionComponent } from './coaching-planning-permission.component';

describe('the planning switch next to the plan (live test 16.11.2026, #26)', () => {
  const state = (allowed: boolean): PlanningPermission => ({ hasTeacher: true, allowedByTeacher: allowed, studentMayPlan: allowed });
  function make(api: Partial<CoachingPlanningPermissionService>) {
    TestBed.configureTestingModule({ providers: [{ provide: CoachingPlanningPermissionService, useValue: api }] });
    const fixture = TestBed.createComponent(CoachingPlanningPermissionComponent);
    fixture.componentRef.setInput('studentId', 'student-1'); fixture.detectChanges();
    return fixture;
  }

  it('shows the saved choice and saves a new one', () => {
    const set = vi.fn().mockReturnValue(of(state(true)));
    const fixture = make({ forStudent: () => of(state(false)), set });
    const box: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="plan-page-planning-permission"] input');
    expect(box.checked).toBe(false);
    fixture.componentInstance.set(true); fixture.detectChanges();
    expect(set).toHaveBeenCalledWith('student-1', true);
    expect(fixture.componentInstance.planning()?.allowedByTeacher).toBe(true);
  });

  it('keeps the saved choice and says so when saving fails', () => {
    const fixture = make({ forStudent: () => of(state(false)), set: () => throwError(() => new Error('403')) });
    fixture.componentInstance.set(true); fixture.detectChanges();
    expect(fixture.componentInstance.planning()?.allowedByTeacher).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Ayar kaydedilemedi');
  });
});
