import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError } from 'rxjs/operators';
import { of, switchMap, Observable } from 'rxjs';
import { SubscriptionService } from '../services/subscription.service';
import { AuthService } from '../services/auth.service';
import { AssessmentService } from '../../services/assessment.service';

/**
 * Öğrencinin aktif Speed Reading aboneliği olup olmadığını kontrol eder.
 * Teacher / Editor rolleri doğrudan geçer — sadece Student için abonelik zorunlu.
 * Abonelik yoksa /no-access sayfasına yönlendirir.
 *
 * Kullanım: canActivate: [authGuard, profileSetupGuard, subscriptionGuard]
 */
export const subscriptionGuard: CanActivateFn = (route, state) => {
  const authService         = inject(AuthService);
  const subscriptionService = inject(SubscriptionService);
  const assessmentService   = inject(AssessmentService);
  const router              = inject(Router);

  // Student dışındaki roller abonelik kontrolüne takılmaz
  const staffRoles = ['Admin', 'SystemAdmin', 'Teacher', 'Editor', 'Coach', 'InstitutionAdmin', 'InstitutionOwner'];
  if (staffRoles.some(r => authService.hasRole(r))) return of(true);

  // The server verifies that the attempt belongs to this student and exercise.
  const assessmentAttemptId = route.queryParamMap.get('assessmentAttemptId');
  if (state.url.startsWith('/student/exercises/universal-player/')
      && route.queryParamMap.get('assessmentMode') === 'true'
      && assessmentAttemptId
      && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(assessmentAttemptId)) {
    return of(true);
  }

  const subscriptionCheck = (): Observable<boolean> => subscriptionService.getMyModules().pipe(
    map(modules => {
      if (modules.hasSpeedReading) return true;
      router.navigate(['/no-access']);
      return false;
    }),
    catchError(() => {
      router.navigate(['/error/500']);
      return of(false);
    })
  );

  // The initial assessment is the free entry point. A student must be able
  // to finish it before the paid-module check is applied, including after a
  // fresh login or a direct visit to the dashboard URL.
  return assessmentService.getAssessmentStatus().pipe(
    switchMap((response: any) => {
      const data = response?.data
        || (response?.hasCompleted !== undefined ? response : null);
      if (data && data.hasCompleted === false) {
        router.navigate(['/student/assessment-intro'], {
          queryParams: { returnUrl: state.url }
        });
        return of(false);
      }
      return subscriptionCheck();
    }),
    catchError(() => subscriptionCheck())
  );
};
