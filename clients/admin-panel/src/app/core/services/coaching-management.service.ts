import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingManagementPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export type CoachingCmsKind = 'Page' | 'Blog' | 'Block';

export interface CoachingCmsEntry {
  id: string;
  kind: CoachingCmsKind;
  group: string | null;
  title: string;
  slug: string;
  summary: string | null;
  content: string;
  seoTitle: string | null;
  seoDescription: string | null;
  tags: string[];
  isPublished: boolean;
  scheduledPublishAt: string | null;
  sortOrder: number;
  eyebrow?: string | null;
  linkLabel?: string | null;
  linkUrl?: string | null;
  secondaryLinkLabel?: string | null;
  secondaryLinkUrl?: string | null;
  imageUrl?: string | null;
  author?: string | null;
  publishedAt?: string | null;
  coverImageUrl?: string | null;
  testimonialConsentConfirmed?: boolean;
  viewCount: number;
  createdAt: string;
  updatedAt: string | null;
  version: number;
}

export type CoachingCmsEntryRequest = Omit<CoachingCmsEntry, 'id' | 'viewCount' | 'createdAt' | 'updatedAt' | 'version'>;

export interface CoachingCmsRevision {
  id: string;
  entryId: string;
  kind: CoachingCmsKind;
  version: number;
  createdAt: string;
  createdBy: string;
}

export interface CoachingCmsNavigationItem {
  id: string;
  menu: string;
  label: string;
  url: string;
  icon: string | null;
  sortOrder: number;
  isVisible: boolean;
  openInNewTab: boolean;
}

export type CoachingCmsNavigationRequest = Omit<CoachingCmsNavigationItem, 'id'>;

