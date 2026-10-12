import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AssessmentService } from '../../../services/assessment.service';
import { AssessmentComponent } from './assessment.component';

describe('assessment training gate race', () => {
  it('does not misreport a newly assigned program as unfinished exercises', () => {
    const router = jasmine.createSpyObj('Router', ['navigate']);
    const toaster = jasmine.createSpyObj('ToasterService', ['error']);
    const assessment = { getRefreshTrigger: () => signal(0), calculateLevel: () => throwError(() =>
      ({ status: 400, error: { code: 'Assessment.BaselineDuringTraining' } })) };
    TestBed.configureTestingModule({ providers: [
      { provide: AssessmentService, useValue: assessment },
      { provide: AuthService, useValue: {} },
      { provide: ActivatedRoute, useValue: {} },
      { provide: Router, useValue: router },
      { provide: ToasterService, useValue: toaster }
    ] });
    const component = TestBed.runInInjectionContext(() => new AssessmentComponent());
    component.calculateLevel();
    expect(component.calculating()).toBeFalse();
    expect(component.showResult()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/student/dashboard']);
    expect(toaster.error).not.toHaveBeenCalled();
  });
});
