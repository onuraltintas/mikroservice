import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated) {
    const requiredRole = route.data['role'];

    if (requiredRole) {
      if (Array.isArray(requiredRole)) {
        // Check if user has ANY of the required roles
        const hasAccess = requiredRole.some(role => authService.hasRole(role));
        if (!hasAccess) {
          router.navigate(['/error/403']);
          return false;
        }
      } else if (!authService.hasRole(requiredRole as string)) {
        router.navigate(['/error/403']);
        return false;
      }
    }
    return true;
  }

  router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
  return false;
};