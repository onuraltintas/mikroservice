import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherSubscriptionPlan {
  id: string;
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
}

export interface CoachingTeacherBankTransferSettings {
  currency: string;
  accountHolder: string | null;
  bankName: string | null;
  iban: string | null;
  paymentInstructions: string | null;
  bankTransferEnabled: boolean;
  isPubliclyAvailable: boolean;
}

export interface CoachingTeacherBankTransferRequest {
  id: string;
  plan: CoachingTeacherSubscriptionPlan;
  amount: number;
  currency: string;
  paymentReference: string;
  status: string;
  reviewNote: string | null;
  createdAt: string;
}

export interface CoachingTeacherSubscriptionSeat {
  studentId: string;
  isSuspended: boolean;
  suspensionReason: string | null;
  assignedAt: string;
}

export interface CoachingTeacherSeatSummary {
  subscriptionId: string;
  planName: string;
  accessUntil: string;
  includedStudentSeats: number;
  usedStudentSeats: number;
  students: CoachingTeacherSubscriptionSeat[];
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherSubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly subscriptionsUrl = `${environment.apiUrl}/coaching/subscriptions`;

  getTeacherPlans() {
    return this.http.get<{ data: CoachingTeacherSubscriptionPlan[] }>(`${environment.apiUrl}/coaching/subscription-plans`)
      .pipe(map(response => response.data.filter(plan => plan.audience === 'Teacher' && plan.isActive && plan.isPublic)));
  }

  getBankTransferSettings() {
    return this.http.get<{ data: CoachingTeacherBankTransferSettings }>(`${this.subscriptionsUrl}/bank-transfer-settings`)
      .pipe(map(response => response.data));
  }

  getMyBankTransferRequests() {
    return this.http.get<{ data: CoachingTeacherBankTransferRequest[] }>(`${this.subscriptionsUrl}/my-bank-transfer-requests`)
      .pipe(map(response => response.data.filter(request => request.plan.audience === 'Teacher')));
  }

  createBankTransferRequest(request: { planId: string; paymentReference: string; payerName: string | null; note: string | null }, idempotencyKey?: string) {
    return this.http.post<{ data: CoachingTeacherBankTransferRequest }>(`${this.subscriptionsUrl}/teacher-bank-transfer-requests`, {
      ...request,
      paymentReference: request.paymentReference.trim(),
      payerName: request.payerName?.trim() || null,
      note: request.note?.trim() || null
    }, { headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey ?? this.createIdempotencyKey() }) })
      .pipe(map(response => response.data));
  }

  getMySeatSummary() {
    return this.http.get<{ data: CoachingTeacherSeatSummary | null }>(`${this.subscriptionsUrl}/my-teacher-subscription`)
      .pipe(map(response => response.data));
  }

  assignStudent(studentId: string) {
    return this.http.put<void>(`${this.subscriptionsUrl}/my-teacher-subscription/students/${encodeURIComponent(studentId)}`, {});
  }

  removeStudent(studentId: string) {
    return this.http.delete<void>(`${this.subscriptionsUrl}/my-teacher-subscription/students/${encodeURIComponent(studentId)}`);
  }

  private createIdempotencyKey() {
    return globalThis.crypto?.randomUUID?.()
      ?? `coaching-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
