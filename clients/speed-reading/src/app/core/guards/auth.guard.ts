import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated) return allow(authService, router, route);

  // 18.11.2026: a session whose short-lived access token ran out (a page left open) is renewed with the refresh
  // cookie before falling back to the sign-in page, so "Panelime git" opens the panel directly.
  if (authService.currentUserValue) {
    return authService.refreshToken().pipe(
      map(() => authService.isAuthenticated ? allow(authService, router, route) : toLogin(router, state)),
      catchError(() => of(toLogin(router, state)))
    );
  }

  return toLogin(router, state);
};

function allow(authService: AuthService, router: Router, route: ActivatedRouteSnapshot): boolean {
  const requiredRole = route.data['role'];
  if (!requiredRole) return true;
  // Check if user has ANY of the required roles
  const roles: string[] = Array.isArray(requiredRole) ? requiredRole : [requiredRole as string];
  if (roles.some(role => authService.hasRole(role))) return true;
  router.navigate(['/error/403']);
  return false;
}

function toLogin(router: Router, state: RouterStateSnapshot): boolean {
  router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
  return false;
}
