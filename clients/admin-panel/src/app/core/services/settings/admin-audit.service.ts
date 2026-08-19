import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export type AdminAuditServiceName = 'identity' | 'coaching' | 'notification';

export interface AdminAuditRecord {
  id: string;
  occurredAt: string;
  serviceName: string;
  actorUserId: string;
  actorRoles: string;
  tenantId?: string;
  httpMethod: string;
  path: string;
  statusCode: number;
  correlationId: string;
  clientIp?: string;
  userAgent?: string;
}

export interface AdminAuditPage {
  items: AdminAuditRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminAuditFilter {
  page: number;
  pageSize: number;
  search?: string;
  statusCode?: number;
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class AdminAuditService {
  private readonly http = inject(HttpClient);

  getPage(service: AdminAuditServiceName, filter: AdminAuditFilter): Observable<AdminAuditPage> {
    let params = new HttpParams()
      .set('page', filter.page)
      .set('pageSize', filter.pageSize);

    if (filter.search) params = params.set('search', filter.search);
    if (filter.statusCode) params = params.set('statusCode', filter.statusCode);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);

    return this.http.get<AdminAuditPage>(
      `${environment.apiUrl}/admin-audit/${service}`,
      { params }
    );
  }
}
