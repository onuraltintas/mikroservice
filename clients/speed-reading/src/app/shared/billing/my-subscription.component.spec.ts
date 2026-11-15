import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/services/auth.service';
import { MySubscriptionComponent } from './my-subscription.component';

describe('MySubscriptionComponent (shared billing page)', () => {
  it('asks the billing service about this product with the signed-in user and shows that no subscription is needed', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: AuthService, useValue: { currentUserValue: { firstName: 'Deneme', lastName: 'Okur', roles: ['Student'] } } }]
    });
    const fixture = TestBed.createComponent(MySubscriptionComponent);
    fixture.componentRef.setInput('product', 'speed-reading');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/billing/v1/me?product=speed-reading').flush({
      productCode: 'speed-reading', productName: 'Master Hızlı Okuma', requireSubscription: false, salesOpen: false,
      entitlements: [], subscriptions: [], pendingOrders: []
    });
    http.expectOne('/api/billing/v1/catalog/speed-reading').flush({
      productCode: 'speed-reading', productName: 'Master Hızlı Okuma', requireSubscription: false, salesOpen: false, plans: [], bankAccounts: []
    });
    http.expectOne('/api/billing/v1/orders').flush([]);
    http.expectOne('/api/billing/v1/organizations').flush([]);
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('abonelik gerektirmiyor');
    expect(fixture.componentInstance.c.buyerName).toBe('Deneme Okur');
    http.verify();
  });
});
