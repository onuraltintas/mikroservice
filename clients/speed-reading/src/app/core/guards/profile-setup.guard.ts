import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Guard that checks if user has completed their profile setup
 * If profile is incomplete, redirects to profile-setup page
 * If profile is complete, allows access to the route
 */
export const profileSetupGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const http = inject(HttpClient);

  // Staff exercise previews do not require a student profile.
  if (authService.hasRole('Teacher') || authService.hasAdminAccess() || authService.hasRole('Editor')) {
    return true;
  }

  return http.get<{ hasAgeGroupConfiguration: boolean }>(
    `${environment.apiUrl}/speed-reading/adaptive-learning/profile/status`
  ).pipe(
    map(status => {
      if (status.hasAgeGroupConfiguration) return true;
      router.navigate(['/student/profile-setup']);
      return false;
    }),
    catchError(() => {
      router.navigate(['/error/500']);
      return of(false);
    })
  );
};
