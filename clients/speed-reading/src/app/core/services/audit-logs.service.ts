import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, from, of, throwError } from 'rxjs';
import { catchError, concatMap, map, switchMap, toArray } from 'rxjs/operators';
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

interface AdminAuditFacets {
  actions: string[];
  resourceTypes: string[];
}

interface AuditLogLoadResult {
  logs: AuditLog[];
  sourceTotalCount: number;
  failedServices: string[];
  limitedBySafetyCap: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuditLogsService {
  private static readonly maxRecords = 100_000;
  private static readonly backendPageSize = 100;
  private static readonly maxBackendPage = 1_000;
  private readonly http = inject(HttpClient);
  private readonly auditUrls = [
    `${environment.apiUrl}/admin-audit/identity`,
    `${environment.apiUrl}/admin-audit/coaching`,
    `${environment.apiUrl}/admin-audit/notification`
  ];
  private readonly facetUrls = this.auditUrls.map(url => `${url}/facets`);

  getAuditLogs(filters?: AuditLogFilters): Observable<AuditLogListResponse> {
    const pageSize = Math.min(Math.max(filters?.pageSize ?? 50, 1), AuditLogsService.backendPageSize);
    const maxPageNumber = Math.ceil(AuditLogsService.maxRecords / pageSize);
    const requestedPageNumber = Math.max(filters?.pageNumber ?? 1, 1);
    let params = new HttpParams().set('page', '1').set('pageSize', '100');

    if (filters?.userId) params = params.set('search', filters.userId);
    if (filters?.action) params = params.set('action', filters.action);
    if (filters?.entityType) params = params.set('resourceType', filters.entityType);
    if (filters?.startDate) params = params.set('from', filters.startDate.toISOString());
    if (filters?.endDate) {
      const endOfDay = new Date(filters.endDate);
      endOfDay.setHours(23, 59, 59, 999);
      params = params.set('to', endOfDay.toISOString());
    }

    return this.loadAuditLogs(params, requestedPageNumber * pageSize, false).pipe(
      map(result => {
        const totalPages = Math.min(
          Math.ceil(result.sourceTotalCount / pageSize),
          maxPageNumber
        );
        const effectivePageNumber = totalPages === 0
          ? 1
          : Math.min(requestedPageNumber, totalPages);

        return {
          logs: result.logs.slice(
            (effectivePageNumber - 1) * pageSize,
            effectivePageNumber * pageSize
          ),
          totalCount: Math.min(result.sourceTotalCount, AuditLogsService.maxRecords),
          pageNumber: effectivePageNumber,
          pageSize,
          totalPages,
          failedServices: result.failedServices,
          warning: this.getListWarning(result)
        };
      })
    );
  }

  getAuditFilterOptions(): Observable<{ actions: string[]; entityTypes: string[] }> {
    const params = new HttpParams().set('page', '1').set('pageSize', '100');
    return forkJoin(this.facetUrls.map(url => this.http.get<AdminAuditFacets>(url, { params }))).pipe(
      map(facets => ({
        actions: [...new Set(facets.flatMap(facet => facet.actions ?? []))].sort(),
        entityTypes: [...new Set(facets.flatMap(facet => facet.resourceTypes ?? []))].sort()
      }))
    );
  }

  exportAuditLogs(filters?: AuditLogFilters): Observable<Blob> {
    let params = new HttpParams().set('page', '1').set('pageSize', '100');
    if (filters?.userId) params = params.set('search', filters.userId);
    if (filters?.action) params = params.set('action', filters.action);
    if (filters?.entityType) params = params.set('resourceType', filters.entityType);
    if (filters?.startDate) params = params.set('from', filters.startDate.toISOString());
    if (filters?.endDate) {
      const endOfDay = new Date(filters.endDate);
      endOfDay.setHours(23, 59, 59, 999);
      params = params.set('to', endOfDay.toISOString());
    }

    return this.loadAuditLogs(params, undefined, true).pipe(
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
    params: HttpParams,
    recordLimit: number | undefined,
    enforceExportLimit: boolean
  ): Observable<AuditLogLoadResult> {
    return forkJoin(this.auditUrls.map(url => this.getPages(url, params, recordLimit, enforceExportLimit).pipe(
      map(pages => ({ serviceName: this.getServiceName(url), pages, failed: false })),
      catchError(error => {
        if (enforceExportLimit) return throwError(() => error);
        return of({ serviceName: this.getServiceName(url), pages: [], failed: true });
      })
    ))).pipe(
      map(serviceResults => {
        const failedServices = serviceResults.filter(result => result.failed).map(result => result.serviceName);
        if (failedServices.length === this.auditUrls.length) {
          throw new Error('Audit servislerinden veri alınamadı.');
        }

        const pageGroups = serviceResults.filter(result => !result.failed).map(result => result.pages);
        const sourceTotalCount = pageGroups.reduce(
          (total, pages) => total + (pages[0]?.totalCount ?? 0),
          0
        );
        if (enforceExportLimit && sourceTotalCount > AuditLogsService.maxRecords) {
          throw new Error('Audit kaydı sayısı güvenli dışa aktarma sınırını aşıyor.');
        }

        const allLogs = pageGroups.flat()
          .flatMap(page => page.items ?? [])
          .map(record => this.toAuditLog(record))
          .sort((left, right) => right.timestamp.getTime() - left.timestamp.getTime());
        const limitedBySafetyCap = sourceTotalCount > AuditLogsService.maxRecords;
        return {
          logs: allLogs.slice(0, AuditLogsService.maxRecords),
          sourceTotalCount,
          failedServices,
          limitedBySafetyCap
        };
      })
    );
  }

  private getPages(
    url: string,
    params: HttpParams,
    recordLimit: number | undefined,
    enforceExportLimit: boolean
  ): Observable<AdminAuditPage[]> {
    return this.http.get<AdminAuditPage>(url, { params }).pipe(
      switchMap(firstPage => {
        const recordsToLoad = recordLimit === undefined
          ? firstPage.totalCount
          : Math.min(firstPage.totalCount, recordLimit);
        if (enforceExportLimit && recordsToLoad > AuditLogsService.maxRecords) {
          return throwError(() => new Error('Audit kaydı sayısı güvenli dışa aktarma sınırını aşıyor.'));
        }
        const pageCount = Math.min(
          Math.ceil(recordsToLoad / AuditLogsService.backendPageSize),
          AuditLogsService.maxBackendPage
        );
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

  private getServiceName(url: string): string {
    return url.substring(url.lastIndexOf('/') + 1);
  }

  private getListWarning(result: AuditLogLoadResult): string | null {
    const warnings: string[] = [];
    if (result.failedServices.length > 0) {
      warnings.push(`Eksik veri: ${result.failedServices.join(', ')} servisi yanıt vermedi.`);
    }
    if (result.limitedBySafetyCap) {
      warnings.push('İlk 100.000 kayıt gösteriliyor; daha eski kayıtlar listelenmiyor.');
    }
    return warnings.length > 0 ? warnings.join(' ') : null;
  }

  private csvValue(value: string): string {
    const safeValue = /^[=+\-@]/.test(value) ? `'${value}` : value;
    return `"${safeValue.replace(/"/g, '""')}"`;
  }
}
