import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AssignmentDto, AssignmentService } from '../../../core/services/assignment.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { StudentAssignmentsPageComponent } from './student-assignments-page.component';

describe('StudentAssignmentsPageComponent', () => {
  function create(): StudentAssignmentsPageComponent {
    TestBed.configureTestingModule({
      providers: [
        { provide: AssignmentService, useValue: {} },
        { provide: Router, useValue: {} },
        { provide: ToasterService, useValue: {} }
      ]
    });
    return TestBed.runInInjectionContext(() => new StudentAssignmentsPageComponent());
  }

  it('averages only completed assignments with a recorded score', () => {
    const component = create();
    component.assignments = [
      { isCompleted: true, score: 80 },
      { isCompleted: true, score: 100 },
      { isCompleted: true },
      { isCompleted: false, score: 0 }
    ] as AssignmentDto[];

    component.calculateStats();

    expect(component.stats.averageScore).toBe(90);
  });

  it('does not present missing scores as zero achievement', () => {
    const component = create();
    component.assignments = [{ isCompleted: true }] as AssignmentDto[];

    component.calculateStats();

    expect(component.stats.averageScore).toBeNull();
  });
});
