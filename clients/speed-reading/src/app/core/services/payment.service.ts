import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface InitPaymentRequest {
    planId: string;
}

export interface InitPaymentResponse {
    token: string;
    paymentPageUrl: string | null;
    checkoutFormContent: string | null;
}

export interface VerifyPaymentResponse {
    success: boolean;
    status: string;
    planName: string | null;
    amount: number;
    subscriptionId: string | null;
}

export interface PaymentSummary {
    id: string;
    userId: string;
    userEmail: string;
    userName: string;
    planName: string;
    amount: number;
    currency: string;
    status: string;
    provider: string;
    providerPaymentId: string | null;
    errorMessage: string | null;
    subscriptionId: string | null;
    createdAt: string;
}

export interface PaymentPagedResult {
    total: number;
    page: number;
    pageSize: number;
    items: PaymentSummary[];
}

@Injectable({ providedIn: 'root' })
export class PaymentService {
    private http   = inject(HttpClient);
    private apiUrl = `${environment.apiUrl}/v1/payment`;

    /** Ödeme başlatır — Iyzico token'ı döner */
    initializePayment(planId: string): Observable<InitPaymentResponse> {
        return this.http.post<InitPaymentResponse>(
            `${this.apiUrl}/initialize`,
            { planId } satisfies InitPaymentRequest
        );
    }

    /** Token ile ödeme durumunu sorgular */
    verifyPayment(token: string): Observable<VerifyPaymentResponse> {
        return this.http.get<VerifyPaymentResponse>(
            `${this.apiUrl}/verify`,
            { params: new HttpParams().set('token', token) }
        );
    }

    /** Admin — tüm ödemeleri listeler */
    getPayments(page = 1, pageSize = 20, status?: string, search?: string): Observable<PaymentPagedResult> {
        let params = new HttpParams()
            .set('page', page)
            .set('pageSize', pageSize);
        if (status) params = params.set('status', status);
        if (search) params = params.set('search', search);
        return this.http.get<PaymentPagedResult>(this.apiUrl, { params });
    }
}
