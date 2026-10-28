import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

/** Roadmap O10: free trial, discount codes, sales documents, withdrawal (cayma) and invoices for a buyer. */
export interface CoachingTrialSummary { subscriptionId: string; planId: string; planName: string; startDate: string; endDate: string }
export interface CoachingCouponQuote { code: string; listPrice: number; discount: number; amount: number }
export interface CoachingSalesDocuments { version: string; preInformation: string; distanceSales: string }
export interface CoachingBuyerPayment {
  id: string; transferId: string | null; planName: string; amount: number; currency: string; status: string; createdAt: string;
  listPrice: number | null; couponCode: string | null; discountAmount: number; invoiceNumber: string | null; invoiceIssuedOn: string | null;
  withdrawalStatus: string | null; withdrawUntil: string | null; hasSalesDocuments: boolean;
}

/** What the checkout adds to a bank-transfer notice. */
export interface CoachingCheckoutChoice { couponCode: string | null; acceptSalesContract: boolean }

const moneyFormat = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
/** 1234.5 → "1.234,50" whatever locale the app registered. */
export const coachingMoney = (value: number | null | undefined) => moneyFormat.format(value ?? 0);

export const COACHING_COUPON_PATTERN = /^[A-Z0-9-]{3,30}$/;
export const normalizeCoachingCoupon = (value: string | null | undefined) => (value ?? '').trim().toLocaleUpperCase('tr-TR')
  .replace(/İ/g, 'I').replace(/\s+/g, '');

export const coachingWithdrawalLabel = (status: string | null | undefined) =>
  ({ Requested: 'Cayma talebi inceleniyor', Approved: 'Cayma onaylandı', Rejected: 'Cayma talebi reddedildi' } as Record<string, string>)[status ?? ''] ?? null;

/** The buyer side of the sales API; `apiUrl` is the application's API root (for example `/api`). */
export class CoachingSalesApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/coaching/subscriptions`; }

  startTrial(planId: string, audience?: 'Individual' | 'Teacher'): Observable<CoachingTrialSummary> {
    return this.http.post<{ data: CoachingTrialSummary }>(`${this.url}/trials`, audience ? { planId, audience } : { planId }).pipe(map(r => r.data));
  }
  quoteCoupon(code: string, planId: string): Observable<CoachingCouponQuote> {
    return this.http.get<{ data: CoachingCouponQuote }>(`${this.url}/coupons/${encodeURIComponent(normalizeCoachingCoupon(code))}`,
      { params: new HttpParams().set('planId', planId) }).pipe(map(r => r.data));
  }
  documents(planId: string, couponCode?: string | null): Observable<CoachingSalesDocuments> {
    let params = new HttpParams().set('planId', planId);
    if (couponCode) params = params.set('couponCode', couponCode);
    return this.http.get<{ data: CoachingSalesDocuments }>(`${this.url}/sales-documents`, { params }).pipe(map(r => r.data));
  }
  acceptedDocuments(transferId: string): Observable<CoachingSalesDocuments> {
    return this.http.get<{ data: CoachingSalesDocuments }>(`${this.url}/my-bank-transfer-requests/${encodeURIComponent(transferId)}/sales-documents`).pipe(map(r => r.data));
  }
  myPayments(): Observable<CoachingBuyerPayment[]> {
    return this.http.get<{ data: CoachingBuyerPayment[] }>(`${this.url}/my-payments`).pipe(map(r => r.data));
  }
  requestWithdrawal(transferId: string, reason: string | null): Observable<CoachingBuyerPayment> {
    return this.http.post<{ data: CoachingBuyerPayment }>(`${this.url}/my-bank-transfer-requests/${encodeURIComponent(transferId)}/withdrawal`,
      { reason: reason?.trim() || null }).pipe(map(r => r.data));
  }
  invoice(paymentId: string): Observable<Blob> {
    return this.http.get(`${this.url}/my-payments/${encodeURIComponent(paymentId)}/invoice`, { responseType: 'blob' });
  }
}

export function coachingErrorMessage(response: unknown, fallback: string): string {
  const message = (response as { error?: { message?: unknown } } | null)?.error?.message;
  return typeof message === 'string' && message.trim() ? message : fallback;
}
