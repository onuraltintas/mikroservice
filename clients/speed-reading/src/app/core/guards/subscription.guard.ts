import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { SubscriptionService } from '../services/subscription.service';
import { AuthService } from '../services/auth.service';

/**
 * Öğrencinin aktif Speed Reading aboneliği olup olmadığını kontrol eder.
 * Admin / Teacher / Editor rolleri doğrudan geçer — sadece Student için abonelik zorunlu.
 * Abonelik yoksa /no-access sayfasına yönlendirir.
 *
 * Kullanım: canActivate: [authGuard, profileSetupGuard, subscriptionGuard]
 */
export const subscriptionGuard: CanActivateFn = () => {
  const authService         = inject(AuthService);
  const subscriptionService = inject(SubscriptionService);
  const router              = inject(Router);

  // Student dışındaki roller abonelik kontrolüne takılmaz
  const studentOnly = ['Admin', 'Teacher', 'Editor', 'Coach', 'InstitutionAdmin'];
  if (studentOnly.some(r => authService.hasRole(r))) return of(true);

  return subscriptionService.getMyModules().pipe(
    map(modules => {
      if (modules.hasSpeedReading) return true;
      router.navigate(['/no-access']);
      return false;
    }),
    catchError(() => of(true)) // API hatasında engelleme
  );
};
