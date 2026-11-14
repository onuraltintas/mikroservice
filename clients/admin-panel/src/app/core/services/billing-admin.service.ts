import { HttpClient, HttpErrorResponse } from '@angular/common/http';
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
