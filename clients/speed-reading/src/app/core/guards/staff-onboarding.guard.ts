import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const staffOnboardingGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  if (authService.hasAdminAccess() || authService.hasRole('Editor')) {
    return inject(Router).createUrlTree(['/student/exercises']);
  }
  return true;
};
