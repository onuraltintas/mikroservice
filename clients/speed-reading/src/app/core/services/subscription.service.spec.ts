import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SubscriptionService } from './subscription.service';

describe('SubscriptionService', () => {
  let service: SubscriptionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SubscriptionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the plans a visitor can buy from the billing catalog and marks bundles', () => {
    let plans: unknown;
    service.getPublicPlans().subscribe(value => plans = value);
    const plan = (id: string, buyerType: string, productCodes: string[]) => ({ id, name: 'TEST ' + id, description: '', buyerType, price: 200, currency: 'TRY',
      durationDays: 30, isContactOnly: false, features: ['TEST özellik'], productCodes });
    http.expectOne('/api/billing/v1/catalog/speed-reading').flush({ plans: [
      plan('a', 'Individual', ['speed-reading']), plan('b', 'Institution', ['speed-reading']), plan('c', 'Individual', ['coaching', 'speed-reading'])] });
    expect(plans).toEqual([
      { id: 'a', name: 'TEST a', description: '', price: 200, currency: 'TRY', durationDays: 30, isContactOnly: false, features: ['TEST özellik'], isBundle: false },
      { id: 'c', name: 'TEST c', description: '', price: 200, currency: 'TRY', durationDays: 30, isContactOnly: false, features: ['TEST özellik'], isBundle: true }]);
  });

  it('asks the Hızlı Okuma service what the student may use', () => {
    let modules: unknown;
    service.getMyModules().subscribe(value => modules = value);
    http.expectOne(request => request.url.endsWith('/subscriptions/my-modules')).flush({ success: true, data: { modules: ['SpeedReading'], hasSpeedReading: true, hasCoaching: false } });
    expect(modules).toEqual({ modules: ['SpeedReading'], hasSpeedReading: true, hasCoaching: false });
  });
});
