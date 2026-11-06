import { isPlatformServer } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import { CoachingOnboardingService } from '../../../core/services/coaching-onboarding.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';

export const ONBOARDING_URL = '/coaching-portal/welcome';
/** KVKK rights (Kişisel verilerim) stay reachable while the wizard is open. */
const ALWAYS_OPEN = ['/coaching-portal/account/data'];

/**
 * Paket C (#3): a coaching student who has not finished the first-login wizard sees nothing else of the panel. Students who
 * finished it (or the old setup, see migration StudentOnboarding) pass; parents and teachers are not affected. If the status
 * cannot be read the panel stays open: the wizard is a guide, the server still checks every request.
 */
export const studentOnboardingGuard: CanActivateChildFn = async (_route, state) => {
  if (isPlatformServer(inject(PLATFORM_ID))) return true;
  const views = inject(CoachingPortalViewService);
  const profile = inject(AuthService).userProfile();
  const onboarding = inject(CoachingOnboardingService);
  const router = inject(Router);
  if (views.current() !== 'Student' || !hasRole(profile, 'Student') || ALWAYS_OPEN.some(path => state.url.startsWith(path))) return true;
  try {
    return await firstValueFrom(onboarding.currentStatus()) === 'Completed' ? true : router.createUrlTree([ONBOARDING_URL]);
  } catch { return true; }
};
