import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { canUseStaffTraining } from './staff-training.guard';

const previewPaths = new Set(['exercises', 'exercises/universal-player/:exerciseId']);

export const studentPageGuard: CanActivateChildFn = child => {
  const auth = inject(AuthService);
  if (canUseStaffTraining(auth) && child.routeConfig?.path === 'dashboard') {
    return inject(Router).createUrlTree(['/student/training-programs']);
  }
  if (canUseStaffTraining(auth) && ['training-programs', 'daily-exercises'].includes(child.routeConfig?.path ?? '')) {
    return true;
  }
  if (auth.hasRole('Student') || previewPaths.has(child.routeConfig?.path ?? '')) {
    return true;
  }

  inject(Router).navigate(['/error/403']);
  return false;
};
