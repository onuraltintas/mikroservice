import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AssessmentService } from '../../services/assessment.service';
import { AuthService } from '../services/auth.service';
import { map, catchError, of } from 'rxjs';

/**
 * Guard that checks if user has completed assessment
 * Redirects to assessment-intro if not completed
 */
export const assessmentGuard: CanActivateFn = (route, state) => {
  const assessmentService = inject(AssessmentService);
  const router = inject(Router);

  const authService = inject(AuthService);

  // Allow Admins and Teachers to bypass assessment check
  if (authService.hasAdminAccess() || authService.hasRole('Teacher')) {
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
      // On error, allow access (fail open)
      return of(true);
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
