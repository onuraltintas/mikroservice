import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type BankTransferApproval = 'Manual' | 'Automatic';
export type BuyerType = 'Individual' | 'Teacher' | 'Institution';
export type EntitlementHolder = 'Buyer' | 'SeatHolder';

export interface BillingEntitlement { id: string; key: string; name: string; hasLimit: boolean; }

export interface BillingProduct {
  id: string;
  code: string;
  name: string;
  requireSubscription: boolean;
  bankTransferApproval: BankTransferApproval;
  isActive: boolean;
  sortOrder: number;
  entitlements: BillingEntitlement[];
}

export interface BillingProductRequest { code: string; name: string; isActive: boolean; sortOrder: number; }
export interface BillingProductSettingsRequest { requireSubscription: boolean; bankTransferApproval: BankTransferApproval; }
export interface BillingEntitlementRequest { key: string; name: string; hasLimit: boolean; }

export interface BillingPlanEntitlement {
  entitlementId: string;
  key: string;
  name: string;
  productCode: string;
  holder: EntitlementHolder;
  limitValue: number | null;
}

export interface BillingPlan {
  id: string;
  name: string;
  description: string;
  buyerType: BuyerType;
  price: number;
  currency: string;
  durationDays: number;
  trialDays: number;
  seatCount: number | null;
  extraSeatPrice: number | null;
  isContactOnly: boolean;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string[];
  entitlements: BillingPlanEntitlement[];
  productCodes: string[];
}

export interface BillingPlanRequest {
  name: string;
  description: string | null;
  buyerType: BuyerType;
  price: number;
  durationDays: number;
  trialDays: number;
  seatCount: number | null;
  extraSeatPrice: number | null;
  isContactOnly: boolean;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string[];
  entitlements: { entitlementId: string; holder: EntitlementHolder; limitValue: number | null }[];
}

export interface BillingSeller {
  title: string | null;
  address: string | null;
  taxOffice: string | null;
  taxNumber: string | null;
  mersisNumber: string | null;
  email: string | null;
  phone: string | null;
  alertEmail: string | null;
  isComplete: boolean;
}

export type BillingSellerRequest = Omit<BillingSeller, 'isComplete'>;

