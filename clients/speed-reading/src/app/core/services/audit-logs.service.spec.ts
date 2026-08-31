import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditLogsService } from './audit-logs.service';

describe('AuditLogsService', () => {
  let service: AuditLogsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuditLogsService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuditLogsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('merges identity, coaching and notification audit streams in descending order', () => {
    service.getAuditLogs({ pageNumber: 1, pageSize: 2 }).subscribe(result => {
      expect(result.totalCount).toBe(3);
      expect(result.logs.map(log => log.id)).toEqual(['identity-1', 'notification-1']);
      expect(result.logs[0].action).toBe('Update');
      expect(result.logs[1].entityType).toBe('Notification');
    });

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    expect(requests.length).toBe(3);
    requests.find(req => req.request.url.endsWith('/identity'))?.flush({
      items: [{
        id: 'identity-1', occurredAt: '2026-08-31T10:00:00Z', serviceName: 'Identity.API',
        actorUserId: 'user-1', actorRoles: 'SystemAdmin', tenantId: null,
        httpMethod: 'PUT', path: '/api/configurations/Site.Name', statusCode: 204,
        correlationId: 'corr-1', clientIp: '127.0.0.1', userAgent: 'Chrome',
        action: 'Update', resourceType: 'Configuration', resourceId: 'Site.Name', changedFieldsJson: null
      }], totalCount: 1, page: 1, pageSize: 100
    });
    requests.find(req => req.request.url.endsWith('/coaching'))?.flush({
      items: [{
        id: 'coaching-1', occurredAt: '2026-08-31T09:00:00Z', serviceName: 'Coaching.API',
        actorUserId: 'user-2', actorRoles: 'SystemAdmin', tenantId: null,
        httpMethod: 'POST', path: '/api/assignments', statusCode: 201,
        correlationId: 'corr-2', clientIp: null, userAgent: null,
        action: 'Create', resourceType: 'Assignment', resourceId: 'assignment-1', changedFieldsJson: null
      }], totalCount: 1, page: 1, pageSize: 100
    });
    requests.find(req => req.request.url.endsWith('/notification'))?.flush({
      items: [{
        id: 'notification-1', occurredAt: '2026-08-31T09:30:00Z', serviceName: 'Notification.API',
        actorUserId: 'user-3', actorRoles: 'SystemAdmin', tenantId: null,
        httpMethod: 'POST', path: '/api/notifications', statusCode: 202,
        correlationId: 'corr-3', clientIp: '127.0.0.1', userAgent: 'Chrome',
        action: null, resourceType: 'Notification', resourceId: 'notification-1', changedFieldsJson: null
      }], totalCount: 1, page: 1, pageSize: 100
    });
  });

  it('creates a CSV from the same merged audit contract', () => {
    service.exportAuditLogs({ pageNumber: 1, pageSize: 50 }).subscribe(blob => {
      expect(blob.type).toBe('text/csv;charset=utf-8');
      expect(blob.size).toBeGreaterThan(0);
    });

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    expect(requests.length).toBe(3);
    for (const request of requests) {
      request.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    }
  });

  it('loads filter facets from the three service-owned audit stores', () => {
    service.getAuditFilterOptions().subscribe(options => {
      expect(options.actions).toEqual(['create', 'update']);
      expect(options.entityTypes).toEqual(['Assignment', 'Configuration']);
    });

    const requests = http.match(req => req.method === 'GET' && req.url.endsWith('/facets'));
    expect(requests.length).toBe(3);
    requests.find(req => req.request.url.includes('/identity/'))?.flush({ actions: ['update'], resourceTypes: ['Configuration'] });
    requests.find(req => req.request.url.includes('/coaching/'))?.flush({ actions: ['create'], resourceTypes: ['Assignment'] });
    requests.find(req => req.request.url.includes('/notification/'))?.flush({ actions: [], resourceTypes: [] });
  });

  it('normalizes the date-picker end date to the end of the selected day', () => {
    service.getAuditLogs({ endDate: new Date(2026, 7, 31), pageNumber: 1, pageSize: 50 }).subscribe();

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    expect(requests.length).toBe(3);
    const expectedEnd = new Date(2026, 7, 31, 23, 59, 59, 999).toISOString();
    for (const request of requests) {
      expect(request.request.params.get('to')).toBe(expectedEnd);
      request.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    }
  });

  it('returns healthy service logs with a warning when one service fails', () => {
    let response: any;
    service.getAuditLogs({ pageNumber: 1, pageSize: 50 }).subscribe(result => response = result);

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    requests.find(req => req.request.url.endsWith('/identity'))?.flush('identity unavailable', {
      status: 503,
      statusText: 'Service Unavailable'
    });
    requests.find(req => req.request.url.endsWith('/coaching'))?.flush({
      items: [], totalCount: 1, page: 1, pageSize: 100
    });
    requests.find(req => req.request.url.endsWith('/notification'))?.flush({
      items: [], totalCount: 2, page: 1, pageSize: 100
    });

    expect(response.failedServices).toEqual(['identity']);
    expect(response.warning).toContain('identity');
    expect(response.totalCount).toBe(3);
  });

  it('fails the list when every audit service fails', () => {
    let receivedError: Error | undefined;
    service.getAuditLogs().subscribe({ error: error => receivedError = error });

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    for (const request of requests) {
      request.flush('service unavailable', { status: 503, statusText: 'Service Unavailable' });
    }

    expect(receivedError?.message).toContain('Audit servislerinden veri alınamadı');
  });

  it('caps normal list metadata at the first 100,000 records', () => {
    let response: any;
    service.getAuditLogs({ pageNumber: 1, pageSize: 100 }).subscribe(result => response = result);

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    for (const request of requests) {
      request.flush({ items: [], totalCount: 100_001, page: 1, pageSize: 100 });
    }

    expect(response.totalCount).toBe(100_000);
    expect(response.totalPages).toBe(1_000);
    expect(response.warning).toContain('100.000');
  });

  it('caps a requested list page to the supported page range', () => {
    let response: any;
    service.getAuditLogs({ pageNumber: 1_001, pageSize: 100 }).subscribe(result => response = result);

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    for (const request of requests) {
      request.flush({ items: [], totalCount: 1, page: 1, pageSize: 100 });
    }

    expect(response.pageNumber).toBe(1_000);
  });

  it('fails closed when export exceeds 100,000 records', () => {
    let receivedError: Error | undefined;
    service.exportAuditLogs().subscribe({ error: error => receivedError = error });

    const requests = http.match(req => req.method === 'GET' && req.url.includes('/api/admin-audit/'));
    requests.filter(req => !req.request.url.endsWith('/identity')).forEach(request => {
      request.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    });
    requests.find(req => req.request.url.endsWith('/identity'))?.flush({
      items: [], totalCount: 100_001, page: 1, pageSize: 100
    });

    expect(receivedError?.message).toContain('güvenli dışa aktarma sınırını aşıyor');
  });

  it('propagates facet failures for the component to show a retry state', () => {
    let receivedError: unknown;
    service.getAuditFilterOptions().subscribe({ error: error => receivedError = error });

    const requests = http.match(req => req.method === 'GET' && req.url.endsWith('/facets'));
    requests.slice(0, 2).forEach(request => {
      request.flush({ actions: [], resourceTypes: [] });
    });
    requests[2].flush('facets unavailable', { status: 503, statusText: 'Service Unavailable' });

    expect(receivedError).toBeTruthy();
  });
});
