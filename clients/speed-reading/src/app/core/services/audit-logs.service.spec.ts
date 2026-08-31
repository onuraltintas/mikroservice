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
});
