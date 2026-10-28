import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CoachingCheckoutComponent } from '../../../../../shared/coaching-checkout.component';
import { CoachingMyPaymentsComponent } from '../../../../../shared/coaching-my-payments.component';
import { CoachingBuyerPayment, CoachingCheckoutChoice, CoachingSalesApi, normalizeCoachingCoupon } from '../../../../../shared/coaching-sales.api';

const docs = { version: '2026-10-28', preInformation: 'ÖN BİLGİLENDİRME FORMU metni', distanceSales: 'MESAFELİ SATIŞ SÖZLEŞMESİ metni' };
const flushMicrotasks = () => new Promise(resolve => setTimeout(resolve));

describe('O10 sales (shared buyer components)', () => {
  let http: HttpTestingController;
  let api: CoachingSalesApi;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = new CoachingSalesApi(TestBed.inject(HttpClient), '/api');
  });
  afterEach(() => http.verify());

  it('normalizes discount codes the way the server does', () => {
    expect(normalizeCoachingCoupon('  yaz-2026 ')).toBe('YAZ-2026');
    expect(normalizeCoachingCoupon('indirim 10')).toBe('INDIRIM10');
  });

  describe('checkout', () => {
    function checkout(planId = 'plan-1') {
      const fixture = TestBed.createComponent(CoachingCheckoutComponent);
      const choices: CoachingCheckoutChoice[] = [];
      fixture.componentInstance.api = api;
      fixture.componentInstance.price = 1000;
      fixture.componentInstance.planId = planId;
      fixture.componentInstance.choiceChange.subscribe(choice => choices.push(choice));
      fixture.detectChanges();
      return { fixture, component: fixture.componentInstance, choices };
    }

    it('prices the plan with a valid discount code and reports the code', async () => {
      const { fixture, component, choices } = checkout();
      component.couponInput = ' yaz-2026 ';
      const pending = component.applyCoupon();
      const request = http.expectOne(r => r.url === '/api/coaching/subscriptions/coupons/YAZ-2026');
      expect(request.request.params.get('planId')).toBe('plan-1');
      request.flush({ data: { code: 'YAZ-2026', listPrice: 1000, discount: 200, amount: 800 } });
      await pending; fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="coupon-quote"]').textContent).toContain('YAZ-2026');
      expect(fixture.nativeElement.textContent).toContain('800,00');
      expect(choices.at(-1)).toEqual({ couponCode: 'YAZ-2026', acceptSalesContract: false });
    });

    it('shows why a code was refused and keeps the list price', async () => {
      const { fixture, component, choices } = checkout();
      component.couponInput = 'BITTI';
      const pending = component.applyCoupon();
      http.expectOne(r => r.url.endsWith('/coupons/BITTI')).flush({ message: 'Bu indirim kodunun süresi dolmuş.' }, { status: 409, statusText: 'Conflict' });
      await pending; fixture.detectChanges();

      expect(component.couponError()).toContain('süresi dolmuş');
      expect(component.quote()).toBeNull();
      expect(choices.every(choice => choice.couponCode === null)).toBe(true);
    });

    it('rejects a malformed code without calling the server', async () => {
      const { component } = checkout();
      component.couponInput = '!';
      await component.applyCoupon();
      http.expectNone(r => r.url.includes('/coupons/'));
      expect(component.couponError()).toContain('3–30');
    });

    it('lets the buyer accept only after opening the documents, and asks again when the price changes', async () => {
      const { fixture, component, choices } = checkout();
      component.setAccepted(true);
      expect(component.accepted()).toBe(false);

      const opening = component.openDocuments();
      http.expectOne(r => r.url === '/api/coaching/subscriptions/sales-documents' && r.params.get('planId') === 'plan-1' && !r.params.has('couponCode')).flush({ data: docs });
      await opening; fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="pre-information"]').textContent).toContain('ÖN BİLGİLENDİRME');
      component.setAccepted(true);
      expect(choices.at(-1)).toEqual({ couponCode: null, acceptSalesContract: true });

      component.couponInput = 'YAZ-2026';
      const applying = component.applyCoupon();
      http.expectOne(r => r.url.endsWith('/coupons/YAZ-2026')).flush({ data: { code: 'YAZ-2026', listPrice: 1000, discount: 200, amount: 800 } });
      await applying;
      expect(component.documents()).toBeNull();
      expect(choices.at(-1)).toEqual({ couponCode: 'YAZ-2026', acceptSalesContract: false });

      const reopening = component.openDocuments();
      http.expectOne(r => r.url.endsWith('/sales-documents') && r.params.get('couponCode') === 'YAZ-2026').flush({ data: docs });
      await reopening;
      expect(component.documents()).not.toBeNull();
    });

    it('starts over when another plan is chosen', async () => {
      const { component, choices } = checkout();
      const opening = component.openDocuments();
      http.expectOne(r => r.url.endsWith('/sales-documents')).flush({ data: docs });
      await opening; component.setAccepted(true);

      component.planId = 'plan-2';
      expect(component.documents()).toBeNull();
      expect(component.accepted()).toBe(false);
      expect(choices.at(-1)).toEqual({ couponCode: null, acceptSalesContract: false });
    });
  });

  describe('my payments', () => {
    const payment = (overrides: Partial<CoachingBuyerPayment> = {}): CoachingBuyerPayment => ({
      id: 'pay-1', transferId: 'tr-1', planName: 'Öğrenci planı', amount: 800, currency: 'TRY', status: 'Completed', createdAt: '2026-10-20T10:00:00Z',
      listPrice: 1000, couponCode: 'YAZ-2026', discountAmount: 200, invoiceNumber: 'EDU2026000001', invoiceIssuedOn: '2026-10-21',
      withdrawalStatus: null, withdrawUntil: new Date(Date.now() + 5 * 86_400_000).toISOString(), hasSalesDocuments: true, ...overrides
    });
    async function payments(items: CoachingBuyerPayment[], formal = false) {
      const fixture = TestBed.createComponent(CoachingMyPaymentsComponent);
      fixture.componentInstance.api = api; fixture.componentInstance.formal = formal;
      fixture.detectChanges();
      http.expectOne('/api/coaching/subscriptions/my-payments').flush({ data: items });
      await flushMicrotasks(); fixture.detectChanges();
      return { fixture, component: fixture.componentInstance };
    }

    it('lists payments with the discount and the invoice download', async () => {
      const save = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
      const { fixture, component } = await payments([payment()]);
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Öğrenci planı'); expect(text).toContain('YAZ-2026 ile 200,00 TRY indirim');

      const downloading = component.downloadInvoice(component.payments()[0]);
      const request = http.expectOne('/api/coaching/subscriptions/my-payments/pay-1/invoice');
      expect(request.request.responseType).toBe('blob');
      request.flush(new Blob(['%PDF'], { type: 'application/pdf' }));
      await downloading;
      expect(save).toHaveBeenCalled();
      save.mockRestore();
    });

    it('says when no invoice is recorded yet', async () => {
      const { fixture } = await payments([payment({ invoiceNumber: null })]);
      expect(fixture.nativeElement.querySelector('[data-testid="invoice-pay-1"]')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Fatura henüz eklenmedi');
    });

    it('shows the documents the buyer accepted', async () => {
      const { fixture, component } = await payments([payment()]);
      const opening = component.toggleDocuments(component.payments()[0]);
      http.expectOne('/api/coaching/subscriptions/my-bank-transfer-requests/tr-1/sales-documents').flush({ data: docs });
      await opening; fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="accepted-distance-sales"]').textContent).toContain('MESAFELİ SATIŞ');
    });

    it('sends a withdrawal request within the 14 days and shows its state', async () => {
      const { fixture, component } = await payments([payment()], true);
      expect(fixture.nativeElement.querySelector('[data-testid="withdraw-pay-1"]')).toBeTruthy();
      component.startWithdrawal(component.payments()[0]);
      component.withdrawalReason = '  Vazgeçtim  ';
      const sending = component.submitWithdrawal(component.payments()[0]);
      const request = http.expectOne('/api/coaching/subscriptions/my-bank-transfer-requests/tr-1/withdrawal');
      expect(request.request.body).toEqual({ reason: 'Vazgeçtim' });
      request.flush({ data: payment({ withdrawalStatus: 'Requested', withdrawUntil: null }) });
      await sending; fixture.detectChanges();

      expect(component.success()).toContain('talebiniz alındı');
      expect(fixture.nativeElement.querySelector('[data-testid="withdrawal-status-pay-1"]').textContent).toContain('inceleniyor');
      expect(fixture.nativeElement.querySelector('[data-testid="withdraw-pay-1"]')).toBeNull();
    });

    it('offers no withdrawal once the period is over', async () => {
      const { fixture } = await payments([payment({ withdrawUntil: null }), payment({ id: 'pay-2', withdrawUntil: '2020-01-01T00:00:00Z' })]);
      expect(fixture.nativeElement.querySelector('[data-testid="withdraw-pay-1"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="withdraw-pay-2"]')).toBeNull();
    });

    it('shows the server reason when a withdrawal is refused', async () => {
      const { component } = await payments([payment()]);
      component.startWithdrawal(component.payments()[0]);
      const sending = component.submitWithdrawal(component.payments()[0]);
      http.expectOne(r => r.url.endsWith('/withdrawal')).flush({ code: 'Sales.WithdrawalPeriodOver', message: 'Cayma süresi doldu.' }, { status: 409, statusText: 'Conflict' });
      await sending;
      expect(component.error()).toBe('Cayma süresi doldu.');
    });
  });
});
