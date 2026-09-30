import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, convertToParamMap } from '@angular/router';
import { firstValueFrom, Observable, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { SubscriptionService } from '../services/subscription.service';
import { AssessmentService } from '../../services/assessment.service';
import { subscriptionGuard } from './subscription.guard';

describe('student subscription persona', () => {
  it('requires student module access for a dual teacher/student in the student portal', async () => {
    const router = { navigate: jasmine.createSpy() };
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: { hasRole: (role: string) => ['Student', 'Teacher'].includes(role) } },
      { provide: SubscriptionService, useValue: { getMyModules: () => of({ hasSpeedReading: false }) } },
      { provide: AssessmentService, useValue: { getAssessmentStatus: () => of({ hasCompleted: true }) } },
      { provide: Router, useValue: router }
    ] });
    const route = { queryParamMap: convertToParamMap({}) } as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => subscriptionGuard(route,
      { url: '/student/daily-exercises' } as RouterStateSnapshot));
    expect(await firstValueFrom(result as Observable<boolean>)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/no-access']);
  });
});
