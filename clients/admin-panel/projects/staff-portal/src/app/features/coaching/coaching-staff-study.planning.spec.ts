import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { of, throwError } from 'rxjs';
import { CoachingStaffStudyComponent } from './coaching-staff-study.component';
import { CoachingPlanningPermissionService, PlanningPermission } from '../../../../../../src/app/core/services/coaching-planning-permission.service';
import { CoachingStudyPlanningService } from '../../../../../../src/app/core/services/coaching-study-planning.service';

// User decision 15.11.2026: the teacher decides whether the student makes her own plan; off by default.
@Component({ selector: 'staff-coaching-study', standalone: true, template: `
  @if (planning(); as permission) {
    <div data-testid="planning-permission">
      @if (permission.hasTeacher) {
        <input type="checkbox" [checked]="permission.allowedByTeacher" [disabled]="readOnly || planningSaving()" (change)="setPlanning($any($event.target).checked)">
      }
      @if (planningError()) { <p role="alert">{{ planningError() }}</p> }
    </div>
  }` })
class Harness extends CoachingStaffStudyComponent {}

describe('CoachingStaffStudyComponent planning permission', () => {
  const state = (allowedByTeacher: boolean): PlanningPermission => ({ hasTeacher: true, allowedByTeacher, studentMayPlan: allowedByTeacher });
  function mount(permissions: object, readOnly = false) {
    TestBed.configureTestingModule({ imports: [Harness], providers: [
      { provide: CoachingPlanningPermissionService, useValue: permissions },
      { provide: CoachingStudyPlanningService, useValue: { getAvailability: () => of(null) } }] });
    const fixture = TestBed.createComponent(Harness);
    fixture.componentInstance.studentId = 'student-1'; fixture.componentInstance.readOnly = readOnly;
    fixture.componentInstance.ngOnChanges(); fixture.detectChanges();
    return fixture;
  }
  const box = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.querySelector('input[type=checkbox]') as HTMLInputElement;

  it('starts closed and saves the teacher\'s choice', () => {
    const set = vi.fn(() => of(state(true)));
    const fixture = mount({ forStudent: () => of(state(false)), set });
    expect(box(fixture).checked).toBe(false);
    box(fixture).checked = true; box(fixture).dispatchEvent(new Event('change')); fixture.detectChanges();
    expect(set).toHaveBeenCalledWith('student-1', true);
    expect(fixture.componentInstance.planning()?.allowedByTeacher).toBe(true);
  });

  it('is read-only for an institution viewer', () => {
    const set = vi.fn();
    const fixture = mount({ forStudent: () => of(state(false)), set }, true);
    expect(box(fixture).disabled).toBe(true);
    fixture.componentInstance.setPlanning(true);
    expect(set).not.toHaveBeenCalled();
  });

  it('keeps the saved state and says so when saving fails', () => {
    const fixture = mount({ forStudent: () => of(state(false)), set: () => throwError(() => new Error('x')) });
    fixture.componentInstance.setPlanning(true); fixture.detectChanges();
    expect(fixture.componentInstance.planning()?.allowedByTeacher).toBe(false);
    expect(fixture.nativeElement.querySelector('[role=alert]')?.textContent).toContain('Ayar kaydedilemedi');
  });
});
