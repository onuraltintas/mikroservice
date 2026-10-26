import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { ToasterService } from '../../../core/services/toaster.service';
import { SupportInboxComponent } from './support-inbox';

describe('support inbox delete', () => {
  const request = {
    id: '11111111-1111-1111-1111-111111111111', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test',
    subject: 'Giriş', message: 'Giriş yapamıyorum', isProcessed: true, createdAt: '2026-10-26T06:00:00Z'
  };

  function setup(permissions: string[], confirmed: boolean) {
    const toaster = { confirm: vi.fn().mockResolvedValue(confirmed), success: vi.fn() };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { userProfile: signal({ roles: [] }), hasPermission: (p: string) => permissions.includes(p) } },
        { provide: ToasterService, useValue: toaster },
        { provide: PLATFORM_ID, useValue: 'browser' }]
    });
    const component = TestBed.runInInjectionContext(() => new SupportInboxComponent());
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/support/requests')).flush({ items: [request], totalCount: 1, pageNumber: 1, pageSize: 25 });
    return { component, http, toaster };
  }

  it('shows delete only with the delete permission', () => {
    expect(setup([ADMIN_PERMISSIONS.supportReply], true).component.canDelete()).toBe(false);
    TestBed.resetTestingModule();
    expect(setup([ADMIN_PERMISSIONS.supportDelete], true).component.canDelete()).toBe(true);
  });

  it('deletes after confirmation and reloads the list', async () => {
    const { component, http, toaster } = setup([ADMIN_PERMISSIONS.supportDelete], true);

    await component.remove(request);

    expect(toaster.confirm).toHaveBeenCalledOnce();
    const call = http.expectOne(`/api/support/requests/${request.id}`);
    expect(call.request.method).toBe('DELETE');
    call.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(r => r.url.endsWith('/support/requests')).flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    expect(toaster.success).toHaveBeenCalled();
    http.verify();
  });

  it('does nothing when the confirmation is cancelled', async () => {
    const { component, http } = setup([ADMIN_PERMISSIONS.supportDelete], false);

    await component.remove(request);

    http.verify();
  });
});
