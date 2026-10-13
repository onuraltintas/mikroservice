import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export type CoachingFinanceMode = 'student' | 'staff' | 'admin';
export type CoachingPaymentMethod = 'Cash' | 'BankTransfer';
export interface CoachingFeeInstallment { id: string; sequence: number; dueOn: string; amount: number; balance: number; isOverdue: boolean }
export interface CoachingFee {
  id: string; studentId: string; institutionId: string | null; coachId: string | null; version: number; title: string; currency: string;
  totalAmount: number; paidAmount: number; balance: number; overdueAmount: number; isCancelled: boolean;
  cancellationReason: string | null; cancelledAt: string | null; installments: CoachingFeeInstallment[];
}
export interface CoachingFeeAccount {
  totalAmount: number; paidAmount: number; balance: number; overdueAmount: number; canManage: boolean; canCreate: boolean;
  totalCount: number; pageNumber: number; pageSize: number; agreements: CoachingFee[];
}
export interface CoachingFeePayment {
  id: string; agreementId: string; studentId: string; recordedByUserId: string; reversalOfPaymentId: string | null;
  method: CoachingPaymentMethod; amount: number; receivedOn: string; reference: string | null; createdAt: string;
}
export interface CoachingPaymentPage { items: CoachingFeePayment[]; totalCount: number; pageNumber: number; pageSize: number }
export interface CoachingCashStatement extends CoachingPaymentPage { cashTotal: number; bankTransferTotal: number }
export interface CoachingReceivables { balance: number; overdueAmount: number; items: CoachingFee[]; totalCount: number; pageNumber: number; pageSize: number }
export interface CoachingFeeCreateInput { requestId: string; title: string; totalAmount: number; installments: { dueOn: string; amount: number }[] }
export interface CoachingFeePaymentInput { requestId: string; expectedVersion: number; amount: number; method: CoachingPaymentMethod; receivedOn: string; reference: string | null }
export interface CoachingFeeReverseInput { requestId: string; expectedVersion: number; receivedOn: string; reason: string }
export interface CoachingFeeCancelInput { expectedVersion: number; reason: string }
interface ApiResult<T> { success: boolean; data: T }

@Injectable({ providedIn: 'root' })
export class CoachingFinanceService {
  private readonly http = inject(HttpClient);
  private root(mode: CoachingFinanceMode) { return `${environment.apiUrl}/${mode === 'admin' ? 'coaching-admin' : 'coaching'}/finance`; }
  private accountUrl(mode: CoachingFinanceMode, studentId: string) {
    return `${this.root(mode)}/${mode === 'student' ? 'me' : `students/${encodeURIComponent(studentId)}`}`;
  }
  private page(pageNumber: number) { return new HttpParams().set('pageNumber', pageNumber).set('pageSize', 25); }
  account(mode: CoachingFinanceMode, studentId: string, pageNumber = 1) {
    return this.http.get<ApiResult<CoachingFeeAccount>>(this.accountUrl(mode, studentId), { params: this.page(pageNumber) }).pipe(map(result => result.data));
  }
  payments(mode: CoachingFinanceMode, studentId: string, feeId: string, pageNumber = 1) {
    return this.http.get<ApiResult<CoachingPaymentPage>>(`${this.accountUrl(mode, studentId)}/fees/${encodeURIComponent(feeId)}/payments`, { params: this.page(pageNumber) }).pipe(map(result => result.data));
  }
  cash(mode: 'staff' | 'admin', fromDate: string, toDate: string, pageNumber = 1) {
    const params = new HttpParams().set('fromDate', fromDate).set('toDate', toDate).set('pageNumber', pageNumber).set('pageSize', 25);
    return this.http.get<ApiResult<CoachingCashStatement>>(`${this.root(mode)}/cash`, { params }).pipe(map(result => result.data));
  }
  receivables(mode: 'staff' | 'admin', pageNumber = 1) {
    return this.http.get<ApiResult<CoachingReceivables>>(`${this.root(mode)}/receivables`, { params: this.page(pageNumber) }).pipe(map(result => result.data));
  }
  create(studentId: string, input: CoachingFeeCreateInput) {
    return this.http.post<ApiResult<CoachingFee>>(`${this.accountUrl('staff', studentId)}/fees`, input).pipe(map(result => result.data));
  }
  pay(studentId: string, feeId: string, input: CoachingFeePaymentInput) {
    return this.http.post<ApiResult<CoachingFee>>(`${this.accountUrl('staff', studentId)}/fees/${encodeURIComponent(feeId)}/payments`, input).pipe(map(result => result.data));
  }
  reverse(studentId: string, feeId: string, paymentId: string, input: CoachingFeeReverseInput) {
    return this.http.post<ApiResult<CoachingFee>>(`${this.accountUrl('staff', studentId)}/fees/${encodeURIComponent(feeId)}/payments/${encodeURIComponent(paymentId)}/reverse`, input).pipe(map(result => result.data));
  }
  cancel(studentId: string, feeId: string, input: CoachingFeeCancelInput) {
    return this.http.post<ApiResult<CoachingFee>>(`${this.accountUrl('staff', studentId)}/fees/${encodeURIComponent(feeId)}/cancel`, input).pipe(map(result => result.data));
  }
}
