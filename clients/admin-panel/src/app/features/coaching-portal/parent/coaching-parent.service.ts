import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingBankTransferRequest, CoachingBankTransferRequestCreate, CoachingSubscriptionAccess } from '../../../core/services/coaching-management.service';
import { CoachingSession, ExamResult, StudentAssignment, StudentProgressSummary } from '../../../core/services/coaching-portal.service';

export type ParentRelationship = 'Mother' | 'Father' | 'Guardian';

/** A parent–student link as both sides see it (roadmap Y7). */
export interface ParentLink {
  id: string;
  parentUserId: string;
  parentName: string;
  studentUserId: string;
  studentName: string;
  relationship: ParentRelationship | 'Other';
  status: 'Pending' | 'Verified';
  requestedAt: string;
  verifiedAt?: string | null;
}

export interface ParentLinkCode { code: string; expiresAt: string }

export interface ParentChildPage<T> { items: T[]; totalCount?: number }

export interface ParentFeeAccount {
  totalAmount?: number; paidAmount?: number; balance: number; overdueAmount: number;
  agreements: { id: string; title: string; totalAmount: number; balance?: number; status?: string }[];
}

/** Parent panel and the student's parent-link screen. The API decides who may read what. */
@Injectable({ providedIn: 'root' })
export class CoachingParentService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;
  private readonly links = `${environment.apiUrl}/users/me/parent-links`;

  getLinks(): Observable<ParentLink[]> { return this.http.get<ParentLink[]>(this.links); }
  requestLink(code: string, relationship: ParentRelationship): Observable<ParentLink> {
    return this.http.post<ParentLink>(this.links, { code: code.trim().toUpperCase(), relationship });
  }
  approveLink(id: string): Observable<void> { return this.http.post<void>(`${this.links}/${encodeURIComponent(id)}/approve`, {}); }
  revokeLink(id: string): Observable<void> { return this.http.post<void>(`${this.links}/${encodeURIComponent(id)}/revoke`, {}); }
  createCode(): Observable<ParentLinkCode> { return this.http.post<ParentLinkCode>(`${this.api}/users/me/parent-link-codes`, {}); }

  getProgress(studentId: string): Observable<StudentProgressSummary> {
    return this.http.get<StudentProgressSummary>(`${this.api}/reports/student/${encodeURIComponent(studentId)}/progress`);
  }
  getExams(studentId: string): Observable<ParentChildPage<ExamResult>> { return this.page<ExamResult>(`exams/student/${encodeURIComponent(studentId)}`); }
  getAssignments(studentId: string): Observable<ParentChildPage<StudentAssignment>> { return this.page<StudentAssignment>(`assignments/student/${encodeURIComponent(studentId)}`); }
  getSessions(studentId: string): Observable<ParentChildPage<CoachingSession>> { return this.page<CoachingSession>(`sessions/student/${encodeURIComponent(studentId)}`); }
  getFees(studentId: string): Observable<ParentFeeAccount> {
    return this.http.get<{ data: ParentFeeAccount }>(`${this.api}/coaching/finance/children/${encodeURIComponent(studentId)}`).pipe(map(r => r.data));
  }
  getAccess(studentId: string): Observable<CoachingSubscriptionAccess> {
    return this.http.get<{ data: CoachingSubscriptionAccess }>(`${this.child(studentId)}/access`).pipe(map(r => r.data));
  }
  getTransfers(studentId: string): Observable<CoachingBankTransferRequest[]> {
    return this.http.get<{ data: CoachingBankTransferRequest[] }>(`${this.child(studentId)}/bank-transfer-requests`).pipe(map(r => r.data));
  }
  /** A notice for the child; reCAPTCHA is added by the auth interceptor. */
  sendTransfer(studentId: string, request: CoachingBankTransferRequestCreate): Observable<CoachingBankTransferRequest> {
    return this.http.post<{ data: CoachingBankTransferRequest }>(`${this.child(studentId)}/bank-transfer-requests`, {
      ...request, paymentReference: request.paymentReference.trim(), payerName: request.payerName?.trim() || null, note: request.note?.trim() || null
    }, { headers: new HttpHeaders({ 'Idempotency-Key': globalThis.crypto?.randomUUID?.() ?? `parent-${Date.now()}-${Math.random().toString(36).slice(2)}` }) })
      .pipe(map(r => r.data));
  }

  private child(studentId: string): string { return `${this.api}/coaching/subscriptions/children/${encodeURIComponent(studentId)}`; }
  private page<T>(path: string): Observable<ParentChildPage<T>> {
    return this.http.get<ParentChildPage<T>>(`${this.api}/${path}`, { params: new HttpParams().set('pageNumber', 1).set('pageSize', 10) });
  }
}
