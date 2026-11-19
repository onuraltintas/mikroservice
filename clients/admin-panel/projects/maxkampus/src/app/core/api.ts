import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { SessionResponse } from './session.service';

/** One piece of maxkampus.com content (site-service, public view). */
export interface SiteEntry {
  id: string; kind: string; slug: string | null; title: string; subtitle: string | null; body: string; highlights: string[];
  brand: string | null; icon: string | null; linkLabel: string | null; linkUrl: string | null; secondaryLinkLabel: string | null;
  secondaryLinkUrl: string | null; imageUrl: string | null; ctaTitle: string | null; ctaBody: string | null; seoTitle: string | null;
  seoDescription: string | null; category?: string | null; audience?: string | null; author?: string | null; publishedAt?: string | null;
}
export interface SiteHome {
  site: SiteEntry | null; hero: SiteEntry | null; sections: SiteEntry[]; features: SiteEntry[]; steps: SiteEntry[]; reviews: SiteEntry[];
  faqs: SiteEntry[]; menus: SiteEntry[]; plans: SiteEntry[];
}
export interface SitePosts { total: number; items: SiteEntry[]; categories: string[]; }
export interface LegalPage { slug: string; title: string; content: string; version: number; updatedAt?: string | null; createdAt: string; }
export interface LegalAcceptance { slug: string; version: number; }
export interface BillingCatalogPlan {
  id: string; name: string; description: string; buyerType: string; price: number; currency: string; durationDays: number; seatCount: number | null;
  isContactOnly: boolean; isPublic: boolean; isActive: boolean; sortOrder: number; features: string[];
}
export interface BillingCatalog { productCode: string; productName: string; requireSubscription: boolean; salesOpen: boolean; plans: BillingCatalogPlan[]; }
export interface MyBilling {
  requireSubscription: boolean; salesOpen: boolean; subscriptions: { id: string; planName: string; endsAt: string; isCurrent: boolean }[];
  pendingOrders: { id: string; planName: string; status: string; paidDeclaredAt: string | null; paymentCode: string }[];
}
export interface RegistrationOutcome { userId: string; verificationEmail: 'sent' | 'notRequired'; }
export interface GooglePending { requiresLegalAcceptance: boolean; registrationToken: string; registrationExpiresAt: string; }
export interface TeacherInvitationInfo { workspaceName: string; email: string; status: 'Pending' | 'Accepted' | 'Cancelled' | 'Expired'; expiresAt: string; }
export interface RegisterRequest {
  kind: 'institution' | 'teacher' | 'invited'; email: string; password: string; firstName: string; lastName: string; phone: string;
  workspaceName: string | null; legalAcceptances: LegalAcceptance[]; mfaCode?: string | null;
}
export interface GoogleCompleteRequest {
  registrationToken: string; kind: 'institution' | 'teacher' | 'invited'; phone: string; workspaceName: string | null; legalAcceptances: LegalAcceptance[];
}

const SITE = `${environment.apiUrl}/site/sites/maxkampus`;
const AUTH = `${environment.apiUrl}/auth`;
const CM = `${environment.apiUrl}/auth/course-management`;

/** Every call maxkampus.com makes outside the institution panel. */
@Injectable({ providedIn: 'root' })
export class MaxkampusApi {
  private readonly http = inject(HttpClient);

  // ---- Site (site-service) ----
  home() { return firstValueFrom(this.http.get<SiteHome>(`${SITE}/home`)); }
  page(slug: string) { return firstValueFrom(this.http.get<SiteEntry>(`${SITE}/pages/${encodeURIComponent(slug)}`)); }
  posts(category: string | null, skip: number, take: number) {
    const params: Record<string, string | number> = { skip, take };
    if (category) params['category'] = category;
    return firstValueFrom(this.http.get<SitePosts>(`${SITE}/posts`, { params }));
  }
  post(slug: string) { return firstValueFrom(this.http.get<SiteEntry>(`${SITE}/posts/${encodeURIComponent(slug)}`)); }
  notices(audience: 'owners' | 'teachers') { return firstValueFrom(this.http.get<SiteEntry[]>(`${SITE}/notices`, { params: { audience } })); }
  contact(body: { name: string; organization: string | null; email: string; phone: string | null; message: string; consent: boolean; captchaToken: string | null }) {
    return firstValueFrom(this.http.post<void>(`${SITE}/contact`, body));
  }
  subscribe(body: { email: string; consent: boolean; captchaToken: string | null; source: string }) {
    return firstValueFrom(this.http.post<void>(`${SITE}/newsletter`, body));
  }
  confirmSubscription(token: string) { return firstValueFrom(this.http.post<void>(`${SITE}/newsletter/confirm`, { token })); }
  unsubscribe(token: string) { return firstValueFrom(this.http.post<void>(`${SITE}/newsletter/unsubscribe`, { token })); }

