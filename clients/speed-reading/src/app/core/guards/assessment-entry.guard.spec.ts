import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { AssessmentService } from '../../services/assessment.service';
import { ToasterService } from '../services/toaster.service';
import { assessmentEntryGuard } from './assessment.guard';

describe('assessment entry', () => {
  let assessment: jasmine.SpyObj<AssessmentService>;
  let router: jasmine.SpyObj<Router>;
  let toaster: jasmine.SpyObj<ToasterService>;
  const state = { url: '/student/assessment' } as RouterStateSnapshot;
  const route = (phase: string | null) => ({ queryParamMap: { get: () => phase } }) as unknown as ActivatedRouteSnapshot;

  beforeEach(() => {
    assessment = jasmine.createSpyObj('AssessmentService', ['getPhasePlan']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    toaster = jasmine.createSpyObj('ToasterService', ['info', 'error']);
    TestBed.configureTestingModule({ providers: [
      { provide: AssessmentService, useValue: assessment },
      { provide: Router, useValue: router },
      { provide: ToasterService, useValue: toaster }
    ] });
  });

  for (const status of [1, 4]) {
    it(`redirects unavailable baseline status ${status} without entering the form`, async () => {
      assessment.getPhasePlan.and.returnValue(of({ phases: [{ phase: 1, status, formVersion: 'baseline' }] }));
      const result = TestBed.runInInjectionContext(() => assessmentEntryGuard(route(null), state));
      expect(await firstValueFrom(result as Observable<boolean>)).toBeFalse();
      expect(router.navigate).toHaveBeenCalledWith(['/student/dashboard']);
      expect(toaster.info).toHaveBeenCalled();
    });
  }

  for (const status of [2, 3]) {
    it(`allows first or unfinished baseline status ${status} before training`, async () => {
      assessment.getPhasePlan.and.returnValue(of({ phases: [{ phase: 1, status, formVersion: 'baseline' }] }));
      const result = TestBed.runInInjectionContext(() => assessmentEntryGuard(route('1'), state));
      expect(await firstValueFrom(result as Observable<boolean>)).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  }

  for (const phase of ['2', '3', '4']) {
    it(`does not apply the baseline restriction to followup phase ${phase}`, () => {
      expect(TestBed.runInInjectionContext(() => assessmentEntryGuard(route(phase), state))).toBeTrue();
      expect(assessment.getPhasePlan).not.toHaveBeenCalled();
    });
  }

  it('does not enter the baseline when its availability cannot be checked', async () => {
    assessment.getPhasePlan.and.returnValue(throwError(() => new Error('offline')));
    const result = TestBed.runInInjectionContext(() => assessmentEntryGuard(route(null), state));
    expect(await firstValueFrom(result as Observable<boolean>)).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/student/dashboard']);
    expect(toaster.error).toHaveBeenCalled();
  });
});
