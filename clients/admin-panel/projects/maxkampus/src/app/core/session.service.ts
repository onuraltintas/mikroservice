import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { jwtDecode } from 'jwt-decode';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** What Identity answers when a maxkampüs session opens (sign-in, two-step verification, Google, refresh). */
export interface SessionResponse {
  accessToken?: string;
  tokenType?: string;
  expiresInMinutes?: number;
  requiresMfa?: boolean;
  mfaEnrollmentRequired?: boolean;
  mfaChallengeToken?: string | null;
  recoveryCodes?: string[] | null;
}

interface Claims {
  sub?: string;
  email?: string;
  exp?: number;
  given_name?: string;
  family_name?: string;
  name?: string;
  role?: string | string[];
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
}

export type MaxkampusRole = 'owner' | 'teacher' | 'other';

/**
 * The maxkampus.com session. The access token lives in memory only; the refresh token is Identity's host-only cookie of
 * this address (course-management), so maxkoç and Master Hızlı Okuma stay signed in separately in the same browser.
 */
@Injectable({ providedIn: 'root' })
export class MaxkampusSession {
  // Refresh and sign-out go around the interceptors (they must not wait for themselves).
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly base = `${environment.apiUrl}/auth/course-management`;
  private readonly token = signal('');
  private readonly claims = signal<Claims | null>(null);
  private expiresAt = 0;
  private refreshing?: Promise<boolean>;

  readonly signedIn = computed(() => this.token().length > 0);
  readonly userId = computed(() => this.claims()?.sub ?? null);
  readonly email = computed(() => this.claims()?.email ?? null);
  readonly displayName = computed(() => {
    const claims = this.claims();
    const name = [claims?.given_name, claims?.family_name].filter(Boolean).join(' ').trim();
    return name || claims?.name || '';
  });
  readonly roles = computed(() => {
    const claims = this.claims();
    const raw = claims?.role ?? claims?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? [];
    return Array.isArray(raw) ? raw : [raw];
  });
  /** Owners buy the plan and see every section; teachers work in an institution's panel. */
  readonly role = computed<MaxkampusRole>(() => {
    const roles = this.roles();
    if (roles.includes('CourseInstitutionOwner')) return 'owner';
    if (roles.includes('CourseTeacher')) return 'teacher';
    return 'other';
  });

  accessToken(): string { return Date.now() < this.expiresAt ? this.token() : ''; }

  /** Applies a session answer; false when it carries no token (two-step verification is asked for). */
  apply(response: SessionResponse | null | undefined): boolean {
    if (!response?.accessToken) return false;
    let claims: Claims | null = null;
    try { claims = jwtDecode<Claims>(response.accessToken); } catch { /* the server checks the token anyway */ }
    this.token.set(response.accessToken);
    this.claims.set(claims);
    this.expiresAt = claims?.exp ? claims.exp * 1000 - 30_000 : Date.now() + (response.expiresInMinutes ?? 15) * 60_000 - 30_000;
    return true;
  }

  /** Restores the session from the refresh cookie (page load); false when there is none. */
  refresh(): Promise<boolean> {
    this.refreshing ??= this.doRefresh().finally(() => this.refreshing = undefined);
    return this.refreshing;
  }

  async ensure(): Promise<boolean> {
    if (this.accessToken()) return true;
    return this.refresh();
  }

  async signOut(): Promise<void> {
    this.clear();
    try { await firstValueFrom(this.http.post(`${this.base}/revoke-token`, {}, { withCredentials: true })); } catch { /* local session is gone */ }
  }

  clear(): void {
    this.token.set('');
    this.claims.set(null);
    this.expiresAt = 0;
  }

  private async doRefresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(this.http.post<SessionResponse>(`${this.base}/refresh-token`, {}, { withCredentials: true }));
      if (this.apply(response)) return true;
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) throw error;
    }
    this.clear();
    return false;
  }
}
