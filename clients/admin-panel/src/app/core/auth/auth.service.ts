import { Injectable, inject, signal, PLATFORM_ID, OnDestroy } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { isPlatformBrowser } from '@angular/common';
import { OAuthService } from 'angular-oauth2-oidc';
import { authConfig } from './auth.config';
import { Router } from '@angular/router';
import { BehaviorSubject, filter, firstValueFrom, Subscription } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface UserProfile {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    username: string;
    roles: string[];
    role: string;
    permissions: string[];
}

interface AuthSessionResponse {
    accessToken: string;
    tokenType: string;
    expiresInMinutes: number;
}

export interface AuthLoginResult {
    authenticated: boolean;
    requiresMfa: boolean;
    mfaEnrollmentRequired: boolean;
    mfaChallengeToken: string | null;
}

interface AuthLoginResponse extends Partial<AuthSessionResponse> {
    requiresMfa?: boolean;
    mfaEnrollmentRequired?: boolean;
    mfaChallengeToken?: string | null;
}

export interface MfaSetupResponse {
    secret: string;
    otpAuthUri: string;
    setupToken: string;
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

export function hasRequiredAccess(
    user: UserProfile | null,
    permission: string,
    requiredRole?: string
): boolean {
    return hasRequiredPermission(user, permission)
        && (!requiredRole || hasRequiredRole(user, requiredRole));
}

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
    private oauthService = inject(OAuthService);
    private socialAuthService = inject(SocialAuthService);
    private router = inject(Router);
    private platformId = inject(PLATFORM_ID);
    private httpClient = inject(HttpClient);

    private _userProfile = signal<UserProfile | null>(null);
    userProfile = this._userProfile.asReadonly();
    private _accessToken = signal('');
    private accessTokenExpiresAt = 0;
    private refreshTimer?: ReturnType<typeof setTimeout>;
    private refreshPromise?: Promise<boolean>;
    private oauthEventsSubscription?: Subscription;

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

    async loginWithGoogle(idToken: string): Promise<AuthLoginResult> {
        const response = await firstValueFrom(this.httpClient.post<AuthLoginResponse>(
            `${environment.apiUrl}/auth/google-login`,
            { idToken },
            { withCredentials: true }));
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
            `${environment.apiUrl}/auth/login`,
            { email, password: pass, rememberMe },
            { withCredentials: true }));
        return this.handleLoginResponse(response);
    }

    async startMfaSetup(challengeToken: string): Promise<MfaSetupResponse> {
        return firstValueFrom(this.httpClient.post<MfaSetupResponse>(
            `${environment.apiUrl}/auth/mfa/setup`,
            { challengeToken }));
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
        await this.router.navigate([this.getLandingRoute()]);
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
        } catch {
            this.clearSession();
            return false;
        }
    }

    async confirmEmail(userId: string, token: string): Promise<unknown> {
        return firstValueFrom(this.httpClient.post(
            `${environment.apiUrl}/auth/confirm-email`,
            { userId, token }));
    }

    async resendVerificationEmail(email: string): Promise<unknown> {
        return firstValueFrom(this.httpClient.post(
            `${environment.apiUrl}/auth/resend-verification-email`,
            { email }));
    }

    async logout() {
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
        await this.router.navigate(['/auth/login']);
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
        await this.router.navigate([this.getLandingRoute()]);
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
        const appRoles = [
            'SystemAdmin', 'Admin', 'InstitutionOwner', 'InstitutionAdmin',
            'Teacher', 'Student', 'Parent'
        ];
        const mainRole = appRoles.find(role => rolesArray.includes(role)) || rolesArray[0] || 'User';

        this._userProfile.set({
            id: parsedToken.sub || parsedToken.id,
            email: parsedToken.email || parsedToken.unique_name,
            firstName: parsedToken.given_name || parsedToken.firstName || '',
            lastName: parsedToken.family_name || parsedToken.lastName || '',
            username: parsedToken.preferred_username || parsedToken.email || '',
            roles: rolesArray,
            role: mainRole,
            permissions: permissionsArray
        });
    }

    hasPermission(permission: string): boolean {
        return hasRequiredPermission(this.userProfile(), permission);
    }

    getLandingRoute(): string {
        const role = this.userProfile()?.role;
        return ['Student', 'Teacher', 'Parent'].includes(role ?? '')
            ? '/coaching-portal'
            : '/dashboard';
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
