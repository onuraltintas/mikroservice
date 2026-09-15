import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SubscriptionService } from './subscription.service';

describe('SubscriptionService bank transfer payments', () => {
  let service: SubscriptionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SubscriptionService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SubscriptionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads only the public bank-transfer configuration', () => {
    service.getPublicBankTransferSettings().subscribe();

    const request = http.expectOne('/api/speed-reading/bank-transfer');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, data: null });
  });

  it('submits the chosen plan and payment reference with an idempotency key', () => {
    service.createBankTransferPaymentRequest({
      planId: 'plan-1',
      paymentReference: 'EFT-2026-000123',
      payerName: 'Örnek Öğrenci',
      note: 'Açıklama'
    }, 'bank-transfer-request-key').subscribe();

    const request = http.expectOne('/api/speed-reading/bank-transfer/requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('bank-transfer-request-key');
    expect(request.request.body.planId).toBe('plan-1');
    request.flush({ success: true, data: { id: 'request-1', status: 'Pending' } });
  });
});
