import { inject, PLATFORM_ID } from '@angular/core';
import { DOCUMENT, isPlatformServer } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, hasManagementRole, hasRequiredRole, hasRole } from './auth.service';
import { EDUIVME_SITE_URLS, EduivmeSite, siteForHost } from '../../../../shared/eduivme-sites';

// Parents have a read-only panel of their linked children (roadmap Y7).
export const COACHING_PORTAL_ROLES = ['Student', 'Teacher', 'Parent'] as const;

export function hasCoachingPortalRole(user: { roles: string[]; product?: string } | null): boolean {
    return user?.product === 'coaching' && COACHING_PORTAL_ROLES.some(role => user.roles.includes(role));
}

export function hasRequiredCoachingRole(
    user: { roles: string[]; product?: string } | null,
    requiredRoles: readonly string[]
): boolean {
    return user?.product === 'coaching' && requiredRoles.some(role => user.roles.includes(role));
}

/**
 * Alan adı planı (18.11.2026): the addresses an account signs in on. Each group has its own host, so the sessions of
 * different accounts stay apart in one browser.
 */
export function accountSites(user: { roles: string[]; product?: string } | null): EduivmeSite[] {
    const roles = user?.roles ?? [];
    const sites: EduivmeSite[] = [];
    if (user?.product === 'coaching') {
        if (roles.includes('Student')) sites.push('student');
        if (roles.includes('Parent')) sites.push('parent');
        if (roles.includes('Teacher')) sites.push('teacher');
        if (roles.includes('InstitutionAdmin') || roles.includes('InstitutionOwner')) sites.push('institution');
    }
    if (hasManagementRole(user)) sites.push('admin');
    return sites;
}

/** The site of the page's host; null on any other host (localhost, a test host), where every account may stay. */
export function currentSite(): EduivmeSite | null {
    return siteForHost(inject(DOCUMENT).defaultView?.location?.hostname);
}

/**
 * Canlı test B2 (18.11.2026): a sign-up page opens on its group's own address, so the new account signs in where it
 * belongs (the parent sign-up on veli.maxkoc.com, the teacher sign-up on ogretmen.eduivme.com, …). On any other host
 * (localhost) the page stays.
 */
export function signUpSiteGuard(target: EduivmeSite): CanActivateFn {
    return (_route, state) => {
        if (isPlatformServer(inject(PLATFORM_ID))) return true;
        const site = currentSite();
        if (site === null || site === target) return true;
        inject(DOCUMENT).defaultView?.location.assign(EDUIVME_SITE_URLS[target] + state.url);
        return false;
    };
}

/** True when the account belongs on this host; otherwise the "other address" page opens. */
async function staysOnThisSite(authService: AuthService, router: Router, site: EduivmeSite | null): Promise<boolean> {
    if (site === null || accountSites(authService.userProfile()).includes(site)) return true;
    await router.navigate(['/other-site']);
    return false;
}

export const authGuard: CanActivateFn = async (_route, state) => {
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
 * Paket F F4 (live 07.11.2026): the /dashboard management shell needs a management role. A Koçluk student,
 * teacher or parent goes to their own panel; any other session (no role at all) is ended and the login page says why.
 * Runs after authGuard, which handles a missing session.
 */
export const managementShellGuard: CanActivateFn = async () => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);
    const site = currentSite();

    if (isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        return false;
    }

    if (hasManagementRole(authService.userProfile())) {
        return staysOnThisSite(authService, router, site);
    }

    // Only a Koçluk-scoped portal role may be sent on; anything else would bounce back here.
    const landing = authService.getLandingRoute();
    if (landing && hasCoachingPortalRole(authService.userProfile())) {
        return site === 'admin' ? router.parseUrl('/other-site') : router.parseUrl(landing);
    }

    await authService.endRoleLessSession();
    return router.parseUrl('/auth/login');
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
    const requiredPermissions = route.data?.['permissions'] as readonly string[] | undefined;
    const requiredRole = route.data?.['role'] as string | undefined;

    if ((!requiredPermission && !requiredRole) || isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }

    const profile = authService.userProfile();
    const hasPermission = requiredPermissions?.length
        ? requiredPermissions.some(permission => authService.hasPermission(permission))
        : !requiredPermission || authService.hasPermission(requiredPermission);
    const hasRole = !requiredRole || hasRequiredRole(profile, requiredRole);
    if (hasPermission && hasRole) {
        return true;
    }

    router.navigate(['/dashboard'], { queryParams: { forbidden: 'true' } });
    return false;
};

/**
 * Keeps identities without a student or teacher workspace out of the Coaching portal.
 * The server remains the source of truth; this guard only controls navigation.
 */
export const coachingPortalGuard: CanActivateFn = async (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const platformId = inject(PLATFORM_ID);
    const site = currentSite();

    if (isPlatformServer(platformId)) {
        return true;
    }

    if (!(await authService.waitForAuth())) {
        await router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }

    if (hasCoachingPortalRole(authService.userProfile())) {
        return staysOnThisSite(authService, router, site);
    }

    // Canlı test B4: an account that belongs on this address (a manager on admin.eduivme.com) goes back to its
    // panel as before; only an account of another address sees where it signs in.
    const sites = accountSites(authService.userProfile());
    if (site !== null && sites.length && !sites.includes(site)) {
        await router.navigate(['/other-site']);
        return false;
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

    if (authService.userProfile()?.product === 'coaching' && hasRole(authService.userProfile(), 'Teacher')) {
        await router.navigate(['/coaching-portal/teacher']);
        return false;
    }

    if (authService.userProfile()?.product === 'coaching' && hasRole(authService.userProfile(), 'Parent')) {
        await router.navigate(['/coaching-portal/parent']);
        return false;
    }

    await router.navigate(['/dashboard'], { queryParams: { forbidden: 'true' } });
    return false;
};
