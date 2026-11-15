import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { BillingAdminOrder, BillingAdminService, BillingAdminSubscription } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BillingOrdersComponent } from './billing-orders';
import { BillingSubscriptionsComponent } from './billing-subscriptions';

const product = { id: 'p2', code: 'speed-reading', name: 'Master Hızlı Okuma', requireSubscription: true, bankTransferApproval: 'Manual' as const,
  isActive: true, sortOrder: 2, entitlements: [] };
const order = (id: string, status: BillingAdminOrder['order']['status'], extra: Partial<BillingAdminOrder['order']> = {}): BillingAdminOrder => ({
  order: { id, planId: 'pl1', planName: 'TEST Aylık', productCodes: ['speed-reading'], buyerType: 'Individual', amount: 200, currency: 'TRY', durationDays: 30,
    paymentCode: `EDU-${id.toUpperCase()}`, status, createdAt: '2026-11-15T09:00:00Z', expiresAt: '2026-11-29T09:00:00Z', paidDeclaredAt: null, payerNote: null,
    hasReceipt: false, reviewedAt: null, reviewNote: null, documentsVersion: '2026-11-15', ...extra },
  accountName: 'Deneme Öğrenci', accountEmail: 'ogrenci@example.invalid', orderedByUserId: 'u1', beneficiaryUserId: 'u1', reviewedBy: null
});
const waiting = order('aaa111', 'Pending', { paidDeclaredAt: '2026-11-15T10:00:00Z', payerNote: 'Gönderdim', hasReceipt: true });
const paid = order('bbb222', 'Paid');
const subscription: BillingAdminSubscription = {
  subscription: { id: 's1', planId: 'pl1', planName: 'TEST Aylık', productCodes: ['speed-reading'], status: 'Active', startsAt: '2026-11-15T09:00:00Z',
    endsAt: '2026-12-15T09:00:00Z', isCurrent: true, seatCount: null },
  accountName: 'Deneme Öğrenci', accountEmail: null, beneficiaryUserId: 'u1'
};

function setup<T>(component: new () => T, overrides: Record<string, unknown> = {}) {
  const billing = {
    getProducts: vi.fn(() => of([product])),
    getOrders: vi.fn(() => of([waiting, paid])),
    approveOrder: vi.fn(() => of(waiting)),
    rejectOrder: vi.fn(() => of(waiting)),
    reverseApproval: vi.fn(() => of(paid)),
    getReceipt: vi.fn(() => of(new Blob(['%PDF']))),
    getSubscriptions: vi.fn(() => of([subscription])),
    ...overrides
  };
  const toaster = { success: vi.fn() };
  TestBed.configureTestingModule({ imports: [component as never], providers: [
    { provide: BillingAdminService, useValue: billing }, { provide: ToasterService, useValue: toaster }] });
  const fixture = TestBed.createComponent(component as never);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance as T, billing, toaster, element: fixture.nativeElement as HTMLElement };
}

describe('Abonelik ve Ödeme: orders and subscriptions', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('opens on the waiting orders and shows code, amount, declaration and receipt', () => {
    const { billing, element } = setup(BillingOrdersComponent);
    expect(billing.getOrders).toHaveBeenCalledWith('Pending', '');
    const row = element.querySelector('[data-testid="order-EDU-AAA111"]')!;
    expect(row.textContent).toContain('Deneme Öğrenci');
    expect(row.textContent).toContain('EDU-AAA111');
    expect(row.textContent).toMatch(/200[.,]00 TRY/);
    expect(row.textContent).toContain('Master Hızlı Okuma');
    expect(row.textContent).toContain('"Gönderdim"');
    expect(row.textContent).toContain('Dekontu aç');
    expect(row.querySelector('.approve')).not.toBeNull();
    const paidRow = element.querySelector('[data-testid="order-EDU-BBB222"]')!;
    expect(paidRow.querySelector('.approve')).toBeNull();
    expect(paidRow.textContent).toContain('Onayı geri al');
  });

  it('approves only after the admin confirms the code and amount', () => {
    const { component, billing, element, fixture, toaster } = setup(BillingOrdersComponent);
    component.start(waiting, 'approve');
    fixture.detectChanges();
    expect(billing.approveOrder).not.toHaveBeenCalled();
    const form = element.querySelector('[data-testid="review-form"]')!;
    expect(form.textContent).toContain('EDU-AAA111');
    expect(form.textContent).toMatch(/200[.,]00 TRY/);
    component.confirm();
    expect(billing.approveOrder).toHaveBeenCalledWith('aaa111');
    expect(toaster.success).toHaveBeenCalledWith('Ödeme onaylandı; abonelik açıldı.');
    expect(billing.getOrders).toHaveBeenCalledTimes(2);
    expect(component.review()).toBeNull();
  });

  it('a refusal and a reversal need a reason', () => {
    const { component, billing, element, fixture } = setup(BillingOrdersComponent);
    component.start(waiting, 'reject');
    fixture.detectChanges();
    const submit = () => element.querySelector('[data-testid="review-form"] button[type=submit]') as HTMLButtonElement;
    expect(submit().disabled).toBe(true);
    component.review.set({ ...component.review()!, note: '  Ödeme hesabımıza ulaşmadı.  ' });
    fixture.detectChanges();
    expect(submit().disabled).toBe(false);
    component.confirm();
    expect(billing.rejectOrder).toHaveBeenCalledWith('aaa111', 'Ödeme hesabımıza ulaşmadı.');

    component.start(paid, 'reverse');
    component.review()!.note = 'Ödeme bankadan geri döndü.';
    component.confirm();
    expect(billing.reverseApproval).toHaveBeenCalledWith('bbb222', 'Ödeme bankadan geri döndü.');
  });

  it('shows why a review was refused and reloads what is true now', () => {
    const { component, billing, element, fixture } = setup(BillingOrdersComponent, { approveOrder: vi.fn(() =>
      throwError(() => new HttpErrorResponse({ status: 400, error: { detail: 'Bu sipariş daha önce sonuçlandı.' } }))) });
    component.start(waiting, 'approve');
    component.confirm();
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Bu sipariş daha önce sonuçlandı.');
    expect(component.review()).toBeNull();
    expect(billing.getOrders).toHaveBeenCalledTimes(2);
  });

  it('filters by status and product', () => {
    const { component, billing } = setup(BillingOrdersComponent);
    component.status.set('');
    component.product.set('speed-reading');
    component.load();
    expect(billing.getOrders).toHaveBeenLastCalledWith('', 'speed-reading');
  });

  it('subscriptions list account, plan, dates and state, filtered by product', () => {
    const { component, billing, element } = setup(BillingSubscriptionsComponent);
    const row = element.querySelector('tbody tr')!.textContent!;
    expect(row).toContain('Deneme Öğrenci');
    expect(row).toContain('Master Hızlı Okuma');
    expect(row).toContain('15.12.2026');
    expect(row).toContain('Sürüyor');
    expect(component.stateLabel({ ...subscription, subscription: { ...subscription.subscription, isCurrent: false, status: 'Cancelled' } })).toBe('Sonlandırıldı');
    expect(component.stateLabel({ ...subscription, subscription: { ...subscription.subscription, isCurrent: false, endsAt: '2020-01-01T00:00:00Z', startsAt: '2019-12-01T00:00:00Z' } })).toBe('Bitti');
    component.product.set('speed-reading');
    component.load();
    expect(billing.getSubscriptions).toHaveBeenLastCalledWith('speed-reading');
  });
});
