import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { StudentReportsComponent } from './student-reports.component';

describe('program report selection', () => {
  it('ignores delayed responses from a previously selected program', () => {
    const comparisons = [new Subject<any>(), new Subject<any>()];
    const plans = [new Subject<any>(), new Subject<any>()];
    const instance = Object.create(StudentReportsComponent.prototype) as any;
    Object.assign(instance, {
      selectedProgramId: signal(null), assessmentComparison: signal(null), phasePlan: signal(null),
      destroy$: new Subject<void>(), assessmentService: {
        getAttemptComparison: jasmine.createSpy().and.returnValues(...comparisons),
        getPhasePlan: jasmine.createSpy().and.returnValues(...plans)
      }
    });
    instance.selectProgram('old');
    instance.selectProgram('new');
    comparisons[1].next({ program: 'new' }); plans[1].next({ program: 'new' });
    comparisons[0].next({ program: 'old' }); plans[0].next({ program: 'old' });
    expect(instance.assessmentComparison().program).toBe('new');
    expect(instance.phasePlan().program).toBe('new');
  });
});
