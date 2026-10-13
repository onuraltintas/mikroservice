import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { jwtDecode } from 'jwt-decode';
import { environment } from '../../environments/environment';
import {
  AuthSessionResponse,
  LoginResponse,
  StaffLoginResult,
  StaffMfaSetupResponse,
  StaffProduct,
  StaffProductAccess
} from './staff-auth.models';

interface AccessTokenClaims {
  exp?: number;
  platform_product?: StaffProduct;
  sub?: string;
}

@Injectable({ providedIn: 'root' })
export class StaffAuthService {
  private static readonly activeProductStorageKey = 'eduplatform_staff_active_product';
  private readonly http = inject(HttpClient);
  private readonly _accessToken = signal('');
  private readonly _products = signal<StaffProductAccess[]>([]);
  private readonly _activeProduct = signal<StaffProduct | null>(null);
  private accessTokenExpiresAt = 0;
  private refreshPromise?: Promise<boolean>;

  readonly products = this._products.asReadonly();
  readonly activeProduct = this._activeProduct.asReadonly();

  isAuthenticated(): boolean {
    return this._accessToken().length > 0 && Date.now() < this.accessTokenExpiresAt;
  }

  getAccessToken(): string {
    return this.isAuthenticated() ? this._accessToken() : '';
  }

  getCurrentUserId(): string | null {
    if (!this.isAuthenticated()) return null;
    try {
      return jwtDecode<AccessTokenClaims>(this._accessToken()).sub ?? null;
    } catch {
      return null;
    }
  }

  async login(
    product: StaffProduct,
    email: string,
    password: string,
    rememberMe = false
  ): Promise<StaffLoginResult> {
    const response = await firstValueFrom(this.http.post<LoginResponse>(
      `${environment.apiUrl}/auth/${product}/login`,
      { email, password, rememberMe },
      { withCredentials: true }
    ));

    if (response.requiresMfa) {
      return {
        requiresMfa: true,
        mfaEnrollmentRequired: response.mfaEnrollmentRequired ?? false,
        mfaChallengeToken: response.mfaChallengeToken ?? null
      };
    }

    if (!response.accessToken) {
      throw new Error('Giriş oturumu başlatılamadı. Lütfen yeniden deneyin.');
    }

    this.applySession({
      accessToken: response.accessToken,
      tokenType: response.tokenType ?? 'Bearer',
      expiresInMinutes: response.expiresInMinutes ?? 15
    }, product);
    await this.loadStaffProducts();
    if (this._products().length === 0) {
      await this.logout();
      throw new Error('Bu hesapta öğretmen veya kurum personeli erişimi bulunamadı.');
    }

    return { requiresMfa: false, mfaEnrollmentRequired: false, mfaChallengeToken: null };
  }

