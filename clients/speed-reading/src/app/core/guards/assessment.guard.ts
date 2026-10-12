import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AssessmentService } from '../../services/assessment.service';
import { AuthService } from '../services/auth.service';
import { ToasterService } from '../services/toaster.service';
import { map, catchError, of } from 'rxjs';

/** Initial placement is not a repeat assessment during an assigned training cycle. */
export const assessmentEntryGuard: CanActivateFn = (route) => {
  const phase = Number(route.queryParamMap.get('phase'));
  // Historical post-training/retention/transfer eligibility is checked by the API.
  if (Number.isInteger(phase) && phase >= 2 && phase <= 4) return true;
  const assessment = inject(AssessmentService);
  const router = inject(Router);
  const toaster = inject(ToasterService);
  return assessment.getPhasePlan().pipe(
    map(plan => {
      const baseline = plan.phases.find(item => item.phase === 1);
      if (baseline?.status === 2 || baseline?.status === 3) return true;
      toaster.info('Başlangıç seviye tespiti şu anda kapalı. Program sonunda eğitim sonrası ölçümünü yapabilirsiniz.');
      router.navigate(['/student/dashboard']);
      return false;
    }),
    catchError(() => {
      toaster.error('Ölçüm durumu kontrol edilemedi. Lütfen yeniden deneyin.');
      router.navigate(['/student/dashboard']);
      return of(false);
    })
  );
};

/**
 * Guard that checks if user has completed assessment
 * Redirects to assessment-intro if not completed
 */
export const assessmentGuard: CanActivateFn = (route, state) => {
  const assessmentService = inject(AssessmentService);
  const router = inject(Router);

  const authService = inject(AuthService);

  // Staff exercise previews do not require a student assessment.
  if (!authService.hasRole('Student')
      && (authService.hasRole('Teacher') || authService.hasAdminAccess() || authService.hasRole('Editor'))) {
    return true;
  }

  // Check if user has completed assessment
  return assessmentService.getAssessmentStatus().pipe(
    map((response: any) => {
      // Handle both wrapped (ApiResponse) and flat responses
      const data = response.data || (response.hasCompleted !== undefined ? response : null);
      const success = response.success || !!data;

      if (success && data && data.hasCompleted) {
        return true; // Assessment completed, allow access
      }

      // Assessment not completed, redirect to intro
      router.navigate(['/student/assessment-intro'], {
        queryParams: { returnUrl: state.url }
      });
      return false;
    }),
    catchError(error => {
      console.error('Error checking assessment status:', error);
      router.navigate(['/error/500'], { queryParams: { returnUrl: state.url } });
      return of(false);
    })
  );
};

/**
 * Guard that prevents access to assessment intro if already completed
 * Redirects to dashboard if assessment is completed
 */
export const assessmentCompletedGuard: CanActivateFn = (route, state) => {
  const assessmentService = inject(AssessmentService);
  const router = inject(Router);

  return assessmentService.getAssessmentStatus().pipe(
    map((response: any) => {
      // Handle both wrapped (ApiResponse) and flat responses
      const data = response.data || (response.hasCompleted !== undefined ? response : null);
      const success = response.success || !!data;

      if (success && data && data.hasCompleted) {
        // Already completed, redirect to dashboard
        router.navigate(['/student/dashboard']);
        return false;
      }
      return true; // Not completed, allow access to intro
    }),
    catchError(error => {
      console.error('Error checking assessment status:', error);
      // On error, allow access to intro
      return of(true);
    })
  );
};
