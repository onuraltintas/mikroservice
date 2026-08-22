import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, hasRequiredRole } from './auth.service';

export const COACHING_PORTAL_ROLES = ['Student', 'Teacher', 'Parent'] as const;

export function hasCoachingPortalRole(user: { roles: string[] } | null): boolean {
    return !!user && COACHING_PORTAL_ROLES.some(role => user.roles.includes(role));
}

export function hasRequiredCoachingRole(
    user: { roles: string[] } | null,
    requiredRoles: readonly string[]
): boolean {
    return !!user && requiredRoles.some(role => user.roles.includes(role));
}

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

/**
 * Permission guard for management routes.  SSR is intentionally allowed to
 * render the shell; the browser performs the real token/permission check
 * after hydration, just like authGuard.
 */
export const permissionGuard: CanActivateFn = async (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);
    const requiredPermission = route.data?.['permission'] as string | undefined;
    const requiredRole = route.data?.['role'] as string | undefined;

    if ((!requiredPermission && !requiredRole) || isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }

    const profile = authService.userProfile();
    const hasPermission = !requiredPermission || authService.hasPermission(requiredPermission);
    const hasRole = !requiredRole || hasRequiredRole(profile, requiredRole);
    if (hasPermission && hasRole) {
        return true;
    }

    router.navigate(['/dashboard'], { queryParams: { forbidden: 'true' } });
    return false;
};

/**
 * Keeps management-only identities out of the student/teacher/parent portal.
 * The server remains the source of truth; this guard only controls navigation.
 */
export const coachingPortalGuard: CanActivateFn = async (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);

    if (isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        await router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }

    if (hasCoachingPortalRole(authService.userProfile())) {
        return true;
    }

    await router.navigate(['/dashboard'], { queryParams: { forbidden: 'true' } });
    return false;
};

/**
 * Restricts a child portal route to its declared coaching role allow-list.
 * API authorization remains authoritative; this guard prevents accidental
 * cross-panel navigation and keeps the UI contract explicit.
 */
export const coachingRoleGuard: CanActivateFn = async (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);
    const requiredRoles = route.data?.['coachingRoles'] as readonly string[] | undefined;

    if (!requiredRoles?.length || isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        await router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }

    if (hasRequiredCoachingRole(authService.userProfile(), requiredRoles)) {
        return true;
    }

    await router.navigate(['/dashboard'], { queryParams: { forbidden: 'true' } });
    return false;
};
