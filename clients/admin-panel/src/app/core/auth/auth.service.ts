import { Injectable, inject, signal, PLATFORM_ID, OnDestroy } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { isPlatformBrowser } from '@angular/common';
import { OAuthService } from 'angular-oauth2-oidc';
import { authConfig } from './auth.config';
import { Router } from '@angular/router';
import { BehaviorSubject, filter, firstValueFrom, Subscription } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { RegistrationLegalAcceptance } from '../services/identity.service';
import { getApiErrorCode } from '../http/api-error-message';
import { SiteAnalyticsService } from '../services/site-analytics.service';
import { ADMIN_PERMISSIONS } from './permissions';

export interface UserProfile {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    username: string;
    roles: string[];
    role: string;
    permissions: string[];
    product?: string;
    mfaVerified?: boolean;
}

interface AuthSessionResponse {
    accessToken: string;
    tokenType: string;
    expiresInMinutes: number;
}

export interface AuthLoginResult {
    requiresLegalAcceptance?: boolean;
    registrationToken?: string;
    authenticated: boolean;
    requiresMfa: boolean;
    mfaEnrollmentRequired: boolean;
    mfaChallengeToken: string | null;
}

interface AuthLoginResponse extends Partial<AuthSessionResponse> {
    requiresLegalAcceptance?: boolean;
    registrationToken?: string;
    requiresMfa?: boolean;
    mfaEnrollmentRequired?: boolean;
    mfaChallengeToken?: string | null;
}

export interface MfaSetupResponse {
    secret: string;
    otpAuthUri: string;
    setupToken: string;
    challengeToken?: string;
}

interface MfaSessionResponse extends AuthSessionResponse {
    recoveryCodes?: string[] | null;
}

export function hasRequiredPermission(user: UserProfile | null, permission: string): boolean {
    return !!user && user.permissions.includes(permission);
}

export function hasRequiredRole(user: UserProfile | null, role: string): boolean {
    return !!user && user.roles.includes(role);
}

export function hasRole(user: UserProfile | null, role: string): boolean {
    return !!user && (user.role === role || user.roles.includes(role));
}

/**
 * Paket F F4 (PR #125 review M2): the /dashboard management shell opens for an explicit staff allowlist, or for a
 * session that carries at least one admin permission claim (a custom staff role). Nothing is allowed by exclusion:
 * a portal role, a course-management role or a custom role without such a permission does not open it.
 */
export const MANAGEMENT_ROLES: readonly string[] = ['SystemAdmin', 'Editor', 'InstitutionAdmin', 'InstitutionOwner'];

/** Admin-panel permission claims, minus the ones the identity seeder also gives to students, teachers and parents. */
const LEARNER_PERMISSIONS: readonly string[] = [
    ADMIN_PERMISSIONS.speedReadingLeaderboardView,
    ADMIN_PERMISSIONS.speedReadingReportView
];
export const MANAGEMENT_PERMISSIONS: readonly string[] = Object.values(ADMIN_PERMISSIONS)
    .filter(permission => !LEARNER_PERMISSIONS.includes(permission));

export function hasManagementRole(
    user: { roles: readonly string[]; permissions?: readonly string[] } | null
): boolean {
    return !!user && (
        user.roles.some(role => MANAGEMENT_ROLES.includes(role))
        || (user.permissions ?? []).some(permission => MANAGEMENT_PERMISSIONS.includes(permission)));
}

/** The identity service's code for an account with product access but no role in that product. */
export const NO_ROLE_FOR_PRODUCT_CODE = 'Auth.NoRoleForProduct';

/** A login-page notice in the identity service's error shape. */
export interface SignInNotice {
    status: number;
    error: { code: string; description: string };
}

/** The same error shape and status as the identity service, for a session that reached the client without a role. */
export function noRoleForProductError(): SignInNotice {
    return {
        status: 400,
        error: {
            code: NO_ROLE_FOR_PRODUCT_CODE,
            description: 'Bu platformda hesabınıza tanımlı bir yetki bulunmuyor. Yeniden erişim için kurumunuzla ya da destekle iletişime geçin.'
        }
    };
}

