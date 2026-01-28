import { Injectable, computed, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OAuthService, AuthConfig } from 'angular-oauth2-oidc';
import { authConfig } from './auth.config';
import { Router } from '@angular/router';
import { BehaviorSubject, filter, firstValueFrom } from 'rxjs';
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

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private oauthService = inject(OAuthService);
    private router = inject(Router);
    private platformId = inject(PLATFORM_ID);

    // State
    private _userProfile = signal<UserProfile | null>(null);
    userProfile = this._userProfile.asReadonly();

    // AuthGuard'ın bekleyeceği sinyal
    public isDoneLoading$ = new BehaviorSubject<boolean>(false);

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            // BEST PRACTICE: Kararlılık için her zaman LocalStorage kullan.
            this.oauthService.setStorage(localStorage);
            this.oauthService.requireHttps = false; // Early disable
            this.initializeAuth();
        } else {
            this.isDoneLoading$.next(true); // SSR için bloklama yapmasın
        }
    }

    private async initializeAuth() {
        this.oauthService.configure(authConfig);
        this.oauthService.requireHttps = false; // FORCE DISABLE FOR LOCAL DEV
        // this.oauthService.setupAutomaticSilentRefresh(); // Disabled to avoid 415 errors with custom JSON backend

        try {
            // Discovery'yi yerel geliştirme için tamamen opsiyonel yapalım.
            // if (authConfig.issuer) {
            //     await this.oauthService.loadDiscoveryDocument();
            // }

            // Kütüphanenin kendi kendine "refresh" yapmasını engellemek için 
            // sadece geçerli bir manuel token var mı ona bakıyoruz.
            if (this.isAuthenticated()) {
                this.loadUserProfile();
            }
        } catch (e) {
            console.warn('Auth Init bypassed for manual flow.');
        } finally {
            // Her durumda (hata olsa bile) "Yükleme Bitti" de, yoksa uygulama sonsuza kadar bekler.
            this.isDoneLoading$.next(true);
        }

        this.oauthService.events.subscribe(e => {
            if (e.type === 'token_received' || e.type === 'token_refreshed') {
                this.loadUserProfile();
            } else if (e.type === 'logout') {
                this._userProfile.set(null);
            }
        });
    }

    // --- GUARD İÇİN BEKLEME HELPER'I ---
    async waitForAuth(): Promise<boolean> {
        // isDoneLoading true olana kadar bekle
        await firstValueFrom(this.isDoneLoading$.pipe(filter(isDone => isDone === true)));
        return this.isAuthenticated();
    }

    isAuthenticated(): boolean {
        if (this.oauthService.hasValidAccessToken()) return true;

        if (isPlatformBrowser(this.platformId)) {
            const token = localStorage.getItem('access_token');
            const expiresAtStr = localStorage.getItem('expires_at');

            if (token && expiresAtStr) {
                const expiresAt = parseInt(expiresAtStr, 10);
                if (Date.now() < expiresAt) {
                    return true;
                }
            }
        }
        return false;
    }

    private httpClient = inject(HttpClient);

    // --- ACTIONS ---

    login() {
        this.oauthService.initLoginFlow();
    }

    async loginWithGoogle(idToken: string): Promise<boolean> {
        try {
            const response = await firstValueFrom(this.httpClient.post<any>(`${environment.apiUrl}/auth/google-login`, { idToken }));

            if (response && response.accessToken) {
                // Manually save tokens for OAuthService compatibility or simple usage
                localStorage.setItem('access_token', response.accessToken);
                localStorage.setItem('refresh_token', response.refreshToken);

                // Set fake expiry if not provided, basically activating the session
                // OAuthService checks 'expires_at' (epoch ms)
                const expiresAt = Date.now() + (15 * 60 * 1000); // 15 mins default
                localStorage.setItem('expires_at', expiresAt.toString());

                // Trigger profile load
                this.loadUserProfile();
                this.router.navigate(['/dashboard']);
                return true;
            }
        } catch (err) {
            console.error('Google Login Backend Failed', err);
            throw err;
        }
        return false;
    }

    async loginWithPassword(email: string, pass: string, rememberMe: boolean = true): Promise<boolean> {
        try {
            const response = await firstValueFrom(
                this.httpClient.post<any>(`${environment.apiUrl}/auth/login`, {
                    email,
                    password: pass
                })
            );

            if (response && response.accessToken) {
                // Manually save tokens
                localStorage.setItem('access_token', response.accessToken);
                localStorage.setItem('refresh_token', response.refreshToken);

                const expiresAt = Date.now() + (response.expiresInMinutes || 15) * 60 * 1000;
                localStorage.setItem('expires_at', expiresAt.toString());

                // Trigger profile load
                this.loadUserProfile();
                this.router.navigate(['/dashboard']);
                return true;
            }
        } catch (err) {
            console.error('Login Failed', err);
            throw err;
        }
        return false;
    }

    logout() {
        this.oauthService.logOut();
        if (isPlatformBrowser(this.platformId)) {
            localStorage.removeItem('access_token');
            localStorage.removeItem('refresh_token');
            localStorage.removeItem('expires_at');
            // Also notify the library to clear any internal state
            sessionStorage.clear();
        }
        this._userProfile.set(null);
        this.router.navigate(['/auth/login']);
    }

    private loadUserProfile() {
        let accessToken = this.oauthService.getAccessToken();
        if (!accessToken && isPlatformBrowser(this.platformId)) {
            accessToken = localStorage.getItem('access_token') || '';
        }

        const idClaims = this.oauthService.getIdentityClaims() as any;

        if (accessToken) {
            const parsedToken = this.parseJwt(accessToken);

            // Handle multiple role claim formats (standard and Microsoft specific)
            const roleClaimType = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
            const roles = parsedToken.role ||
                parsedToken.roles ||
                parsedToken[roleClaimType] ||
                parsedToken.realm_access?.roles ||
                [];

            // Handle permissions
            const permissions = parsedToken.permission || [];
            const permissionsArray = Array.isArray(permissions) ? permissions : [permissions];

            const rolesArray = Array.isArray(roles) ? roles : [roles];

            const appRoles = ['SystemAdmin', 'Admin', 'InstitutionOwner', 'InstitutionAdmin', 'Teacher', 'Student', 'Parent'];
            const mainRole = appRoles.find(r => rolesArray.includes(r)) || rolesArray[0] || 'User';

            this._userProfile.set({
                id: idClaims?.sub || parsedToken.sub || parsedToken.id,
                email: idClaims?.email || parsedToken.email || parsedToken.unique_name,
                firstName: idClaims?.given_name || parsedToken.given_name || parsedToken.firstName || '',
                lastName: idClaims?.family_name || parsedToken.family_name || parsedToken.lastName || '',
                username: idClaims?.preferred_username || parsedToken.preferred_username || parsedToken.email || '',
                roles: rolesArray,
                role: mainRole,
                permissions: permissionsArray
            });
        }
    }

    hasPermission(permission: string): boolean {
        const user = this.userProfile();
        return user?.permissions?.includes(permission) ?? false;
    }

    private parseJwt(token: string) {
        if (typeof window === 'undefined') return {};
        try {
            return JSON.parse(decodeURIComponent(window.atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join('')));
        } catch (e) {
            return {};
        }
    }

    getToken(): string {
        return this.oauthService.getAccessToken() || (isPlatformBrowser(this.platformId) ? localStorage.getItem('access_token') || '' : '');
    }
}