  async verifyMfa(
    product: StaffProduct,
    challengeToken: string,
    code: string | null,
    recoveryCode: string | null = null
  ): Promise<void> {
    const verifyUrl = product === 'course-management'
      ? `${environment.apiUrl}/auth/course-management/mfa/verify`
      : `${environment.apiUrl}/auth/mfa/verify`;
    const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
      verifyUrl,
      { challengeToken, code, recoveryCode },
      { withCredentials: true }
    ));
    this.applySession(response, product);
    await this.loadStaffProducts();
    if (this._products().length === 0) {
      await this.logout();
      throw new Error('Bu hesapta öğretmen veya kurum personeli erişimi bulunamadı.');
    }
  }

  async startMfaSetup(challengeToken: string, product?: StaffProduct): Promise<StaffMfaSetupResponse> {
    return firstValueFrom(this.http.post<StaffMfaSetupResponse>(
      product === 'course-management'
        ? `${environment.apiUrl}/auth/course-management/mfa/setup`
        : `${environment.apiUrl}/auth/mfa/setup`,
      { challengeToken }
    ));
  }

  async enableMfa(
    product: StaffProduct,
    challengeToken: string,
    setupToken: string,
    code: string
  ): Promise<string[]> {
    const response = await firstValueFrom(this.http.post<AuthSessionResponse & { recoveryCodes?: string[] | null }>(
      product === 'course-management'
        ? `${environment.apiUrl}/auth/course-management/mfa/enable`
        : `${environment.apiUrl}/auth/mfa/enable`,
      { challengeToken, setupToken, code },
      { withCredentials: true }
    ));
    this.applySession(response, product);
    await this.loadStaffProducts();
    if (this._products().length === 0) {
      await this.logout();
      throw new Error('Bu hesapta öğretmen veya kurum personeli erişimi bulunamadı.');
    }
    return response.recoveryCodes ?? [];
  }

  async switchProduct(product: StaffProduct): Promise<void> {
    if (!this._products().some(access => access.product === product)) {
      throw new Error('Bu ürün için personel erişiminiz bulunmuyor.');
    }

    const switchUrl = product === 'course-management'
      ? `${environment.apiUrl}/auth/course-management/session`
      : `${environment.apiUrl}/auth/staff-session/switch/${product}`;
    const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
      switchUrl,
      {},
      { withCredentials: true }
    ));
    this.applySession(response, product);
    this._activeProduct.set(product);
  }

  async joinCourseManagement(role: 'Teacher' | 'InstitutionOwner'): Promise<void> {
    await firstValueFrom(this.http.post<void>(`${environment.apiUrl}/auth/course-management/join`, { role }));
    await this.loadStaffProducts();
    await this.switchProduct('course-management');
  }

  async restoreSession(): Promise<void> {
    const refreshed = await this.refreshSession();
    if (!refreshed) return;

    try {
      await this.loadStaffProducts();
      if (this._products().length === 0) await this.logout();
    } catch {
      await this.logout();
    }
  }

  async refreshSession(): Promise<boolean> {
    if (this.refreshPromise) return this.refreshPromise;

    const operation = this.performRefresh();
    this.refreshPromise = operation;
    try {
      return await operation;
    } finally {
      if (this.refreshPromise === operation) this.refreshPromise = undefined;
    }
  }

  async logout(): Promise<void> {
    const revokeUrl = this._activeProduct() === 'course-management'
      ? `${environment.apiUrl}/auth/course-management/revoke-token`
      : `${environment.apiUrl}/auth/revoke-token`;
    this.clearSession();
    try {
      await firstValueFrom(this.http.post(
        revokeUrl,
        {},
        { withCredentials: true }
      ));
    } catch {
      // Local access is already cleared; a later refresh will reject a stale cookie.
    }
  }

  private async performRefresh(): Promise<boolean> {
    try {
      const product = this._activeProduct() ?? this.getStoredActiveProduct();
      const refreshUrl = product === 'course-management'
        ? `${environment.apiUrl}/auth/course-management/refresh-token`
        : `${environment.apiUrl}/auth/refresh-token`;
      const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
        refreshUrl,
        {},
        { withCredentials: true }
      ));
      if (!response?.accessToken) {
        this.clearSession();
        return false;
      }

      this.applySession(response, product ?? undefined);
      return true;
    } catch {
      this.clearSession();
      return false;
    }
  }

  private async loadStaffProducts(): Promise<void> {
    const products = await firstValueFrom(this.http.get<StaffProductAccess[]>(
      `${environment.apiUrl}/auth/staff-session/products`,
      { withCredentials: true }
    ));
    this._products.set(products);
    if (!products.some(access => access.product === this._activeProduct())) {
      this._activeProduct.set(products[0]?.product ?? null);
    }
  }

  private applySession(response: AuthSessionResponse, fallbackProduct?: StaffProduct): void {
    this._accessToken.set(response.accessToken);
    let claims: AccessTokenClaims | null = null;
    try {
      claims = jwtDecode<AccessTokenClaims>(response.accessToken);
    } catch {
      // The server token remains authoritative; the response lifetime is the fallback.
    }

    this.accessTokenExpiresAt = claims?.exp
      ? claims.exp * 1000
      : Date.now() + response.expiresInMinutes * 60_000;
    const product = claims?.platform_product ?? fallbackProduct ?? null;
    this._activeProduct.set(product);
    if (product) this.storeActiveProduct(product);
  }

  private clearSession(): void {
    this._accessToken.set('');
    this._products.set([]);
    this._activeProduct.set(null);
    this.accessTokenExpiresAt = 0;
    try {
      if (typeof window !== 'undefined') window.sessionStorage.removeItem(StaffAuthService.activeProductStorageKey);
    } catch {
      // Session state remains in memory when browser storage is unavailable.
    }
  }

  private getStoredActiveProduct(): StaffProduct | null {
    if (typeof window === 'undefined') return null;
    try {
      const product = window.sessionStorage.getItem(StaffAuthService.activeProductStorageKey);
      return product === 'coaching' || product === 'speed-reading' || product === 'course-management' ? product : null;
    } catch {
      return null;
    }
  }

  private storeActiveProduct(product: StaffProduct): void {
    try {
      if (typeof window !== 'undefined') window.sessionStorage.setItem(StaffAuthService.activeProductStorageKey, product);
    } catch {
      // Session state remains in memory when browser storage is unavailable.
    }
  }
}
