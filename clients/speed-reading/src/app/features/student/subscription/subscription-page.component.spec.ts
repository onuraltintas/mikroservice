import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { SubscriptionPageComponent } from './subscription-page.component';

// Billing parity (14.11.2026): the subscriber's own page inside the app.
describe('SubscriptionPageComponent', () => {
  beforeAll(() => registerLocaleData(localeTr));
  const day = 86_400_000;
  const subscription = (id: string, endInDays: number | null, isActive = true) => ({
    id, plan: { name: `Plan ${id}` }, productName: 'Hızlı Okuma', isActive,
    endDate: endInDays === null ? null : new Date(Date.now() + endInDays * day).toISOString()
  });

  function setup(subscriptions: unknown[], requests: unknown[] = []) {
    const service = {
      getMySubscriptions: jasmine.createSpy('subscriptions').and.returnValue(of(subscriptions)),
      getMyBankTransferPaymentRequests: jasmine.createSpy('requests').and.returnValue(of(requests))
    };
    TestBed.configureTestingModule({
      imports: [SubscriptionPageComponent],
      providers: [provideRouter([]), { provide: SubscriptionService, useValue: service }]
    });
    const fixture = TestBed.createComponent(SubscriptionPageComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, page: fixture.nativeElement as HTMLElement };
  }

  it('shows the active subscription with its end date and no renewal hint while it is far away', () => {
    const { component, page } = setup([subscription('a', 200), subscription('old', -30, false)]);
    expect(component.active().map(item => item.id)).toEqual(['a']);
    expect(component.endsSoon()).toBeFalse();
    expect(page.querySelector('[data-testid="subscription-status"]')?.textContent).toContain('Plan a');
    expect(page.querySelector('[data-testid="ends-soon"]')).toBeNull();
    expect(page.querySelector('[data-testid="no-requests"]')).not.toBeNull();
  });

  it('offers renewal in the last week', () => {
    const { component, page } = setup([subscription('a', 3)]);
    expect(component.endsSoon()).toBeTrue();
    expect(page.querySelector('[data-testid="ends-soon"]')?.textContent).toContain('mevcut dönemin bittiği gün başlar');
    expect(page.querySelector('a.action')?.getAttribute('href')).toBe('/odeme');
  });

  it('says so when nothing is active and names the last subscription that ended', () => {
    const { component, page } = setup([subscription('older', -90, false), subscription('last', -5, false)]);
    expect(component.active()).toEqual([]);
    expect(component.lastEnded()?.id).toBe('last');
    expect(page.querySelector('[data-testid="no-subscription"]')?.textContent).toContain('Plan last');
    expect(page.querySelector('a.action')?.textContent).toContain('Planları gör');
  });

  it('lists the payment requests and replaces one that changed', () => {
    const { component, page } = setup([], [
      { id: 'request-1', planName: 'Yıllık plan', paymentReference: 'EFT-1', status: 'Approved', createdAt: '2026-11-14T10:00:00Z', invoiceNumber: 'ONL2026000001' }
    ]);
    expect(page.querySelector('app-payment-requests')?.textContent).toContain('ONL2026000001');
    component.replaceRequest({ id: 'request-1', withdrawalStatus: 'Requested' } as never);
    expect(component.requests()[0].withdrawalStatus).toBe('Requested');
  });

  it('still shows what it could load when one of the two calls fails', () => {
    const service = {
      getMySubscriptions: jasmine.createSpy('subscriptions').and.returnValue(throwError(() => new Error('offline'))),
      getMyBankTransferPaymentRequests: jasmine.createSpy('requests').and.returnValue(of([{ id: 'request-1', planName: 'Yıllık plan', paymentReference: 'EFT-1', status: 'Pending', createdAt: '2026-11-14T10:00:00Z' }]))
    };
    TestBed.configureTestingModule({ imports: [SubscriptionPageComponent], providers: [provideRouter([]), { provide: SubscriptionService, useValue: service }] });
    const fixture = TestBed.createComponent(SubscriptionPageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.failed()).toBeTrue();
    expect(fixture.componentInstance.loading()).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('EFT-1');
  });
});
