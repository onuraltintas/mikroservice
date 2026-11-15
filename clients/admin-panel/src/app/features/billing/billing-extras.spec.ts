import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { BillingAdminOrder, BillingAdminService, BillingCoupon, BillingWithdrawal } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BillingCouponsComponent } from './billing-coupons';
import { BillingOrdersComponent } from './billing-orders';
import { BillingWithdrawalsComponent } from './billing-withdrawals';

const plan = { id: 'pl1', name: 'TEST Aylık' };
const coupon: BillingCoupon = { id: 'c1', code: 'OKUL-25', kind: 'Percent', value: 25, startsAt: null, endsAt: '2026-12-31T21:00:00Z', maxUses: 100, usedCount: 3,
  planIds: ['pl1'], isActive: true };
const withdrawal: BillingWithdrawal = { id: 'w1', orderId: 'o1', status: 'Requested', reason: 'Vazgeçtim', requestedAt: '2026-11-15T09:00:00Z', reviewNote: null,
  reviewedAt: null, accountName: 'Deneme Öğrenci', accountEmail: null, planName: 'TEST Aylık', paymentCode: 'EDU-AAA111', amount: 200, currency: 'TRY' };
const paid = (extra: Partial<BillingAdminOrder['order']> = {}): BillingAdminOrder => ({
  order: { id: 'o1', planId: 'pl1', planName: 'TEST Aylık', productCodes: ['coaching'], buyerType: 'Individual', amount: 150, currency: 'TRY', durationDays: 30,
    paymentCode: 'EDU-AAA111', status: 'Paid', createdAt: '2026-11-15T09:00:00Z', expiresAt: '2026-11-29T09:00:00Z', paidDeclaredAt: null, payerNote: null,
    hasReceipt: false, reviewedAt: '2026-11-15T10:00:00Z', reviewNote: null, documentsVersion: 'v', listPrice: 200, discount: 50, couponCode: 'OKUL-25',
    withdrawal: null, hasInvoice: false, ...extra },
  accountName: 'Deneme Öğrenci', accountEmail: null, orderedByUserId: 'u', beneficiaryUserId: 'u', reviewedBy: 'a' });

function setup<T>(component: new () => T, overrides: Record<string, unknown> = {}) {
  const billing = {
    getCoupons: vi.fn(() => of([coupon])),
    getPlans: vi.fn(() => of([plan])),
    getProducts: vi.fn(() => of([])),
    createCoupon: vi.fn(() => of(coupon)),
    updateCoupon: vi.fn(() => of(coupon)),
    getWithdrawals: vi.fn(() => of([withdrawal])),
    reviewWithdrawal: vi.fn(() => of(withdrawal)),
    getOrders: vi.fn(() => of([paid()])),
    attachInvoice: vi.fn(() => of(paid({ hasInvoice: true }))),
    removeInvoice: vi.fn(() => of(undefined)),
    ...overrides
  };
  const toaster = { success: vi.fn() };
  TestBed.configureTestingModule({ imports: [component as never], providers: [
    { provide: BillingAdminService, useValue: billing }, { provide: ToasterService, useValue: toaster }] });
  const fixture = TestBed.createComponent(component as never);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance as T, billing, toaster, element: fixture.nativeElement as HTMLElement };
}

