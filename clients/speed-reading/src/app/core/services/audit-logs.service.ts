import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, from, of } from 'rxjs';
import { concatMap, map, switchMap, toArray } from 'rxjs/operators';
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
    if (filters?.endDate) {
      const endOfDay = new Date(filters.endDate);
      endOfDay.setHours(23, 59, 59, 999);
      params = params.set('to', endOfDay.toISOString());
    }

    const fetchAll = !!filters?.action || !!filters?.entityType;
    return this.loadAuditLogs(filters, params, fetchAll ? undefined : pageNumber * pageSize).pipe(
      map(result => ({
        logs: result.logs.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
        totalCount: result.totalCount,
        pageNumber,
        pageSize,
        totalPages: Math.ceil(result.totalCount / pageSize)
      }))
    );
  }

  getAuditFilterOptions(): Observable<{ actions: string[]; entityTypes: string[] }> {
    const params = new HttpParams().set('page', '1').set('pageSize', '100');
    return this.loadAuditLogs(undefined, params, undefined).pipe(
      map(result => ({
        actions: [...new Set(result.logs.map(log => log.action))].sort(),
        entityTypes: [...new Set(result.logs.map(log => log.entityType).filter((type): type is string => !!type))].sort()
      }))
    );
  }

  exportAuditLogs(filters?: AuditLogFilters): Observable<Blob> {
    let params = new HttpParams().set('page', '1').set('pageSize', '100');
    if (filters?.userId) params = params.set('search', filters.userId);
    if (filters?.startDate) params = params.set('from', filters.startDate.toISOString());
    if (filters?.endDate) {
      const endOfDay = new Date(filters.endDate);
      endOfDay.setHours(23, 59, 59, 999);
      params = params.set('to', endOfDay.toISOString());
    }

    return this.loadAuditLogs(filters, params, undefined).pipe(
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

  private loadAuditLogs(
    filters: AuditLogFilters | undefined,
    params: HttpParams,
    recordLimit: number | undefined
  ): Observable<{ logs: AuditLog[]; totalCount: number }> {
    return forkJoin(this.auditUrls.map(url => this.getPages(url, params, recordLimit))).pipe(
      map(pageGroups => {
        const allLogs = pageGroups.flat()
          .flatMap(page => page.items ?? [])
          .map(record => this.toAuditLog(record))
          .filter(log => !filters?.action || log.action.toLowerCase() === filters.action.toLowerCase())
          .filter(log => !filters?.entityType || log.entityType === filters.entityType)
          .sort((left, right) => right.timestamp.getTime() - left.timestamp.getTime());
        const totalCount = filters?.action || filters?.entityType
          ? allLogs.length
          : pageGroups.reduce((total, pages) => total + (pages[0]?.totalCount ?? 0), 0);
        return { logs: allLogs, totalCount };
      })
    );
  }

  private getPages(url: string, params: HttpParams, recordLimit: number | undefined): Observable<AdminAuditPage[]> {
    return this.http.get<AdminAuditPage>(url, { params }).pipe(
      switchMap(firstPage => {
        const recordsToLoad = recordLimit === undefined
          ? firstPage.totalCount
          : Math.min(firstPage.totalCount, recordLimit);
        const pageCount = Math.min(Math.ceil(recordsToLoad / 100), 1000);
        if (pageCount <= 1) return of([firstPage]);

        return from(Array.from({ length: pageCount - 1 }, (_, index) => index + 2)).pipe(
          concatMap(page => this.http.get<AdminAuditPage>(url, {
            params: params.set('page', String(page))
          })),
          toArray(),
          map(remainingPages => [firstPage, ...remainingPages])
        );
      })
    );
  }

  private csvValue(value: string): string {
    return `"${value.replace(/"/g, '""')}"`;
  }
}
