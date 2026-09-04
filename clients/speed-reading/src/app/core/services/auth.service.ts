import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, firstValueFrom, map, of, tap } from 'rxjs';
import { Router } from '@angular/router';
import { AuthResponse, LoginRequest, RegisterInstitutionRequest, RegisterRequest } from '../models/user.model';
import { environment } from '../../../environments/environment';
import { SettingsService } from './settings.service';

export interface MfaSetupResponse {
  secret: string;
  otpAuthUri: string;
  setupToken: string;
  challengeToken?: string | null;
}

interface MfaSessionResponse {
  accessToken: string;
  tokenType: string;
  expiresInMinutes: number;
  recoveryCodes?: string[] | null;
}

/**
 * Authentication Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (AuthResponse, etc.)
 * - Access tokens stay in memory; the refresh token is an HttpOnly cookie
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly settingsService = inject(SettingsService);
  private readonly API_URL = environment.apiUrl;
  private readonly AUTH_URL = `${this.API_URL}/auth`;
  private accessToken: string | null = null;
  private sessionInitialized = false;

  private currentUserSubject = new BehaviorSubject<AuthResponse | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  get currentUserValue(): AuthResponse | null {
    return this.currentUserSubject.value;
  }

  get isAuthenticated(): boolean {
    return !!this.currentUserValue && !!this.accessToken && !this.isAccessTokenExpired(this.accessToken);
  }

  get token(): string | null {
    return this.accessToken;
  }

  initializeSession(): Observable<AuthResponse | null> {
    if (this.sessionInitialized) {
      return of(this.currentUserValue);
    }

    this.sessionInitialized = true;
    // The refresh token is HttpOnly, so the browser cannot inspect it directly.
    // The locally stored user is the session marker written after a successful login.
    // Avoid probing the refresh endpoint for a genuinely anonymous visitor.
    if (typeof localStorage === 'undefined' || !localStorage.getItem('currentUser')) {
      return of(null);
    }

    return this.refreshToken().pipe(
      catchError(() => {
        this.clearLocalSession();
        return of(null);
      })
    );
  }

  hasRole(role: string): boolean {
    const userRoles = this.currentUserValue?.roles || [];
    // Admin has access to everything (Superuser) logic REMOVED on user request
    // if (userRoles.includes('Admin')) {
    //   return true;
    // }
    return userRoles.includes(role);
  }

  hasAdminAccess(): boolean {
    return this.hasRole('Admin') || this.hasRole('SystemAdmin');
  }

  /**
   * Check if user has completed their profile setup
   * Profile is complete if dateOfBirth is set
   */
  hasCompletedProfile(): boolean {
    const user = this.currentUserValue;
    return !!(user && user.dateOfBirth);
  }

  /**
   * Login user with credentials
   * Backend returns: ApiResponse<AuthResponse>
   * Service receives: AuthResponse (auto-unwrapped)
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/login`, credentials, { withCredentials: true }).pipe(
      map(response => this.normalizeAuthResponse(response)),
      tap(response => {
        if (!response.requiresMfa) {
          this.setUser(response);
        }
      })
    );
  }

  /**
   * Register new user
   * Backend returns: ApiResponse<AuthResponse>
   * Service receives: AuthResponse (auto-unwrapped)
   */
  register(data: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/register`, data, { withCredentials: true }).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Register new institution
   */
  registerInstitution(data: RegisterInstitutionRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/register-institution`, data, { withCredentials: true }).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Register new teacher
   */
  registerTeacher(data: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/register-teacher`, data, { withCredentials: true }).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  registerCoach(data: any): Observable<void> {
    return this.http.post<void>(`${this.AUTH_URL}/register-coach`, data, { withCredentials: true });
  }

  /**
   * Logout current user
   * Calls backend to log the event, then clears local storage and redirects to login
   */
  logout(): void {
    // IMPORTANT: Clear local storage FIRST to prevent error interceptor from calling logout again
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');
    this.currentUserSubject.next(null);

    // Optional: Notify backend (fire-and-forget, ignore errors)
    this.http.post(`${this.AUTH_URL}/revoke-token`, {}, { withCredentials: true }).subscribe({
      next: () => console.log('Logout event logged on backend'),
      error: () => { } // Ignore errors (token might be expired)
    });

    // Redirect to login
    this.router.navigate(['/auth/login']);
  }

  /**
   * Refresh authentication token
   * Backend returns: ApiResponse<AuthResponse>
   * Service receives: AuthResponse (auto-unwrapped)
   */
  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/refresh-token`, {}, {
      withCredentials: true,
      headers: { 'X-Skip-Error-Toast': 'true' }
    }).pipe(
      map(response => this.normalizeAuthResponse(response)),
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Google OAuth authentication
   * Backend returns: ApiResponse<AuthResponse>
   * Service receives: AuthResponse (auto-unwrapped)
   */
  googleAuth(idToken: string, role?: string): Observable<AuthResponse> {
    const payload: any = { idToken };
    if (role) {
      payload.role = role;
    }
    return this.http.post<AuthResponse>(`${this.AUTH_URL}/google-login`, payload, { withCredentials: true }).pipe(
      map(response => this.normalizeAuthResponse(response)),
      tap(response => {
        if (!response.requiresMfa) {
          this.setUser(response);
        }
      })
    );
  }

  /**
   * Request password reset email
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${this.AUTH_URL}/forgot-password`, { email }, { withCredentials: true });
  }

  /**
   * Reset password with token
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  resetPassword(data: { email: string; token: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.AUTH_URL}/reset-password`, data, { withCredentials: true });
  }

  /**
   * Verify email with token
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  verifyEmail(data: { email: string; token: string }): Observable<any> {
    return this.http.post(`${this.AUTH_URL}/confirm-email`, data, { withCredentials: true });
  }

  /**
   * Resend verification email
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  resendVerification(email: string): Observable<any> {
    return this.http.post(`${this.AUTH_URL}/resend-verification-email`, { email }, { withCredentials: true });
  }

  /** Called by GoogleCallbackComponent after server-side OAuth redirect */
  loginFromCallback(response: AuthResponse): void {
    this.setUser(this.normalizeAuthResponse(response));
  }

  private setUser(response: AuthResponse): void {
    response = this.normalizeAuthResponse(response);
    this.accessToken = response.token || null;

    // Decode token to ensure all claims are present in the user object
    if (response.token) {
      try {
        const decoded = this.decodeToken(response.token);
        // Map InstitutionId from token if not present in response
        if (!response.institutionId && decoded['InstitutionId']) {
          response.institutionId = decoded['InstitutionId'];
        }
        // Map other potentially missing fields
        if (!response.institutionName && decoded['InstitutionName']) {
          response.institutionName = decoded['InstitutionName'];
        }

        // Ensure roles are synced
        if (decoded['role']) {
          const roles = Array.isArray(decoded['role']) ? decoded['role'] : [decoded['role']];
          if (!response.roles || response.roles.length === 0) {
            response.roles = roles;
          }
        }
      } catch (e) {
        console.error('Error decoding token', e);
      }
    }

    localStorage.setItem('currentUser', JSON.stringify({
      ...response,
      token: undefined,
      refreshToken: undefined
    }));
    localStorage.removeItem('token');
    this.currentUserSubject.next(response);
    // Load user settings after successful authentication
    this.settingsService.loadSettings().subscribe();
  }

  private normalizeAuthResponse(response: AuthResponse): AuthResponse {
    const rawResponse = response as AuthResponse & { accessToken?: string };
    const token = rawResponse.token || rawResponse.accessToken || '';
    const decoded = token ? this.decodeToken(token) : {};
    const roles = this.readClaimValues(decoded, [
      'role',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    ]);

    return {
      ...rawResponse,
      id: rawResponse.id || decoded['sub'] || '',
      token,
      refreshToken: rawResponse.refreshToken || '',
      email: rawResponse.email || decoded['email'] || '',
      firstName: rawResponse.firstName || decoded['given_name'] || decoded['firstName'] || '',
      lastName: rawResponse.lastName || decoded['family_name'] || decoded['lastName'] || '',
      roles: rawResponse.roles?.length ? rawResponse.roles : roles
    };
  }

  private readClaimValues(claims: any, names: string[]): string[] {
    for (const name of names) {
      const value = claims?.[name];
      if (Array.isArray(value)) {
        return value.filter((item): item is string => typeof item === 'string');
      }
      if (typeof value === 'string' && value.length > 0) {
        return [value];
      }
    }
    return [];
  }

  /**
   * Helper to decode JWT token without external library
   */
  private decodeToken(token: string): any {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function (c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
      }).join(''));

      return JSON.parse(jsonPayload);
    } catch (e) {
      console.error('Failed to decode token', e);
      return {};
    }
  }

  private isAccessTokenExpired(token: string): boolean {
    const claims = this.decodeToken(token);
    return typeof claims['exp'] !== 'number' || claims['exp'] * 1000 <= Date.now();
  }

  /**
   * Update current user information
   * Useful for updating user data after profile changes
   */
  updateUser(userData: Partial<AuthResponse>): void {
    const currentUser = this.currentUserValue;
    if (currentUser) {
      const updatedUser = { ...currentUser, ...userData };
      localStorage.setItem('currentUser', JSON.stringify({
        ...updatedUser,
        token: undefined,
        refreshToken: undefined
      }));
      this.currentUserSubject.next(updatedUser);
    }
  }

  private clearLocalSession(): void {
    this.accessToken = null;
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');
    this.currentUserSubject.next(null);
  }
  /**
   * Change password for current user
   */
  changePassword(data: any): Observable<any> {
    return this.http.post(`${this.AUTH_URL}/change-password`, data, { withCredentials: true });
  }

  async startAuthenticatedMfaSetup(currentPassword: string): Promise<MfaSetupResponse> {
    return firstValueFrom(this.http.post<MfaSetupResponse>(
      `${this.AUTH_URL}/mfa/setup-authenticated`,
      { currentPassword },
      { withCredentials: true }
    ));
  }

  async startMfaSetup(challengeToken: string): Promise<MfaSetupResponse> {
    return firstValueFrom(this.http.post<MfaSetupResponse>(
      `${this.AUTH_URL}/mfa/setup`,
      { challengeToken }
    ));
  }

  async enableMfa(challengeToken: string, setupToken: string, code: string): Promise<string[]> {
    if (!/^\d{6}$/.test(code)) {
      throw new Error('MFA code must contain exactly six digits.');
    }

    const response = await firstValueFrom(this.http.post<MfaSessionResponse>(
      `${this.AUTH_URL}/mfa/enable`,
      { challengeToken, setupToken, code },
      { withCredentials: true }
    ));

    this.promoteMfaSession(response);

    return response.recoveryCodes ?? [];
  }

  async verifyMfa(
    challengeToken: string,
    code: string | null,
    recoveryCode: string | null = null): Promise<void> {
    const response = await firstValueFrom(this.http.post<MfaSessionResponse>(
      `${this.AUTH_URL}/mfa/verify`,
      { challengeToken, code, recoveryCode },
      { withCredentials: true }
    ));

    this.promoteMfaSession(response);
  }

  private promoteMfaSession(response: MfaSessionResponse): void {
    const currentUser = this.currentUserValue;
    this.setUser(this.normalizeAuthResponse({
      ...(currentUser ?? {}),
      token: response.accessToken,
      refreshToken: ''
    } as AuthResponse));
  }
}