export function hasRequiredAccess(
    user: UserProfile | null,
    permission: string,
    requiredRole?: string
): boolean {
    return hasRequiredPermission(user, permission)
        && (!requiredRole || hasRequiredRole(user, requiredRole));
}

/** Client-only code (F4, re-review L-d): the account has roles, but none this panel serves. */
export const SESSION_NOT_FOR_THIS_PANEL_CODE = 'Auth.SessionNotForThisPanel';

export function sessionNotForThisPanelError(): SignInNotice {
    return {
        status: 400,
        error: {
            code: SESSION_NOT_FOR_THIS_PANEL_CODE,
            description: 'Bu oturumla bu panele erişemezsiniz. Doğru siteden giriş yapın.'
        }
    };
}

/** The notice for a session that cannot open any page here: no role at all, or roles this panel does not serve. */
export function landingFailureError(user: Pick<UserProfile, 'roles'> | null): SignInNotice {
    return user?.roles.some(role => !!role) ? sessionNotForThisPanelError() : noRoleForProductError();
}

/** Refresh refusals that end a session for a reason the login page should explain (F4, re-review L-b). */
const NOTICE_REFRESH_CODES: readonly string[] = [NO_ROLE_FOR_PRODUCT_CODE, 'Auth.ProductAccessDenied'];

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
    private oauthService = inject(OAuthService);
    private socialAuthService = inject(SocialAuthService);
    private router = inject(Router);
    private platformId = inject(PLATFORM_ID);
    private httpClient = inject(HttpClient);
    private siteAnalytics = inject(SiteAnalyticsService);

    private _userProfile = signal<UserProfile | null>(null);
    userProfile = this._userProfile.asReadonly();

    // Live test 13.11.2026 (Paket I3): tabs of one browser share the sign-in cookie, so signing in with another account in
    // one tab silently changed who the other tabs acted as. The tabs tell each other; the page warns, it does not sign out
    // (user decision 14.11.2026).
    private readonly _otherAccountSignedIn = signal(false);
    /** True when this browser signed in with another account than the one this tab shows. */
    readonly otherAccountSignedIn = this._otherAccountSignedIn.asReadonly();
    private readonly sessionChannel: BroadcastChannel | null = (() => {
        try {
            if (typeof BroadcastChannel === 'undefined') return null;
            const channel = new BroadcastChannel('eduivme-session');
            channel.onmessage = event => this.noteSignedInAccount((event.data as { id?: unknown } | null)?.id);
            return channel;
        } catch { return null; }
    })();
    /** Another tab (or this one, after a silent refresh) reports the account the browser is signed in with. */
    noteSignedInAccount(id: unknown): void {
        const mine = this._userProfile()?.id;
        if (typeof id === 'string' && id && mine) this._otherAccountSignedIn.set(id !== mine);
    }
    private _accessToken = signal('');
    private accessTokenExpiresAt = 0;
    private refreshTimer?: ReturnType<typeof setTimeout>;
    private refreshPromise?: Promise<boolean>;
    private oauthEventsSubscription?: Subscription;
    /** An error to show on the login page after a role-less session was ended outside it (F4: guard, refresh). */
    private readonly _signInNotice = signal<SignInNotice | null>(null);
    readonly signInNotice = this._signInNotice.asReadonly();

    public isDoneLoading$ = new BehaviorSubject<boolean>(false);

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            void this.initializeAuth();
        } else {
            this.isDoneLoading$.next(true);
        }
    }

    private async initializeAuth() {
        this.oauthService.configure(authConfig);
        this.oauthService.requireHttps = environment.production;

        try {
            await this.refreshSession();
        } finally {
            this.isDoneLoading$.next(true);
        }

        this.oauthEventsSubscription = this.oauthService.events.subscribe(event => {
            if (event.type === 'token_received' || event.type === 'token_refreshed') {
                const accessToken = this.oauthService.getAccessToken();
                if (accessToken) {
                    this.applySession({ accessToken, tokenType: 'Bearer', expiresInMinutes: 15 });
                }
            } else if (event.type === 'logout') {
                this.clearSession();
            }
        });
    }

    async waitForAuth(): Promise<boolean> {
        await firstValueFrom(this.isDoneLoading$.pipe(filter(isDone => isDone)));
        return this.isAuthenticated();
    }

    isAuthenticated(): boolean {
        return this._accessToken().length > 0 && Date.now() < this.accessTokenExpiresAt;
    }

    login() {
        this.oauthService.initLoginFlow();
    }

    async loginWithGoogle(idToken: string, legalAcceptances: RegistrationLegalAcceptance[] = []): Promise<AuthLoginResult> {
        const response = await firstValueFrom(this.httpClient.post<AuthLoginResponse>(
            `${environment.apiUrl}/auth/coaching/google-login`,
            { idToken, legalAcceptances, utm: this.siteAnalytics.registrationUtm() },
            { withCredentials: true }));
        return this.handleLoginResponse(response);
    }

    async completeGoogleRegistration(registrationToken: string, legalAcceptances: RegistrationLegalAcceptance[]): Promise<AuthLoginResult> {
        const response = await firstValueFrom(this.httpClient.post<AuthLoginResponse>(
            `${environment.apiUrl}/auth/coaching/google-register-complete`,
            { registrationToken, legalAcceptances, utm: this.siteAnalytics.registrationUtm() }, { withCredentials: true }));
        return this.handleLoginResponse(response);
    }

    async linkGoogleAccount(idToken: string): Promise<void> {
        await firstValueFrom(this.httpClient.post(
            `${environment.apiUrl}/auth/google-link`,
            { idToken },
            { withCredentials: true }));
    }

    async loginWithPassword(
        email: string,
        pass: string,
        rememberMe: boolean = true): Promise<AuthLoginResult> {
        const response = await firstValueFrom(this.httpClient.post<AuthLoginResponse>(
            `${environment.apiUrl}/auth/coaching/login`,
            { email, password: pass, rememberMe },
            { withCredentials: true }));
        return this.handleLoginResponse(response);
    }

    async startMfaSetup(challengeToken: string): Promise<MfaSetupResponse> {
        return firstValueFrom(this.httpClient.post<MfaSetupResponse>(
            `${environment.apiUrl}/auth/mfa/setup`,
            { challengeToken }));
    }

    async startAuthenticatedMfaSetup(currentPassword: string): Promise<MfaSetupResponse> {
        return firstValueFrom(this.httpClient.post<MfaSetupResponse>(
            `${environment.apiUrl}/auth/mfa/setup-authenticated`,
            { currentPassword },
            { withCredentials: true }));
    }

    async enableMfa(
        challengeToken: string,
        setupToken: string,
        code: string): Promise<string[]> {
        const response = await firstValueFrom(this.httpClient.post<MfaSessionResponse>(
            `${environment.apiUrl}/auth/mfa/enable`,
            { challengeToken, setupToken, code },
            { withCredentials: true }));
        this.applySession(response);
        return response.recoveryCodes ?? [];
    }

    async verifyMfa(
        challengeToken: string,
        code: string | null,
        recoveryCode: string | null = null): Promise<void> {
        const response = await firstValueFrom(this.httpClient.post<AuthSessionResponse>(
            `${environment.apiUrl}/auth/mfa/verify`,
            { challengeToken, code, recoveryCode },
            { withCredentials: true }));
        this.applySession(response);
        await this.navigateToLanding();
    }

    async refreshSession(): Promise<boolean> {
        if (!isPlatformBrowser(this.platformId)) return false;
        if (this.refreshPromise) return this.refreshPromise;

        const operation = this.performRefresh();
        this.refreshPromise = operation;
        try {
            return await operation;
        } finally {
            if (this.refreshPromise === operation) this.refreshPromise = undefined;
        }
    }

    private async performRefresh(): Promise<boolean> {
        try {
            const response = await firstValueFrom(this.httpClient.post<AuthSessionResponse>(
                `${environment.apiUrl}/auth/refresh-token`,
                {},
                { withCredentials: true }));
            if (!response?.accessToken) {
                this.clearSession();
                return false;
            }

            this.applySession(response);
            return true;
        } catch (error: unknown) {
            this.clearSession();
            // F4 (PR #125 review L4, re-review L-b): the server refused the session because the account has no role
            // or no access here any more (removing the last role closes the access); the login page says why.
            const code = getApiErrorCode(error);
            if (code && NOTICE_REFRESH_CODES.includes(code)) this._signInNotice.set(refreshRefusalNotice(error, code));
            return false;
        }
    }

    async confirmEmail(userId: string, token: string): Promise<unknown> {
        return firstValueFrom(this.httpClient.post(
            `${environment.apiUrl}/auth/confirm-email`,
            // PR #111 third review N2: the Coaching welcome e-mail.
            { userId, token, product: 'coaching' }));
    }

    async resendVerificationEmail(email: string): Promise<unknown> {
        return firstValueFrom(this.httpClient.post(
            `${environment.apiUrl}/auth/resend-verification-email`,
            // PR #111 second review H1: the Coaching e-mail and link (the API may otherwise pick by role).
            { email, product: 'coaching' }));
    }

    async logout() {
        await this.endSession();
        await this.router.navigate(['/auth/login']);
    }

    private async endSession() {
        this.clearSession();

        try {
            await this.socialAuthService.signOut();
        } catch {
            // The user may not have authenticated with Google.
        }

        try {
            await firstValueFrom(this.httpClient.post(
                `${environment.apiUrl}/auth/revoke-token`,
                {},
                { withCredentials: true }));
        } catch {
            // Local state must still be cleared when the network is unavailable.
        }

        this.oauthService.logOut();
    }

    /**
     * Paket F F4: opens the page for the signed-in roles. A session without any role in the product (the server
     * refuses those since F4; this guards an older or unexpected token) is ended and the login error is thrown,
     * so it never reaches the management shell.
     */
    async navigateToLanding(): Promise<void> {
        const route = this.getLandingRoute();
        if (!route) {
            const failure = landingFailureError(this.userProfile());
            await this.endSession();
            throw failure;
        }
        await this.router.navigate([route]);
    }

    /** Ends a role-less session found by a route guard; the login page shows why (see consumeSignInNotice). */
    async endRoleLessSession(): Promise<void> {
        const notice = landingFailureError(this.userProfile());
        await this.endSession();
        this._signInNotice.set(notice);
    }

    consumeSignInNotice(): SignInNotice | null {
        const notice = this._signInNotice();
        this._signInNotice.set(null);
        return notice;
    }

    private applySession(response: AuthSessionResponse) {
        this._accessToken.set(response.accessToken);
        const lifetimeMinutes = Number.isFinite(response.expiresInMinutes)
            ? Math.max(1, response.expiresInMinutes)
            : 15;
        this.accessTokenExpiresAt = Date.now() + lifetimeMinutes * 60_000;
        this.loadUserProfile();
        this.scheduleRefresh();
    }

    private async handleLoginResponse(response: AuthLoginResponse): Promise<AuthLoginResult> {
        if (response.requiresLegalAcceptance && response.registrationToken) {
            return { authenticated: false, requiresMfa: false, mfaEnrollmentRequired: false,
                mfaChallengeToken: null, requiresLegalAcceptance: true, registrationToken: response.registrationToken };
        }
        if (response.requiresMfa) {
            return {
                authenticated: false,
                requiresMfa: true,
                mfaEnrollmentRequired: response.mfaEnrollmentRequired === true,
                mfaChallengeToken: response.mfaChallengeToken ?? null
            };
        }

        if (!response.accessToken || !response.tokenType || response.expiresInMinutes === undefined) {
            return {
                authenticated: false,
                requiresMfa: false,
                mfaEnrollmentRequired: false,
                mfaChallengeToken: null
            };
        }

        this.applySession(response as AuthSessionResponse);
        await this.navigateToLanding();
        return {
            authenticated: true,
            requiresMfa: false,
            mfaEnrollmentRequired: false,
            mfaChallengeToken: null
        };
    }

    private scheduleRefresh() {
        if (this.refreshTimer) clearTimeout(this.refreshTimer);
        const delay = Math.max(1_000, this.accessTokenExpiresAt - Date.now() - 60_000);
        this.refreshTimer = setTimeout(() => void this.refreshSession(), delay);
    }

    private clearSession() {
        if (this.refreshTimer) clearTimeout(this.refreshTimer);
        this.refreshTimer = undefined;
        this._accessToken.set('');
        this.accessTokenExpiresAt = 0;
        this._userProfile.set(null);
    }

    private loadUserProfile() {
        const parsedToken = this.parseJwt(this._accessToken());
        const roleClaimType = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
        const roles = parsedToken.role
            || parsedToken.roles
            || parsedToken[roleClaimType]
            || parsedToken.realm_access?.roles
            || [];
        const permissions = parsedToken.permission || [];
        const permissionsArray = Array.isArray(permissions) ? permissions : [permissions];
        const rolesArray = Array.isArray(roles) ? roles : [roles];
        const authenticationMethods = Array.isArray(parsedToken.amr)
            ? parsedToken.amr
            : [parsedToken.amr];
        const appRoles = [
            'SystemAdmin', 'Admin', 'InstitutionOwner', 'InstitutionAdmin',
            'Teacher', 'Student', 'Parent', 'Editor'
        ];
        const mainRole = appRoles.find(role => rolesArray.includes(role)) || rolesArray[0] || 'User';

        const previousId = this._userProfile()?.id;
        const signedInId = parsedToken.sub || parsedToken.id;
        // The same tab got another account's token without a sign-in here (the shared cookie was replaced elsewhere).
        if (previousId && signedInId && previousId !== signedInId) this._otherAccountSignedIn.set(true);
        else if (!previousId) this._otherAccountSignedIn.set(false);
        try { if (signedInId) this.sessionChannel?.postMessage({ id: signedInId }); } catch { /* a closed channel only loses the warning */ }
        this._userProfile.set({
            id: parsedToken.sub || parsedToken.id,
            email: parsedToken.email || parsedToken.unique_name,
            firstName: parsedToken.given_name || parsedToken.firstName || '',
            lastName: parsedToken.family_name || parsedToken.lastName || '',
            username: parsedToken.preferred_username || parsedToken.email || '',
            roles: rolesArray,
            role: mainRole,
            permissions: permissionsArray,
            product: parsedToken.platform_product,
            mfaVerified: authenticationMethods.includes('mfa')
        });
    }

    hasPermission(permission: string): boolean {
        return hasRequiredPermission(this.userProfile(), permission);
    }

    /** The first page for the signed-in roles, or null when the session has no role at all (F4). */
    getLandingRoute(): string | null {
        const profile = this.userProfile();
        const role = profile?.role;
        if (role === 'Parent') return '/coaching-portal/parent';
        if (['Student', 'Teacher'].includes(role ?? '')) return '/coaching-portal';
        return hasManagementRole(profile) ? '/dashboard' : null;
    }

    private parseJwt(token: string): any {
        if (typeof window === 'undefined' || !token) return {};
        try {
            const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
            return JSON.parse(decodeURIComponent(window.atob(payload).split('').map(character =>
                `%${(`00${character.charCodeAt(0).toString(16)}`).slice(-2)}`).join('')));
        } catch {
            return {};
        }
    }

    getToken(): string {
        return this.isAuthenticated() ? this._accessToken() : '';
    }

    ngOnDestroy() {
        this.oauthEventsSubscription?.unsubscribe();
        this.clearSession();
    }
}

/** The server's own refusal text where it has one (the "sen" pages rewrite it by code). */
function refreshRefusalNotice(error: unknown, code: string): SignInNotice {
    const body = (error as { error?: { description?: unknown; Description?: unknown } } | null)?.error;
    const description = body?.description ?? body?.Description;
    return typeof description === 'string' && description.trim()
        ? { status: 400, error: { code, description } }
        : code === NO_ROLE_FOR_PRODUCT_CODE
            ? noRoleForProductError()
            : { status: 400, error: { code, description: 'Bu platform için hesabınıza erişim yetkisi tanımlı değil.' } };
}
