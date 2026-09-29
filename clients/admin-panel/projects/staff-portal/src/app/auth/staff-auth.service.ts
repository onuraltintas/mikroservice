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
    const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
      `${environment.apiUrl}/auth/mfa/verify`,
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

  async startMfaSetup(challengeToken: string): Promise<StaffMfaSetupResponse> {
    return firstValueFrom(this.http.post<StaffMfaSetupResponse>(
      `${environment.apiUrl}/auth/mfa/setup`,
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
      `${environment.apiUrl}/auth/mfa/enable`,
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

    const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
      `${environment.apiUrl}/auth/staff-session/switch/${product}`,
      {},
      { withCredentials: true }
    ));
    this.applySession(response, product);
    this._activeProduct.set(product);
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
    this.clearSession();
    try {
      await firstValueFrom(this.http.post(
        `${environment.apiUrl}/auth/revoke-token`,
        {},
        { withCredentials: true }
      ));
    } catch {
      // Local access is already cleared; a later refresh will reject a stale cookie.
    }
  }

  private async performRefresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(this.http.post<AuthSessionResponse>(
        `${environment.apiUrl}/auth/refresh-token`,
        {},
        { withCredentials: true }
      ));
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
    this._activeProduct.set(claims?.platform_product ?? fallbackProduct ?? null);
  }

  private clearSession(): void {
    this._accessToken.set('');
    this._products.set([]);
    this._activeProduct.set(null);
    this.accessTokenExpiresAt = 0;
  }
}
