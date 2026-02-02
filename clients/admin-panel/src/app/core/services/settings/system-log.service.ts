import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Observable } from 'rxjs';

export interface LogEntry {
    timestamp: string;
    level: string;
    message: string;
    exception?: string;
    properties?: string;
    application?: string;
}

export interface PagedLogsResponse {
    logs: LogEntry[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export interface LogFilterRequest {
    level?: string;
    application?: string;
    searchTerm?: string;
    startDate?: string;
    endDate?: string;
    page: number;
    pageSize: number;
}

export interface RetentionPolicy {
    id: string;
    retentionTime: string;
    removedSignalExpression?: string;
    retentionDays: number;
    signalTitle?: string;
}

export interface CreateRetentionPolicyRequest {
    retentionDays: number;
    logLevel?: string;
}

@Injectable({
    providedIn: 'root'
})
export class SystemLogService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.apiUrl}/system-logs`;

    getLogs(filter: LogFilterRequest): Observable<PagedLogsResponse> {
        let params = new HttpParams()
            .set('page', filter.page.toString())
            .set('pageSize', filter.pageSize.toString());

        if (filter.level) params = params.set('level', filter.level);
        if (filter.application) params = params.set('application', filter.application);
        if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
        if (filter.startDate) params = params.set('startDate', filter.startDate);
        if (filter.endDate) params = params.set('endDate', filter.endDate);

        return this.http.get<PagedLogsResponse>(this.apiUrl, { params });
    }

    getApplications(): Observable<string[]> {
        return this.http.get<string[]>(`${this.apiUrl}/applications`);
    }

    getRetentionPolicies(): Observable<RetentionPolicy[]> {
        return this.http.get<RetentionPolicy[]>(`${this.apiUrl}/retention-policies`);
    }

    createRetentionPolicy(request: CreateRetentionPolicyRequest): Observable<RetentionPolicy> {
        return this.http.post<RetentionPolicy>(`${this.apiUrl}/retention-policies`, request);
    }

    deleteRetentionPolicy(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/retention-policies/${id}`);
    }

    getSeqUrl(): Observable<{ url: string }> {
        return this.http.get<{ url: string }>(`${this.apiUrl}/seq-url`);
    }
}
