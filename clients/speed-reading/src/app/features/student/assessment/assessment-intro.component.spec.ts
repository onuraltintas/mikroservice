import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { AssessmentService } from '../../../services/assessment.service';
import { AssessmentIntroComponent } from './assessment-intro.component';

describe('AssessmentIntroComponent', () => {
  it('keeps the student on the assessment page when skipping fails', () => {
    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const assessment = jasmine.createSpyObj<AssessmentService>('AssessmentService', ['skipAssessment']);
    assessment.skipAssessment.and.returnValue(throwError(() => new Error('No baseline program')));
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: AssessmentService, useValue: assessment }
      ]
    });

    const component = TestBed.runInInjectionContext(() => new AssessmentIntroComponent());
    component.skipAssessment();

    expect(component.loading).toBe(false);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