export interface CoachingCmsMediaAsset {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  url: string;
  altText: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CoachingSubscriptionPlan {
  id: string;
  slug: string;
  name: string;
  description: string;
  audience: 'Individual' | 'Institution' | 'Teacher';
  price: number;
  isContactOnly: boolean;
  billingPeriod: string;
  durationDays: number;
  includedStudentSeats: number | null;
  features: string[];
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
}

export type CoachingSubscriptionPlanRequest = Omit<CoachingSubscriptionPlan, 'id'>;

export interface CoachingSubscriptionSettings {
  requireActiveSubscription: boolean;
  currency: string;
  accountHolder: string | null;
  bankName: string | null;
  iban: string | null;
  paymentInstructions: string | null;
  bankTransferEnabled: boolean;
  isPubliclyAvailable: boolean;
  updatedAt: string | null;
}

export type CoachingSubscriptionSettingsRequest = Omit<CoachingSubscriptionSettings, 'isPubliclyAvailable' | 'updatedAt'>;

export interface CoachingSubscription {
  id: string;
  planId: string;
  plan: CoachingSubscriptionPlan;
  userId: string | null;
  userName: string | null;
  userEmail: string | null;
  institutionId: string | null;
  status: string;
  startDate: string;
  endDate: string;
  seatCount: number;
  usedSeatCount: number;
  notes: string | null;
  createdAt: string;
  paymentReference?: string | null;
}

export interface CoachingSubscriptionSeat {
  studentId: string;
  isSuspended: boolean;
  suspensionReason: string | null;
  assignedAt: string;
}

export interface CoachingSubscriptionAccess {
  hasAccess: boolean;
  enforcementEnabled: boolean;
  planName: string | null;
  status: string | null;
  accessUntil: string | null;
  subscriptions: CoachingSubscription[];
}

export interface CoachingBankTransferRequestCreate {
  planId: string;
  paymentReference: string;
  payerName: string | null;
  note: string | null;
  adultPayerDeclaration: boolean;
}

export interface CoachingBankTransferRequest {
  adultPayerDeclarationVersion?: number | null;
  adultPayerDeclaredAt?: string | null;
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  plan: CoachingSubscriptionPlan;
  amount: number;
  currency: string;
  paymentReference: string;
  payerName: string | null;
  note: string | null;
  status: string;
  subscriptionId: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNote: string | null;
  createdAt: string;
}

export interface CoachingPayment {
  id: string;
  userId: string;
  userName: string;
  userEmail: string;
  planName: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  reference: string | null;
  createdAt: string;
}

export type CoachingNewsletterSubscriberStatus = 'PendingConfirmation' | 'Active' | 'Unsubscribed';

export interface CoachingNewsletterRecaptchaConfiguration {
  enabled: boolean;
  siteKey: string | null;
}

export interface CoachingNewsletterSubscriber {
  id: string;
  email: string;
  status: CoachingNewsletterSubscriberStatus;
  source: string;
  consentTextVersion: string;
  consentedAt: string;
  confirmedAt: string | null;
  unsubscribedAt: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class CoachingManagementService {
  private readonly http = inject(HttpClient);
  private readonly coachingCmsAdminUrl = `${environment.apiUrl}/coaching-admin/cms`;
  private readonly coachingSubscriptionsAdminUrl = `${environment.apiUrl}/coaching-admin/subscriptions`;
  private readonly coachingPublicSubscriptionUrl = `${environment.apiUrl}/coaching/subscriptions`;
  private readonly coachingNewsletterPublicUrl = `${environment.apiUrl}/coaching/cms/newsletter`;
  private readonly coachingNewsletterAdminUrl = `${this.coachingCmsAdminUrl}/newsletter/subscribers`;

  getCoachingNewsletterRecaptchaConfiguration() {
    return this.http.get<{ success: boolean; data: CoachingNewsletterRecaptchaConfiguration }>(
      `${this.coachingNewsletterPublicUrl}/recaptcha`
    ).pipe(map(response => response.data));
  }

  subscribeToCoachingNewsletter(request: {
    email: string;
    consentGiven: boolean;
    privacyPolicyVersion: number;
    newsletterConsentVersion: number;
    honeypot?: string;
    recaptchaToken?: string;
  }) {
    return this.http.post<{ success: boolean; message: string }>(`${this.coachingNewsletterPublicUrl}/subscriptions`, {
      email: request.email.trim(),
      consentGiven: request.consentGiven,
      privacyPolicyVersion: request.privacyPolicyVersion,
      newsletterConsentVersion: request.newsletterConsentVersion,
      honeypot: request.honeypot ?? '',
      recaptchaToken: request.recaptchaToken ?? null
    });
  }

  confirmCoachingNewsletter(token: string) {
    return this.http.post<{ success: boolean; message: string }>(`${this.coachingNewsletterPublicUrl}/confirm`, { token });
  }

  unsubscribeFromCoachingNewsletter(token: string) {
    return this.http.post<{ success: boolean; message: string }>(`${this.coachingNewsletterPublicUrl}/unsubscribe`, { token });
  }

  getCoachingNewsletterSubscribers(pageNumber = 1, pageSize = 25, search = '', status = '') {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<{ data: CoachingManagementPage<CoachingNewsletterSubscriber> }>(this.coachingNewsletterAdminUrl, { params })
      .pipe(map(response => response.data));
  }

  exportCoachingNewsletterSubscribers(status = '', search = '') {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get(`${this.coachingNewsletterAdminUrl}/export`, { params, responseType: 'blob' });
  }

  unsubscribeCoachingNewsletterSubscriber(id: string) {
    return this.http.post<void>(`${this.coachingNewsletterAdminUrl}/${encodeURIComponent(id)}/unsubscribe`, {});
  }

  deleteCoachingNewsletterSubscriber(id: string) {
    return this.http.delete<void>(`${this.coachingNewsletterAdminUrl}/${encodeURIComponent(id)}`);
  }

  getCmsEntries(kind: CoachingCmsKind, pageNumber = 1, pageSize = 25, search = '', group?: string) {
    let params = new HttpParams().set('kind', kind).set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (group?.trim()) params = params.set('group', group.trim());
    return this.http.get<{ data: CoachingManagementPage<CoachingCmsEntry> }>(`${this.coachingCmsAdminUrl}/entries`, { params })
      .pipe(map(response => response.data));
  }

  createCmsEntry(request: CoachingCmsEntryRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.coachingCmsAdminUrl}/entries`, request);
  }

  updateCmsEntry(id: string, request: CoachingCmsEntryRequest) {
    return this.http.put<void>(`${this.coachingCmsAdminUrl}/entries/${encodeURIComponent(id)}`, request);
  }

  deleteCmsEntry(id: string) {
    return this.http.delete<void>(`${this.coachingCmsAdminUrl}/entries/${encodeURIComponent(id)}`);
  }

  getCmsRevisions(id: string) {
    return this.http.get<{ data: CoachingCmsRevision[] }>(`${this.coachingCmsAdminUrl}/entries/${encodeURIComponent(id)}/revisions`)
      .pipe(map(response => response.data));
  }

  restoreCmsRevision(entryId: string, revisionId: string) {
    return this.http.post<void>(`${this.coachingCmsAdminUrl}/entries/${encodeURIComponent(entryId)}/revisions/${encodeURIComponent(revisionId)}/restore`, {});
  }

  getNavigation(menu = 'Main', includeHidden = true) {
    const params = new HttpParams().set('menu', menu).set('includeHidden', includeHidden);
    return this.http.get<{ data: CoachingCmsNavigationItem[] }>(`${this.coachingCmsAdminUrl}/navigation`, { params })
      .pipe(map(response => response.data));
  }

  createNavigation(request: CoachingCmsNavigationRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.coachingCmsAdminUrl}/navigation`, request);
  }

