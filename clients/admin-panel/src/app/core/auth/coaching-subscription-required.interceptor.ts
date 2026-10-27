import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

const SUBSCRIPTION_PAGE = '/coaching-portal/subscription';

/**
 * Coaching answers 403 `CoachingSubscriptionRequired` when a student without an active subscription opens a learning
 * page. Instead of a bare error the student lands on the subscription page, which explains why. The error is still
 * passed on so the calling page stops its loading state.
 */
export const coachingSubscriptionRequiredInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);
  return next(request).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse
        && error.status === 403
        && (error.error as { code?: unknown } | null)?.code === 'CoachingSubscriptionRequired'
        && !router.url.startsWith(SUBSCRIPTION_PAGE)) {
      void router.navigate([SUBSCRIPTION_PAGE], { queryParams: { required: 1 } });
    }
    return throwError(() => error);
  }));
};