describe('Abonelik ve Ödeme: coupons, withdrawals, invoices', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('coupons: the list shows discount, last day, use and plans', () => {
    const { element } = setup(BillingCouponsComponent);
    const row = element.querySelector('tbody tr')!.textContent!;
    expect(row).toContain('OKUL-25');
    expect(row).toContain('%25');
    expect(row).toContain('3 / 100');
    expect(row).toContain('TEST Aylık');
    expect(row).toContain('31.12.2026');
  });

  it('coupons: the last day is kept whole and only chosen plans are sent', () => {
    const { component, billing } = setup(BillingCouponsComponent);
    component.start();
    Object.assign(component.draft()!, { code: ' yaz-10 ', kind: 'Amount', value: 75, startsOn: '2026-12-01', endsOn: '2026-12-31', maxUses: '' as unknown as number,
      planIds: { pl1: true, pl2: false } });
    const request = component.toRequest(component.draft()!);
    expect(request).toMatchObject({ code: 'yaz-10', kind: 'Amount', value: 75, maxUses: null, planIds: ['pl1'], isActive: true });
    expect(new Date(request.endsAt!).getTime() - new Date(request.startsAt!).getTime()).toBe(31 * 24 * 3600 * 1000);
    component.save();
    expect(billing.createCoupon).toHaveBeenCalled();

    component.start(coupon);
    expect(component.draft()).toMatchObject({ id: 'c1', code: 'OKUL-25', maxUses: 100, planIds: { pl1: true } });
    component.save();
    expect(billing.updateCoupon).toHaveBeenCalledWith('c1', expect.objectContaining({ kind: 'Percent', value: 25 }));
  });

  it('withdrawals: accepting needs no note, refusing needs a reason', () => {
    const { component, billing, element, fixture } = setup(BillingWithdrawalsComponent);
    expect(billing.getWithdrawals).toHaveBeenCalledWith('Requested');
    expect(element.querySelector('[data-testid="withdrawal-EDU-AAA111"]')!.textContent).toContain('"Vazgeçtim"');
    component.start(withdrawal, false);
    fixture.detectChanges();
    const submit = () => element.querySelector('[data-testid="withdrawal-review"] button[type=submit]') as HTMLButtonElement;
    expect(submit().disabled).toBe(true);
    component.review.set({ ...component.review()!, note: 'Süre geçti.' });
    fixture.detectChanges();
    expect(submit().disabled).toBe(false);
    component.confirm();
    expect(billing.reviewWithdrawal).toHaveBeenCalledWith('w1', false, 'Süre geçti.');

    component.start(withdrawal, true);
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="withdrawal-review"]')!.textContent).toContain('bankadan siz iade edeceksiniz');
    component.confirm();
    expect(billing.reviewWithdrawal).toHaveBeenLastCalledWith('w1', true, '');
  });

  it('orders: a paid order shows its coupon, takes an invoice with number, date and file, and removes it after asking', () => {
    const { component, billing, element, fixture } = setup(BillingOrdersComponent);
    const row = element.querySelector('[data-testid="order-EDU-AAA111"]')!;
    expect(row.textContent).toContain('Kupon OKUL-25');
    expect(row.textContent).toContain('Fatura ekle');

    component.start(paid(), 'invoice');
    expect(component.canConfirm()).toBe(false);
    component.review.set({ ...component.review()!, number: 'ABC2026000000001', issuedOn: '2026-11-15' });
    expect(component.canConfirm()).toBe(false);
    const file = new File(['%PDF'], 'fatura.pdf', { type: 'application/pdf' });
    component.chooseInvoice({ target: { files: [file] } } as unknown as Event);
    expect(component.canConfirm()).toBe(true);
    component.confirm();
    expect(billing.attachInvoice).toHaveBeenCalledWith('o1', 'ABC2026000000001', '2026-11-15', file);

    billing.getOrders.mockReturnValue(of([paid({ hasInvoice: true, withdrawal: 'Requested' })]));
    component.load();
    fixture.detectChanges();
    const withInvoice = element.querySelector('[data-testid="order-EDU-AAA111"]')!;
    expect(withInvoice.textContent).toContain('Faturayı aç');
    expect(withInvoice.textContent).toContain('Cayma talebi var');
    expect(billing.removeInvoice).not.toHaveBeenCalled();
    component.start(paid({ hasInvoice: true }), 'removeInvoice');
    component.confirm();
    expect(billing.removeInvoice).toHaveBeenCalledWith('o1');
  });
});