  updateNavigation(id: string, request: CoachingCmsNavigationRequest) {
    return this.http.put<void>(`${this.coachingCmsAdminUrl}/navigation/${encodeURIComponent(id)}`, request);
  }

  deleteNavigation(id: string) {
    return this.http.delete<void>(`${this.coachingCmsAdminUrl}/navigation/${encodeURIComponent(id)}`);
  }

  getCmsMediaAssets(pageNumber = 1, pageSize = 25) {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<{ data: CoachingManagementPage<CoachingCmsMediaAsset> }>(`${this.coachingCmsAdminUrl}/media`, { params })
      .pipe(map(response => response.data));
  }

  uploadCmsMedia(file: File, altText: string | null) {
    const form = new FormData();
    form.append('file', file, file.name);
    if (altText?.trim()) form.append('altText', altText.trim());
    return this.http.post<{ data: CoachingCmsMediaAsset }>(`${this.coachingCmsAdminUrl}/media`, form)
      .pipe(map(response => response.data));
  }

  deleteCmsMedia(id: string) {
    return this.http.delete<void>(`${this.coachingCmsAdminUrl}/media/${encodeURIComponent(id)}`);
  }

  getPlans(includeInactive = true) {
    const params = new HttpParams().set('includeInactive', includeInactive);
    return this.http.get<{ data: CoachingSubscriptionPlan[] }>(`${this.coachingSubscriptionsAdminUrl}/plans`, { params })
      .pipe(map(response => response.data));
  }

  getPublicSubscriptionPlans() {
    return this.http.get<{ data: CoachingSubscriptionPlan[] }>(`${environment.apiUrl}/coaching/subscription-plans`)
      .pipe(map(response => response.data));
  }

  getPublicCmsBlocks(group = 'HomePage') {
    const params = new HttpParams().set('group', group);
    return this.http.get<{ data: CoachingCmsEntry[] }>(`${environment.apiUrl}/coaching/cms/blocks`, { params })
      .pipe(map(response => response.data));
  }

  getPublicCmsPage(slug: string) {
    return this.http.get<{ data: CoachingCmsEntry }>(`${environment.apiUrl}/coaching/cms/pages/${encodeURIComponent(slug)}`)
      .pipe(map(response => response.data));
  }

  getPublicCmsBlog(pageNumber = 1, pageSize = 10) {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<{ data: CoachingManagementPage<CoachingCmsEntry> }>(`${environment.apiUrl}/coaching/cms/blog`, { params })
      .pipe(map(response => response.data));
  }

  getPublicCmsBlogPost(slug: string) {
    return this.http.get<{ data: CoachingCmsEntry }>(`${environment.apiUrl}/coaching/cms/blog/${encodeURIComponent(slug)}`)
      .pipe(map(response => response.data));
  }

  getPublicCmsNavigation(menu = 'Main') {
    const params = new HttpParams().set('menu', menu);
    return this.http.get<{ data: CoachingCmsNavigationItem[] }>(`${environment.apiUrl}/coaching/cms/navigation`, { params })
      .pipe(map(response => response.data));
  }

  getPublicBankTransferSettings() {
    return this.http.get<{ data: CoachingSubscriptionSettings }>(`${this.coachingPublicSubscriptionUrl}/bank-transfer-settings`)
      .pipe(map(response => response.data));
  }

  getMyCoachingSubscriptionAccess() {
    return this.http.get<{ data: CoachingSubscriptionAccess }>(`${this.coachingPublicSubscriptionUrl}/my-access`)
      .pipe(map(response => response.data));
  }

  getMyCoachingBankTransferRequests() {
    return this.http.get<{ data: CoachingBankTransferRequest[] }>(`${this.coachingPublicSubscriptionUrl}/my-bank-transfer-requests`)
      .pipe(map(response => response.data));
  }

