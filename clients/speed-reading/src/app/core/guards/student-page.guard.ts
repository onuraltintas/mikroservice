import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

const previewPaths = new Set(['exercises', 'exercises/universal-player/:exerciseId']);

export const studentPageGuard: CanActivateChildFn = child => {
  const auth = inject(AuthService);
  if (auth.hasRole('Student') || previewPaths.has(child.routeConfig?.path ?? '')) {
    return true;
  }

  inject(Router).navigate(['/error/403']);
  return false;
};
