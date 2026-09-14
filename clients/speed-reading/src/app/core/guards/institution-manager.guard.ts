import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const isInstitutionManager = (roles: readonly string[] = []): boolean =>
  roles.some(role => role === 'InstitutionAdmin' || role === 'InstitutionOwner');

export const institutionManagerGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (isInstitutionManager(authService.currentUserValue?.roles)) {
    return true;
  }

  router.navigate(['/error/403']);
  return false;
};