  createMyCoachingBankTransferRequest(request: CoachingBankTransferRequestCreate) {
    return this.http.post<{ data: CoachingBankTransferRequest }>(`${this.coachingPublicSubscriptionUrl}/bank-transfer-requests`, {
      ...request,
      paymentReference: request.paymentReference.trim(),
      payerName: request.payerName?.trim() || null,
      note: request.note?.trim() || null
    }, { headers: this.idempotencyHeaders() }).pipe(map(response => response.data));
  }

  createPlan(request: CoachingSubscriptionPlanRequest) {
    return this.http.post<{ data: { id: string } }>(`${this.coachingSubscriptionsAdminUrl}/plans`, request);
  }

  updatePlan(id: string, request: CoachingSubscriptionPlanRequest) {
    return this.http.put<void>(`${this.coachingSubscriptionsAdminUrl}/plans/${encodeURIComponent(id)}`, request);
  }

  deactivatePlan(id: string) {
    return this.http.delete<void>(`${this.coachingSubscriptionsAdminUrl}/plans/${encodeURIComponent(id)}`);
  }

  getSubscriptionSettings() {
    return this.http.get<{ data: CoachingSubscriptionSettings }>(`${this.coachingSubscriptionsAdminUrl}/settings`)
      .pipe(map(response => response.data));
  }

  updateSubscriptionSettings(request: CoachingSubscriptionSettingsRequest) {
    return this.http.put<{ data: CoachingSubscriptionSettings }>(`${this.coachingSubscriptionsAdminUrl}/settings`, request, { headers: this.idempotencyHeaders() })
      .pipe(map(response => response.data));
  }

  getTransferRequests(pageNumber = 1, pageSize = 25, search = '', status = '') {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<{ data: CoachingManagementPage<CoachingBankTransferRequest> }>(`${this.coachingSubscriptionsAdminUrl}/transfer-requests`, { params })
      .pipe(map(response => response.data));
  }

  reviewTransferRequest(id: string, status: 'Approved' | 'Rejected', reviewNote: string | null) {
    return this.http.post<void>(`${this.coachingSubscriptionsAdminUrl}/transfer-requests/${encodeURIComponent(id)}/review`, { status, reviewNote }, { headers: this.idempotencyHeaders() });
  }

  deleteTransferRequest(id: string) {
    return this.http.delete<void>(`${this.coachingSubscriptionsAdminUrl}/transfer-requests/${encodeURIComponent(id)}`);
  }

  getSubscriptions(pageNumber = 1, pageSize = 25, search = '', status = '') {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<{ data: CoachingManagementPage<CoachingSubscription> }>(this.coachingSubscriptionsAdminUrl, { params })
      .pipe(map(response => response.data));
  }

  createSubscription(request: {
    planId: string; userId: string | null; userName: string | null; userEmail: string | null;
    institutionId: string | null; studentIds: string[]; startDate: string; notes: string | null; paymentReference?: string | null;
  }) {
    return this.http.post<void>(this.coachingSubscriptionsAdminUrl, request);
  }

  updateSubscription(id: string, status: string, endDate: string, notes: string | null) {
    return this.http.put<void>(`${this.coachingSubscriptionsAdminUrl}/${encodeURIComponent(id)}`, { status, endDate, notes });
  }

  getSubscriptionSeats(id: string) {
    return this.http.get<{ data: CoachingSubscriptionSeat[] }>(`${this.coachingSubscriptionsAdminUrl}/${encodeURIComponent(id)}/students`)
      .pipe(map(response => response.data));
  }

  private idempotencyHeaders() {
    const key = globalThis.crypto?.randomUUID?.()
      ?? `coaching-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return new HttpHeaders({ 'Idempotency-Key': key });
  }

  changeStudentSeat(subscriptionId: string, studentId: string, isSuspended: boolean, reason: string | null) {
    return this.http.put<void>(`${this.coachingSubscriptionsAdminUrl}/${encodeURIComponent(subscriptionId)}/students/${encodeURIComponent(studentId)}/suspension`, { isSuspended, reason });
  }

  getPayments(pageNumber = 1, pageSize = 25, search = '', status = '') {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<{ data: CoachingManagementPage<CoachingPayment> }>(`${this.coachingSubscriptionsAdminUrl}/payments`, { params })
      .pipe(map(response => response.data));
  }
}
