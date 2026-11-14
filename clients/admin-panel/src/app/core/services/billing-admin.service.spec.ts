import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BillingAdminService } from './billing-admin.service';

describe('BillingAdminService', () => {
  let service: BillingAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(BillingAdminService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('talks to the billing admin API behind the gateway', () => {
    service.getProducts().subscribe();
    http.expectOne({ method: 'GET', url: '/api/billing/v1/admin/products' }).flush([]);
    service.updateProductSettings('p1', { requireSubscription: true, bankTransferApproval: 'Automatic' }).subscribe();
    const settings = http.expectOne({ method: 'PUT', url: '/api/billing/v1/admin/products/p1/settings' });
    expect(settings.request.body).toEqual({ requireSubscription: true, bankTransferApproval: 'Automatic' });
    settings.flush({});
    service.addEntitlement('p1', { key: 'coaching.parent', name: 'Veli', hasLimit: false }).subscribe();
    http.expectOne({ method: 'POST', url: '/api/billing/v1/admin/products/p1/entitlements' }).flush({});
    service.removeEntitlement('e1').subscribe();
    http.expectOne({ method: 'DELETE', url: '/api/billing/v1/admin/entitlements/e1' }).flush(null);
    service.getPlans().subscribe();
    http.expectOne({ method: 'GET', url: '/api/billing/v1/admin/plans' }).flush([]);
    service.getSeller().subscribe();
    http.expectOne({ method: 'GET', url: '/api/billing/v1/admin/seller' }).flush({});
    service.removeBankAccount('a1').subscribe();
    http.expectOne({ method: 'DELETE', url: '/api/billing/v1/admin/bank-accounts/a1' }).flush(null);
  });
});
