import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { BillingAdminService, BillingAdminSubscription, BillingOverview } from '../../core/services/billing-admin.service';
import { BillingOverviewComponent } from './billing-overview';
import { BillingSubscriptionsComponent } from './billing-subscriptions';

const row = (over: Partial<BillingAdminSubscription['subscription']> = {}): BillingAdminSubscription => ({
  subscription: { id: 's1', planId: 'pl1', planName: 'TEST Aylık', productCodes: ['coaching'], status: 'Active', startsAt: '2026-11-01T09:00:00Z',
    endsAt: '2026-11-19T09:00:00Z', isCurrent: true, seatCount: null, ...over },
  accountName: 'Deneme Öğrenci', accountEmail: 'ogrenci@example.invalid', beneficiaryUserId: 'u1' });
const overview: BillingOverview = { waitingOrders: 4, declaredOrders: 3, withdrawalRequests: 1, unmatchedTransactions: 2, currentSubscriptions: 12,
  approvedThisMonth: 1250.5, approvedLastMonth: 900, currency: 'TRY', endingSoon: [row()] };
const plans = [{ id: 'pl1', name: 'TEST Aylık', buyerType: 'Individual' }, { id: 'pl3', name: 'TEST Kurum', buyerType: 'Institution' }];
const uid = '11111111-1111-4111-8111-111111111111';

function setup<T>(component: new () => T, overrides: Record<string, unknown> = {}) {
  const billing = {
    getProducts: vi.fn(() => of([{ id: 'p1', code: 'coaching', name: 'Eduİvme Koçluk' }])),
    getOverview: vi.fn(() => of(overview)),
    getSubscriptions: vi.fn(() => of([row()])),
    getPlans: vi.fn(() => of(plans)),
    grantSubscription: vi.fn(() => of(row())),
    extendSubscription: vi.fn(() => of(row())),
    cancelSubscription: vi.fn(() => of(row())),
    ...overrides
  };
  TestBed.configureTestingModule({ imports: [component as never], providers: [provideRouter([]), { provide: BillingAdminService, useValue: billing }] });
  const fixture = TestBed.createComponent(component as never);
  fixture.detectChanges();
  return { fixture, billing, page: fixture.componentInstance as T, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
}

describe('Abonelik ve Ödeme › Genel bakış', () => {
  it('shows what waits, what ends soon and what was approved, and filters by product', () => {
    const { fixture, billing, page, text } = setup(BillingOverviewComponent);
    const tile = (id: string) => (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`)!.textContent!;
    expect(tile('tile-orders')).toContain('4');
    expect(tile('tile-orders')).toContain('3 tanesi');
    expect(tile('tile-withdrawals')).toContain('1');
    expect(tile('tile-transactions')).toContain('2');
    expect(tile('tile-subscriptions')).toContain('1 tanesi 14 gün içinde bitiyor');
    expect(tile('tile-approved')).toMatch(/1[.,]250[.,]50 TL/);
    expect(text()).toContain('Deneme Öğrenci');
    page.product.set('coaching');
    page.load();
    expect(billing.getOverview).toHaveBeenLastCalledWith('coaching');
  });

  it('says so when it cannot be loaded', () => {
    const { text } = setup(BillingOverviewComponent, { getOverview: vi.fn(() => throwError(() => new Error('x'))) });
    expect(text()).toContain('Genel bakış yüklenemedi.');
  });
});

describe('Abonelik ve Ödeme › Abonelikler: elle verme, uzatma, sonlandırma', () => {
  it('gives a plan to a user, or to an institution when the plan is an institution plan, only with a reason', () => {
    const { fixture, billing, page, text } = setup(BillingSubscriptionsComponent);
    page.openGrant();
    fixture.detectChanges();
    expect(text()).toContain('Kullanıcı kimliği');
    page.form = { planId: 'pl1', holderId: 'x', displayName: 'Deneme Öğrenci', email: '', days: null, note: 'TEST: tanıtım' };
    page.grant();
    expect(page.error()).toBe('Kimlik numarası geçersiz.');
    page.form = { ...page.form, holderId: uid, note: ' ' };
    page.grant();
    expect(page.error()).toBe('Gerekçe yazın.');
    expect(billing.grantSubscription).not.toHaveBeenCalled();
    page.form = { ...page.form, note: 'TEST: tanıtım', days: 10 };
    page.grant();
    expect(billing.grantSubscription).toHaveBeenCalledWith({ userId: uid, organizationId: null, displayName: 'Deneme Öğrenci', email: null, planId: 'pl1', days: 10, note: 'TEST: tanıtım' });
    expect(page.granting()).toBe(false);

    page.openGrant();
    page.form = { planId: 'pl3', holderId: uid, displayName: 'TEST Kurum A.Ş.', email: 'kurum@example.invalid', days: null, note: 'TEST: sözleşme' };
    fixture.detectChanges();
    expect(text()).toContain('Kurum kimliği');
    page.grant();
    expect(billing.grantSubscription).toHaveBeenLastCalledWith({ userId: null, organizationId: uid, displayName: 'TEST Kurum A.Ş.', email: 'kurum@example.invalid', planId: 'pl3', days: null, note: 'TEST: sözleşme' });
  });

  it('extends and ends a subscription with a reason, and shows the server sentence when refused', () => {
    const { fixture, billing, page, text } = setup(BillingSubscriptionsComponent,
      { cancelSubscription: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 400, error: { detail: 'Bu abonelik zaten sona ermiş.' } }))) });
    page.begin(row(), 'extend');
    fixture.detectChanges();
    expect(text()).toContain('Eklenecek gün');
    page.confirm();
    expect(page.error()).toBe('Gerekçe yazın.');
    page.actionNote = 'TEST: telafi';
    page.actionDays = 5;
    page.confirm();
    expect(billing.extendSubscription).toHaveBeenCalledWith('s1', 5, 'TEST: telafi');
    expect(page.acting()).toBeNull();

    page.begin(row(), 'cancel');
    fixture.detectChanges();
    expect(text()).toContain('Ödeme kaydı değişmez');
    page.actionNote = 'TEST: kullanıcı istedi';
    page.confirm();
    expect(billing.cancelSubscription).toHaveBeenCalledWith('s1', 'TEST: kullanıcı istedi');
    expect(page.error()).toBe('Bu abonelik zaten sona ermiş.');
    expect(page.acting()).not.toBeNull();
  });
});
