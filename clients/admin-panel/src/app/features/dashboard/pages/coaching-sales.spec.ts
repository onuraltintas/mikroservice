import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { DASHBOARD_ROUTES } from '../dashboard.routes';
import { CoachingCoupon, CoachingSalesComponent } from './coaching-sales';

const flush = () => new Promise(resolve => setTimeout(resolve));
const coupon = (overrides: Partial<CoachingCoupon> = {}): CoachingCoupon => ({
  id: 'c1', code: 'YAZ-2026', kind: 'Percent', value: 20, validFrom: null, validUntil: null, maxUses: 100, usedCount: 3, planIds: [], isActive: true,
  createdAt: '2026-10-01T00:00:00Z', ...overrides
});

describe('CoachingSalesComponent (O10)', () => {
  let http: HttpTestingController;
  let toaster: { confirm: ReturnType<typeof vi.fn>; prompt: ReturnType<typeof vi.fn>; success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  const management = {
    getPlans: vi.fn(() => of([
      { id: 'p-ind', name: 'Bireysel yıllık', audience: 'Individual', isContactOnly: false },
      { id: 'p-inst', name: 'Kurum', audience: 'Institution', isContactOnly: false }
    ])),
    getPayments: vi.fn(() => of({ items: [
      { id: 'pay-1', userId: 'u1', userName: 'Ada Öğrenci', userEmail: 'ada@example.com', planName: 'Bireysel yıllık', amount: 800, currency: 'TRY', status: 'Completed', provider: 'BankTransfer', reference: 'EFT-1', createdAt: '2026-10-20T00:00:00Z' },
      { id: 'pay-2', userId: 'u2', userName: 'Can Öğrenci', userEmail: 'can@example.com', planName: 'Bireysel yıllık', amount: 1000, currency: 'TRY', status: 'Completed', provider: 'BankTransfer', reference: 'EFT-2', createdAt: '2026-10-21T00:00:00Z' }
    ], pageNumber: 1, pageSize: 25, totalCount: 2 }))
  };

  async function setup(withdrawals: unknown[] = []) {
    toaster = { confirm: vi.fn().mockResolvedValue(true), prompt: vi.fn().mockResolvedValue('Hizmet kullanıldı'), success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({ imports: [CoachingSalesComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingManagementService, useValue: management }, { provide: ToasterService, useValue: toaster }] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(CoachingSalesComponent);
    fixture.detectChanges();
    http.expectOne('/api/coaching-admin/sales/coupons').flush({ data: [coupon()] });
    http.expectOne('/api/coaching-admin/sales/withdrawals').flush({ data: withdrawals });
    await flush(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }
  afterEach(() => http?.verify());

  it('is reachable only for the platform administrator', () => {
    const route = DASHBOARD_ROUTES.find(item => item.path === 'coaching/sales');
    expect(route?.data?.['role']).toBe('SystemAdmin');
    expect(route?.data?.['permission']).toBe('Permissions.Coaching.SubscriptionManage');
  });

  it('lists discount codes and offers only plans a buyer can pay for', async () => {
    const { fixture, component } = await setup();
    expect(fixture.nativeElement.querySelector('[data-testid="coupon-YAZ-2026"]').textContent).toContain('3 / 100');
    expect(component.couponPlans().map(plan => plan.id)).toEqual(['p-ind']);
  });

  it('creates a code limited to a plan and a date range', async () => {
    const { component } = await setup();
    component.couponDraft = { ...component.couponDraft, code: ' kis-50 ', kind: 'Amount', value: 50, maxUses: 10 };
    component.couponFrom = '2026-11-01'; component.couponUntil = '2026-11-30';
    component.togglePlan('p-ind');
    const saving = component.saveCoupon();
    const request = http.expectOne(r => r.method === 'POST' && r.url === '/api/coaching-admin/sales/coupons');
    expect(request.request.body).toEqual({ code: 'KIS-50', kind: 'Amount', value: 50, maxUses: 10, planIds: ['p-ind'], isActive: true,
      validFrom: '2026-10-31T21:00:00.000Z', validUntil: '2026-11-30T20:59:59.000Z' });
    request.flush({ data: coupon({ id: 'c2', code: 'KIS-50', kind: 'Amount', value: 50 }) });
    await saving;
    expect(component.coupons().map(item => item.code)).toEqual(['KIS-50', 'YAZ-2026']);
    expect(component.success()).toContain('KIS-50');
  });

  it('refuses an invalid code or a percent above 100 before calling the server', async () => {
    const { component } = await setup();
    component.couponDraft = { ...component.couponDraft, code: 'x' };
    await component.saveCoupon();
    expect(component.error()).toContain('3–30');
    component.couponDraft = { ...component.couponDraft, code: 'COK', kind: 'Percent', value: 150 };
    await component.saveCoupon();
    expect(component.error()).toContain('0 ile 100');
    http.expectNone(r => r.method === 'POST');
  });

  it('closes a code by updating it inactive', async () => {
    const { component } = await setup();
    const closing = component.disableCoupon(component.coupons()[0]);
    const request = http.expectOne(r => r.method === 'PUT' && r.url === '/api/coaching-admin/sales/coupons/c1');
    expect(request.request.body.isActive).toBe(false);
    request.flush({ data: coupon({ isActive: false }) });
    await closing;
    expect(component.coupons()[0].isActive).toBe(false);
  });

  it('shows the lawyer note and saves the seller details', async () => {
    const { fixture, component } = await setup();
    component.select('seller');
    http.expectOne('/api/coaching-admin/sales/seller').flush({ data: { title: null, address: null, taxOffice: null, taxNumber: null, mersisNumber: null, email: null, phone: null } });
    await flush(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="lawyer-note"]').textContent).toContain('avukata');
    component.seller = { ...component.seller, title: '  Eduİvme Eğitim Ltd. Şti. ', taxNumber: '1234567890', email: '' };
    const saving = component.saveSeller();
    const request = http.expectOne(r => r.method === 'PUT' && r.url.endsWith('/sales/seller'));
    expect(request.request.body).toEqual({ title: 'Eduİvme Eğitim Ltd. Şti.', address: null, taxOffice: null, taxNumber: '1234567890', mersisNumber: null, email: null, phone: null });
    request.flush({ data: request.request.body });
    await saving;
    expect(component.success()).toContain('kaydedildi');
  });

  it('approves a withdrawal after confirmation and rejects one only with a reason', async () => {
    const pending = (id: string) => ({ transferId: id, userName: 'Ada', userEmail: 'ada@example.com', planName: 'Bireysel yıllık', amount: 800, currency: 'TRY',
      approvedAt: '2026-10-20T00:00:00Z', withdrawalStatus: 'Requested', reason: 'Vazgeçtim', requestedAt: '2026-10-22T00:00:00Z', reviewNote: null, reviewedAt: null });
    const { fixture, component } = await setup([pending('t1'), pending('t2')]);
    expect(component.pendingWithdrawals()).toBe(2);
    expect(fixture.nativeElement.querySelector('[data-testid="sales-tab-withdrawals"]').textContent).toContain('(2)');

    const approving = component.reviewWithdrawal(component.withdrawals()[0], true);
    await flush();
    const approve = http.expectOne('/api/coaching-admin/sales/withdrawals/t1/review');
    expect(approve.request.body).toEqual({ approve: true, note: null });
    approve.flush({ success: true });
    await flush();
    http.expectOne('/api/coaching-admin/sales/withdrawals').flush({ data: [{ ...pending('t1'), withdrawalStatus: 'Approved' }, pending('t2')] });
    await approving;
    expect(toaster.confirm).toHaveBeenCalled();
    expect(component.success()).toContain('İadeyi');

    toaster.prompt.mockResolvedValueOnce('  ');
    await component.reviewWithdrawal(component.withdrawals()[1], false);
    expect(component.error()).toContain('gerekçe');

    const rejecting = component.reviewWithdrawal(component.withdrawals()[1], false);
    await flush();
    const reject = http.expectOne('/api/coaching-admin/sales/withdrawals/t2/review');
    expect(reject.request.body).toEqual({ approve: false, note: 'Hizmet kullanıldı' });
    reject.flush({ success: true });
    await flush();
    http.expectOne('/api/coaching-admin/sales/withdrawals').flush({ data: [] });
    await rejecting;
  });

  it('attaches a PDF invoice to a payment and removes it', async () => {
    const { fixture, component } = await setup();
    component.select('invoices');
    await flush();
    const lookup = http.expectOne(r => r.url === '/api/coaching-admin/sales/invoices');
    expect(lookup.request.params.getAll('paymentIds')).toEqual(['pay-1', 'pay-2']);
    lookup.flush({ data: [{ id: 'i1', paymentId: 'pay-2', invoiceNumber: 'EDU2026000001', issuedOn: '2026-10-21', fileName: 'f.pdf', sizeBytes: 100 }] });
    await flush(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="payment-row-pay-2"]').textContent).toContain('EDU2026000001');

    component.startInvoice(component.payments()[0]);
    component.invoiceNumber = ' EDU2026000002 ';
    await component.attachInvoice(component.payments()[0]);
    expect(component.error()).toContain('PDF');

    component.chooseInvoice({ target: { files: [new File(['%PDF-1.7'], 'fatura.pdf', { type: 'application/pdf' })] } } as unknown as Event);
    const attaching = component.attachInvoice(component.payments()[0]);
    const upload = http.expectOne(r => r.method === 'POST' && r.url === '/api/coaching-admin/sales/payments/pay-1/invoice');
    expect(upload.request.params.get('invoiceNumber')).toBe('EDU2026000002');
    expect(upload.request.params.get('fileName')).toBe('fatura.pdf');
    expect(upload.request.headers.get('Content-Type')).toBe('application/pdf');
    upload.flush({ data: { id: 'i2', paymentId: 'pay-1', invoiceNumber: 'EDU2026000002', issuedOn: component.invoiceDate, fileName: 'fatura.pdf', sizeBytes: 8 } });
    await attaching;
    expect(component.invoices()['pay-1'].invoiceNumber).toBe('EDU2026000002');

    const removing = component.removeInvoice(component.payments()[0]);
    await flush();
    http.expectOne(r => r.method === 'DELETE' && r.url.endsWith('/payments/pay-1/invoice')).flush({ success: true });
    await removing;
    expect(component.invoices()['pay-1']).toBeUndefined();
  });
});
