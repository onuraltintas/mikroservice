import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/user.model';
import { environment } from '../../../environments/environment';
import { SettingsService } from './settings.service';

/**
 * Authentication Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (AuthResponse, etc.)
 * - Token and user management remain unchanged
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly settingsService = inject(SettingsService);
  private readonly API_URL = environment.apiUrl;

  private currentUserSubject = new BehaviorSubject<AuthResponse | null>(
    this.getUserFromStorage()
  );
  public currentUser$ = this.currentUserSubject.asObservable();

  private getUserFromStorage(): AuthResponse | null {
    const userJson = localStorage.getItem('currentUser');
    if (!userJson) return null;

    const user = JSON.parse(userJson);

    // Hydrate missing fields from token if available
    if (user.token && (!user.institutionId || !user.institutionName)) {
      try {
        const decoded = this.decodeToken(user.token);
        let changed = false;

        if (!user.institutionId && decoded['InstitutionId']) {
          user.institutionId = decoded['InstitutionId'];
          changed = true;
        }
        if (!user.institutionName && decoded['InstitutionName']) {
          user.institutionName = decoded['InstitutionName'];
          changed = true;
        }
        // Ensure roles are synced
        if (decoded['role']) {
          const roles = Array.isArray(decoded['role']) ? decoded['role'] : [decoded['role']];
          if (!user.roles || user.roles.length === 0) {
            user.roles = roles;
            changed = true;
          }
        }

        if (changed) {
          localStorage.setItem('currentUser', JSON.stringify(user));
        }
      } catch (e) {
        console.error('Error hydrating user from storage token', e);
      }
    }

    return user;
  }

  get currentUserValue(): AuthResponse | null {
    return this.currentUserSubject.value;
  }

  get isAuthenticated(): boolean {
    return !!this.currentUserValue;
  }

  get token(): string | null {
    return this.currentUserValue?.token || null;
  }

  hasRole(role: string): boolean {
    const userRoles = this.currentUserValue?.roles || [];
    // Admin has access to everything (Superuser) logic REMOVED on user request
    // if (userRoles.includes('Admin')) {
    //   return true;
    // }
    return userRoles.includes(role);
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
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/login`, credentials).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Register new user
   * Backend returns: ApiResponse<AuthResponse>
   * Service receives: AuthResponse (auto-unwrapped)
   */
  register(data: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/register`, data).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Register new institution
   */
  registerInstitution(data: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/register-institution`, data).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Register new teacher
   */
  registerTeacher(data: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/register-teacher`, data).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  registerCoach(data: any): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/v1/auth/register-coach`, data);
  }

  /**
   * Logout current user
   * Calls backend to log the event, then clears local storage and redirects to login
   */
  logout(): void {
    // Read refreshToken BEFORE clearing state
    const refreshToken = this.currentUserValue?.refreshToken;

    // IMPORTANT: Clear local storage FIRST to prevent error interceptor from calling logout again
    localStorage.removeItem('currentUser');
    localStorage.removeItem('token');
    this.currentUserSubject.next(null);

    // Optional: Notify backend (fire-and-forget, ignore errors)
    this.http.post(`${this.API_URL}/v1/auth/logout`, { refreshToken }).subscribe({
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
    const refreshToken = this.currentUserValue?.refreshToken;
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/refresh-token`, { refreshToken }).pipe(
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
    return this.http.post<AuthResponse>(`${this.API_URL}/v1/auth/google-login`, payload).pipe(
      tap(response => {
        this.setUser(response);
      })
    );
  }

  /**
   * Request password reset email
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${this.API_URL}/v1/auth/forgot-password`, { email });
  }

  /**
   * Reset password with token
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  resetPassword(data: { email: string; token: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.API_URL}/v1/auth/reset-password`, data);
  }

  /**
   * Verify email with token
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  verifyEmail(data: { email: string; token: string }): Observable<any> {
    return this.http.post(`${this.API_URL}/v1/auth/verify-email`, data);
  }

  /**
   * Resend verification email
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  resendVerification(email: string): Observable<any> {
    return this.http.post(`${this.API_URL}/v1/auth/resend-verification`, { email });
  }

  /** Called by GoogleCallbackComponent after server-side OAuth redirect */
  loginFromCallback(response: AuthResponse): void {
    this.setUser(response);
  }

  private setUser(response: AuthResponse): void {
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

    localStorage.setItem('currentUser', JSON.stringify(response));
    localStorage.setItem('token', response.token);
    this.currentUserSubject.next(response);
    // Load user settings after successful authentication
    this.settingsService.loadSettings().subscribe();
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

  /**
   * Update current user information
   * Useful for updating user data after profile changes
   */
  updateUser(userData: Partial<AuthResponse>): void {
    const currentUser = this.currentUserValue;
    if (currentUser) {
      const updatedUser = { ...currentUser, ...userData };
      localStorage.setItem('currentUser', JSON.stringify(updatedUser));
      this.currentUserSubject.next(updatedUser);
    }
  }
  /**
   * Change password for current user
   */
  changePassword(data: any): Observable<any> {
    return this.http.post(`${this.API_URL}/v1/auth/change-password`, data);
  }
}
