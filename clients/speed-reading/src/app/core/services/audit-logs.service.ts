import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuditLog,
  AuditLogDetail,
  AuditLogListResponse,
  AuditLogFilters,
  AuditLogStats
} from '../models/audit-log.model';

@Injectable({
  providedIn: 'root'
})
export class AuditLogsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/audit-logs`;

  getAuditLogs(filters?: AuditLogFilters): Observable<AuditLogListResponse> {
    let params = new HttpParams();

    if (filters?.pageNumber) {
      params = params.set('pageNumber', filters.pageNumber.toString());
    }
    if (filters?.pageSize) {
      params = params.set('pageSize', filters.pageSize.toString());
    }
    if (filters?.userId) {
      params = params.set('userId', filters.userId);
    }
    if (filters?.action) {
      params = params.set('action', filters.action);
    }
    if (filters?.entityType) {
      params = params.set('entityType', filters.entityType);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate.toISOString());
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate.toISOString());
    }

    return this.http.get<AuditLogListResponse>(this.API_URL, { params });
  }

  getAuditLogDetail(id: string): Observable<AuditLogDetail> {
    return this.http.get<AuditLogDetail>(`${this.API_URL}/${id}`);
  }

  getActions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/actions`);
  }

  getEntityTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.API_URL}/entity-types`);
  }

  getStats(startDate?: Date, endDate?: Date): Observable<AuditLogStats> {
    let params = new HttpParams();

    if (startDate) {
      params = params.set('startDate', startDate.toISOString());
    }
    if (endDate) {
      params = params.set('endDate', endDate.toISOString());
    }

    return this.http.get<AuditLogStats>(`${this.API_URL}/stats`, { params });
  }

  exportAuditLogs(filters?: AuditLogFilters): Observable<Blob> {
    let params = new HttpParams();

    if (filters?.userId) {
      params = params.set('userId', filters.userId);
    }
    if (filters?.action) {
      params = params.set('action', filters.action);
    }
    if (filters?.entityType) {
      params = params.set('entityType', filters.entityType);
    }
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate.toISOString());
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate.toISOString());
    }

    return this.http.get(`${this.API_URL}/export`, {
      params,
      responseType: 'blob'
    });
  }
}
