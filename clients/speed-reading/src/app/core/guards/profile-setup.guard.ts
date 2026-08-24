import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Guard that checks if user has completed their profile setup
 * If profile is incomplete, redirects to profile-setup page
 * If profile is complete, allows access to the route
 */
export const profileSetupGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Allow Admins and Teachers to bypass profile setup check
  if (authService.hasRole('Admin') || authService.hasRole('Teacher')) {
    return true;
  }

  // If user has not completed profile setup, redirect to profile-setup
  if (!authService.hasCompletedProfile()) {
    router.navigate(['/student/profile-setup']);
    return false;
  }

  return true;
};