export interface BillingBankAccount {
  id: string;
  bankName: string;
  accountHolder: string;
  iban: string;
  ibanFormatted: string;
  instructions: string | null;
  statementTemplate: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface BillingBankAccountRequest {
  bankName: string;
  accountHolder: string;
  iban: string;
  instructions: string | null;
  statementTemplate: string | null;
  isActive: boolean;
  sortOrder: number;
}

export type BillingOrderStatus = 'Pending' | 'Paid' | 'Rejected' | 'Expired' | 'Withdrawn' | 'Cancelled';

export interface BillingOrder {
  id: string;
  planId: string;
  planName: string;
  productCodes: string[];
  buyerType: BuyerType;
  amount: number;
  currency: string;
  durationDays: number;
  paymentCode: string;
  status: BillingOrderStatus;
  createdAt: string;
  expiresAt: string;
  paidDeclaredAt: string | null;
  payerNote: string | null;
  hasReceipt: boolean;
  reviewedAt: string | null;
  reviewNote: string | null;
  documentsVersion: string;
}

export interface BillingAdminOrder {
  order: BillingOrder;
  accountName: string;
  accountEmail: string | null;
  orderedByUserId: string;
  beneficiaryUserId: string;
  reviewedBy: string | null;
}

export interface BillingSubscription {
  id: string;
  planId: string;
  planName: string;
  productCodes: string[];
  status: 'Active' | 'Cancelled';
  startsAt: string;
  endsAt: string;
  isCurrent: boolean;
  seatCount: number | null;
}

export interface BillingAdminSubscription {
  subscription: BillingSubscription;
  accountName: string;
  accountEmail: string | null;
  beneficiaryUserId: string;
}

export const ORDER_STATUS_LABELS: Record<BillingOrderStatus, string> = {
  Pending: 'Ödeme bekliyor', Paid: 'Onaylandı', Rejected: 'Reddedildi', Expired: 'Süresi doldu', Withdrawn: 'Geri çekildi', Cancelled: 'Onay geri alındı'
};

export type BankTransactionStatus = 'Unmatched' | 'Matched' | 'Approved' | 'Ignored';

export interface StatementMapping {
  headerRow: number;
  dateColumn: number;
  descriptionColumn: number;
  amountColumn: number;
  senderColumn: number | null;
  referenceColumn: number | null;
  directionColumn: number | null;
  incomingValue: string | null;
}

export interface StatementPreview { rows: string[][]; totalRows: number; suggested: StatementMapping | null; }

export interface BankTransaction {
  id: string;
  bankAccountId: string;
  bankName: string;
  bookedAt: string;
  amount: number;
  description: string;
  sender: string | null;
  paymentCode: string | null;
  status: BankTransactionStatus;
  orderId: string | null;
  orderSummary: string | null;
  note: string | null;
}

export interface StatementImport {
  id: string;
  bankAccountId: string;
  bankName: string;
  fileName: string;
  uploadedAt: string;
  rowCount: number;
  addedCount: number;
  duplicateCount: number;
  matchedCount: number;
  autoApprovedCount: number;
}

export interface StatementImportResult { import: StatementImport; transactions: BankTransaction[]; }

export const BANK_TRANSACTION_STATUS_LABELS: Record<BankTransactionStatus, string> = {
  Unmatched: 'Eşleşmedi', Matched: 'Eşleşti, onay bekliyor', Approved: 'Ödeme onaylandı', Ignored: 'Yok sayıldı'
};

export interface BillingSeat { userId: string; isSuspended: boolean; assignedAt: string; isCovered: boolean; }

export interface BillingSeatPool {
  subscriptionId: string;
  planName: string;
  endsAt: string;
  capacity: number;
  used: number;
  extraSeats: number;
  extraSeatsUntil: string | null;
  extraSeatPrice: number | null;
  seats: BillingSeat[];
}

export const BUYER_TYPE_LABELS: Record<BuyerType, string> = { Individual: 'Bireysel', Teacher: 'Öğretmen', Institution: 'Kurum' };

/** The server explains a refused change in the admin's language; anything else gets the fallback. */
export function billingErrorMessage(error: unknown, fallback: string): string {
  const detail = error instanceof HttpErrorResponse && error.status === 400 ? error.error?.detail : null;
  return typeof detail === 'string' && detail.trim() ? detail : fallback;
}

/** The admin section "Abonelik ve Ödeme": catalog and settings of the billing service (stage 1). */
@Injectable({ providedIn: 'root' })
export class BillingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/billing/v1/admin`;

  getProducts(): Observable<BillingProduct[]> { return this.http.get<BillingProduct[]>(`${this.url}/products`); }
  createProduct(request: BillingProductRequest): Observable<BillingProduct> { return this.http.post<BillingProduct>(`${this.url}/products`, request); }
  updateProduct(id: string, request: BillingProductRequest): Observable<BillingProduct> { return this.http.put<BillingProduct>(`${this.url}/products/${id}`, request); }
  updateProductSettings(id: string, request: BillingProductSettingsRequest): Observable<BillingProduct> {
    return this.http.put<BillingProduct>(`${this.url}/products/${id}/settings`, request);
  }
  addEntitlement(productId: string, request: BillingEntitlementRequest): Observable<BillingProduct> {
    return this.http.post<BillingProduct>(`${this.url}/products/${productId}/entitlements`, request);
  }
  updateEntitlement(id: string, request: BillingEntitlementRequest): Observable<BillingProduct> {
    return this.http.put<BillingProduct>(`${this.url}/entitlements/${id}`, request);
  }
  removeEntitlement(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/entitlements/${id}`); }

  getPlans(): Observable<BillingPlan[]> { return this.http.get<BillingPlan[]>(`${this.url}/plans`); }
  createPlan(request: BillingPlanRequest): Observable<BillingPlan> { return this.http.post<BillingPlan>(`${this.url}/plans`, request); }
  updatePlan(id: string, request: BillingPlanRequest): Observable<BillingPlan> { return this.http.put<BillingPlan>(`${this.url}/plans/${id}`, request); }

  getOrders(status: BillingOrderStatus | '', productCode: string): Observable<BillingAdminOrder[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (productCode) params = params.set('product', productCode);
    return this.http.get<BillingAdminOrder[]>(`${this.url}/orders`, { params });
  }
  approveOrder(id: string): Observable<BillingAdminOrder> { return this.http.post<BillingAdminOrder>(`${this.url}/orders/${id}/approve`, {}); }
  rejectOrder(id: string, note: string): Observable<BillingAdminOrder> { return this.http.post<BillingAdminOrder>(`${this.url}/orders/${id}/reject`, { note }); }
  reverseApproval(id: string, note: string): Observable<BillingAdminOrder> { return this.http.post<BillingAdminOrder>(`${this.url}/orders/${id}/reverse`, { note }); }
  getReceipt(id: string): Observable<Blob> { return this.http.get(`${this.url}/orders/${id}/receipt`, { responseType: 'blob' }); }
  getSubscriptions(productCode: string): Observable<BillingAdminSubscription[]> {
    return this.http.get<BillingAdminSubscription[]>(`${this.url}/subscriptions`, { params: productCode ? new HttpParams().set('product', productCode) : undefined });
  }

  getSeats(subscriptionId: string): Observable<BillingSeatPool> { return this.http.get<BillingSeatPool>(`${this.url}/subscriptions/${subscriptionId}/seats`); }
  releaseSeat(subscriptionId: string, userId: string): Observable<BillingSeatPool> {
    return this.http.post<BillingSeatPool>(`${this.url}/subscriptions/${subscriptionId}/seats/${userId}/release`, {});
  }

  previewStatement(file: File): Observable<StatementPreview> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<StatementPreview>(`${this.url}/bank-statements/preview`, form);
  }
  importStatement(file: File, bankAccountId: string, mapping: StatementMapping): Observable<StatementImportResult> {
    const form = new FormData();
    form.append('file', file);
    form.append('bankAccountId', bankAccountId);
    for (const [key, value] of Object.entries(mapping)) if (value !== null && value !== '') form.append(key, String(value));
    return this.http.post<StatementImportResult>(`${this.url}/bank-statements`, form);
  }
  getStatementImports(): Observable<StatementImport[]> { return this.http.get<StatementImport[]>(`${this.url}/bank-statements`); }
  getBankTransactions(status: BankTransactionStatus | ''): Observable<BankTransaction[]> {
    return this.http.get<BankTransaction[]>(`${this.url}/bank-transactions`, { params: status ? new HttpParams().set('status', status) : undefined });
  }
  approveBankTransaction(id: string, orderId: string): Observable<BankTransaction> {
    return this.http.post<BankTransaction>(`${this.url}/bank-transactions/${id}/approve`, { orderId });
  }
  ignoreBankTransaction(id: string): Observable<BankTransaction> { return this.http.post<BankTransaction>(`${this.url}/bank-transactions/${id}/ignore`, {}); }
  restoreBankTransaction(id: string): Observable<BankTransaction> { return this.http.post<BankTransaction>(`${this.url}/bank-transactions/${id}/restore`, {}); }

  getSeller(): Observable<BillingSeller> { return this.http.get<BillingSeller>(`${this.url}/seller`); }
  updateSeller(request: BillingSellerRequest): Observable<BillingSeller> { return this.http.put<BillingSeller>(`${this.url}/seller`, request); }

  getBankAccounts(): Observable<BillingBankAccount[]> { return this.http.get<BillingBankAccount[]>(`${this.url}/bank-accounts`); }
  createBankAccount(request: BillingBankAccountRequest): Observable<BillingBankAccount> {
    return this.http.post<BillingBankAccount>(`${this.url}/bank-accounts`, request);
  }
  updateBankAccount(id: string, request: BillingBankAccountRequest): Observable<BillingBankAccount> {
    return this.http.put<BillingBankAccount>(`${this.url}/bank-accounts/${id}`, request);
  }
  removeBankAccount(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/bank-accounts/${id}`); }
}
