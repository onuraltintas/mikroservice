import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  AuditLog,
  AuditLogListResponse,
  AuditLogFilters
} from '../models/audit-log.model';

interface AdminAuditRecord {
  id: string;
  occurredAt: string;
  serviceName: string;
  actorUserId: string;
  actorRoles: string;
  tenantId: string | null;
  httpMethod: string;
  path: string;
  statusCode: number;
  correlationId: string;
  clientIp: string | null;
  userAgent: string | null;
  action: string | null;
  resourceType: string | null;
  resourceId: string | null;
  changedFieldsJson: string | null;
}

interface AdminAuditPage {
  items: AdminAuditRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuditLogsService {
  private readonly http = inject(HttpClient);
  private readonly auditUrls = [
    `${environment.apiUrl}/admin-audit/identity`,
    `${environment.apiUrl}/admin-audit/coaching`,
    `${environment.apiUrl}/admin-audit/notification`
  ];

  getAuditLogs(filters?: AuditLogFilters): Observable<AuditLogListResponse> {
    const pageNumber = filters?.pageNumber ?? 1;
    const pageSize = Math.min(filters?.pageSize ?? 50, 100);
    let params = new HttpParams().set('page', '1').set('pageSize', '100');

    if (filters?.userId) params = params.set('search', filters.userId);
    if (filters?.startDate) params = params.set('from', filters.startDate.toISOString());
    if (filters?.endDate) params = params.set('to', filters.endDate.toISOString());

    return forkJoin(this.auditUrls.map(url => this.getAllPages(url, params))).pipe(
      map(pageGroups => {
        const pages = pageGroups.flat();
        const allLogs = pages
          .flatMap(page => page.items ?? [])
          .map(record => this.toAuditLog(record))
          .filter(log => !filters?.action || log.action === filters.action)
          .filter(log => !filters?.entityType || log.entityType === filters.entityType)
          .sort((left, right) => right.timestamp.getTime() - left.timestamp.getTime());
        const totalCount = filters?.action || filters?.entityType
          ? allLogs.length
          : pages.reduce((total, page) => total + (page.totalCount ?? 0), 0);
        const start = (pageNumber - 1) * pageSize;

        return {
          logs: allLogs.slice(start, start + pageSize),
          totalCount,
          pageNumber,
          pageSize,
          totalPages: Math.ceil(totalCount / pageSize)
        };
      })
    );
  }

  exportAuditLogs(filters?: AuditLogFilters): Observable<Blob> {
    return this.getAuditLogs({ ...filters, pageNumber: 1, pageSize: 100 }).pipe(
      map(result => {
        const header = ['Tarih', 'Servis', 'Kullanıcı', 'Aksiyon', 'Varlık', 'Varlık ID', 'HTTP', 'Durum', 'IP', 'İstek'];
        const rows = result.logs.map(log => [
          log.timestamp.toISOString(),
          log.serviceName ?? '',
          log.userId,
          log.action,
          log.entityType ?? '',
          log.entityId ?? '',
          log.httpMethod ?? '',
          String(log.statusCode ?? ''),
          log.ipAddress,
          log.requestPath ?? ''
        ]);
        const csv = [header, ...rows]
          .map(row => row.map(value => this.csvValue(value)).join(','))
          .join('\r\n');
        return new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' });
      })
    );
  }

  private toAuditLog(record: AdminAuditRecord): AuditLog {
    const action = record.action ?? record.httpMethod;
    return {
      id: record.id,
      userId: record.actorUserId,
      userName: record.actorUserId,
      userEmail: '',
      action,
      entityType: record.resourceType,
      entityId: record.resourceId,
      entityName: record.resourceType && record.resourceId
        ? `${record.resourceType}/${record.resourceId}`
        : record.resourceType,
      ipAddress: record.clientIp ?? '',
      timestamp: new Date(record.occurredAt),
      serviceName: record.serviceName,
      httpMethod: record.httpMethod,
      statusCode: record.statusCode,
      requestPath: record.path,
      correlationId: record.correlationId,
      userAgent: record.userAgent ?? '',
      changedFieldsJson: record.changedFieldsJson
    };
  }

  private getAllPages(url: string, params: HttpParams): Observable<AdminAuditPage[]> {
    return this.http.get<AdminAuditPage>(url, { params }).pipe(
      switchMap(firstPage => {
        const pageCount = Math.min(Math.ceil((firstPage.totalCount ?? 0) / 100), 1000);
        const remainingPages = Array.from({ length: Math.max(0, pageCount - 1) }, (_, index) =>
          this.http.get<AdminAuditPage>(url, {
            params: params.set('page', String(index + 2))
          }).pipe(
            // A service can legitimately have no audit store in a local/staged
            // deployment. Keep the combined admin screen usable with the other
            // service streams in that case.
            catchError(() => of({ items: [], totalCount: 0, page: index + 2, pageSize: 100 }))
          ));
        return forkJoin([of(firstPage), ...remainingPages]);
      }),
      catchError(() => of([{ items: [], totalCount: 0, page: 1, pageSize: 100 }]))
    );
  }

  private csvValue(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
  }
}