  // ---- Legal texts (Identity) ----
  legal(slug: string) {
    return firstValueFrom(this.http.get<{ data: LegalPage }>(`${environment.apiUrl}/platform/legal-pages/${encodeURIComponent(slug)}`)).then(answer => answer.data);
  }

  // ---- Billing catalog (public) ----
  catalog() { return firstValueFrom(this.http.get<BillingCatalog>(`${environment.apiUrl}/billing/v1/catalog/course-management`)); }
  myBilling() { return firstValueFrom(this.http.get<MyBilling>(`${environment.apiUrl}/billing/v1/me`, { params: { product: 'course-management' } })); }

  // ---- Account (Identity) ----
  login(email: string, password: string, rememberMe: boolean) {
    return firstValueFrom(this.http.post<SessionResponse>(`${CM}/login`, { email, password, rememberMe }, { withCredentials: true }));
  }
  verifyMfa(challengeToken: string, code: string | null, recoveryCode: string | null) {
    return firstValueFrom(this.http.post<SessionResponse>(`${CM}/mfa/verify`, { challengeToken, code, recoveryCode }, { withCredentials: true }));
  }
  startMfaSetup(challengeToken: string) {
    return firstValueFrom(this.http.post<{ secret: string; otpAuthUri: string; setupToken: string }>(`${CM}/mfa/setup`, { challengeToken }));
  }
  enableMfa(challengeToken: string, setupToken: string, code: string) {
    return firstValueFrom(this.http.post<SessionResponse>(`${CM}/mfa/enable`, { challengeToken, setupToken, code }, { withCredentials: true }));
  }
  register(request: RegisterRequest) { return firstValueFrom(this.http.post<RegistrationOutcome>(`${CM}/register`, request)); }
  google(idToken: string) {
    return firstValueFrom(this.http.post<SessionResponse & Partial<GooglePending>>(`${CM}/google`, { idToken }, { withCredentials: true }));
  }
  completeGoogle(request: GoogleCompleteRequest) {
    return firstValueFrom(this.http.post<SessionResponse>(`${CM}/google-register-complete`, request, { withCredentials: true }));
  }
  forgotPassword(email: string) { return firstValueFrom(this.http.post<void>(`${AUTH}/forgot-password`, { email, product: 'course-management' })); }
  resetPassword(email: string, token: string, newPassword: string) {
    return firstValueFrom(this.http.post<void>(`${AUTH}/reset-password`, { email, token, newPassword }));
  }
  confirmEmail(userId: string, token: string) {
    return firstValueFrom(this.http.post<{ status: 'confirmed' | 'alreadyConfirmed' }>(`${AUTH}/confirm-email`, { userId, token, product: 'course-management' }));
  }
  resendVerification(email: string) {
    return firstValueFrom(this.http.post<void>(`${AUTH}/resend-verification-email`, { email, product: 'course-management' }));
  }

  // ---- Teacher invitations (Course Management) ----
  invitation(code: string) {
    return firstValueFrom(this.http.get<TeacherInvitationInfo>(`${environment.apiUrl}/course-management/public/teacher-invitations/${encodeURIComponent(code)}`));
  }
  acceptInvitation(code: string, displayName: string) {
    return firstValueFrom(this.http.post<{ workspaceId: string }>(
      `${environment.apiUrl}/course-management/v1/me/teacher-invitations/${encodeURIComponent(code)}/accept`, { displayName }));
  }
}
