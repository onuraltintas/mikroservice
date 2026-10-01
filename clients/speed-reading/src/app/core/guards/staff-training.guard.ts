import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export function canUseStaffTraining(auth: Pick<AuthService, 'hasRole'>): boolean {
  return !auth.hasRole('Student') && ['Admin', 'SystemAdmin', 'Teacher'].some(role => auth.hasRole(role));
}

export const staffTrainingGuard: CanActivateFn = () => canUseStaffTraining(inject(AuthService))
  || inject(Router).createUrlTree(['/error/403']);
