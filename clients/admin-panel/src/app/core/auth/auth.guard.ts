import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);

    // SSR Check: Server has no access to localStorage/cookies easily in this setup.
    // Allow the initial render to pass, client-side hydration will handle real auth check.
    if (isPlatformServer(platformId)) {
        return true;
    }

    // Wait for auth service to initialize (Discovery loading etc.)
    // This prevents race conditions on page refresh.
    const isAuthenticated = await authService.waitForAuth();

    if (isAuthenticated) {
        return true;
    }

    // Redirect to login
    router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
};
