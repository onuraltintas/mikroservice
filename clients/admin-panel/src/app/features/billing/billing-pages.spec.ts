import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../core/auth/permissions';
import { BillingAdminService, BillingPlan, BillingProduct, billingErrorMessage } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { DASHBOARD_ROUTES } from '../dashboard/dashboard.routes';
import { SidebarComponent } from '../dashboard/layout/sidebar/sidebar';
import { LayoutService } from '../dashboard/services/layout.service';
import { BillingPlansComponent } from './billing-plans';
import { BillingProductsComponent } from './billing-products';
import { BillingSettingsComponent } from './billing-settings';

const coaching: BillingProduct = {
  id: 'p-coaching', code: 'coaching', name: 'Eduİvme Koçluk', requireSubscription: false, bankTransferApproval: 'Manual', isActive: true, sortOrder: 1,
  entitlements: [
    { id: 'e-student', key: 'coaching.student', name: 'Koçluk öğrenci erişimi', hasLimit: false },
    { id: 'e-teacher', key: 'coaching.teacher', name: 'Koçluk öğretmen erişimi', hasLimit: false }
  ]
};
const courses: BillingProduct = {
  id: 'p-courses', code: 'course-management', name: 'Kurs Yönetimi', requireSubscription: false, bankTransferApproval: 'Manual', isActive: true, sortOrder: 3,
  entitlements: [{ id: 'e-workspace', key: 'course-management.workspace', name: 'Çalışma alanı', hasLimit: true }]
};
const teacherPlan: BillingPlan = {
  id: 'plan-1', name: 'Öğretmen 10', description: '', buyerType: 'Teacher', price: 900, currency: 'TRY', durationDays: 30, trialDays: 0,
  seatCount: 10, extraSeatPrice: null, isContactOnly: false, isActive: true, isPublic: true, sortOrder: 1, features: ['Bir', 'İki'],
  entitlements: [
    { entitlementId: 'e-teacher', key: 'coaching.teacher', name: 'Koçluk öğretmen erişimi', productCode: 'coaching', holder: 'Buyer', limitValue: null },
    { entitlementId: 'e-student', key: 'coaching.student', name: 'Koçluk öğrenci erişimi', productCode: 'coaching', holder: 'SeatHolder', limitValue: null }
  ],
  productCodes: ['coaching']
};
const seller = { title: null, address: null, taxOffice: null, taxNumber: null, mersisNumber: null, email: null, phone: null, alertEmail: null, isComplete: false };
const account = { id: 'a-1', bankName: 'Birinci Banka', accountHolder: 'Deneme Ltd.', iban: 'TR330006100519786457841326',
  ibanFormatted: 'TR33 0006 1005 1978 6457 8413 26', instructions: null, statementTemplate: 'Halkbank', isActive: true, sortOrder: 1 };
const refused = (detail: string) => new HttpErrorResponse({ status: 400, error: { detail, code: 'Billing.Invalid' } });

function setup<T>(component: new () => T, overrides: Record<string, unknown> = {}) {
  const billing = {
    getProducts: vi.fn(() => of([coaching, courses])),
    getPlans: vi.fn(() => of([teacherPlan])),
    getSeller: vi.fn(() => of(seller)),
    getBankAccounts: vi.fn(() => of([account])),
    updateProductSettings: vi.fn((_: string, request: object) => of({ ...coaching, ...request })),
    createProduct: vi.fn(() => of(coaching)),
    updateProduct: vi.fn(() => of(coaching)),
    addEntitlement: vi.fn(() => of(coaching)),
    updateEntitlement: vi.fn(() => of(coaching)),
    removeEntitlement: vi.fn(() => of(undefined)),
    createPlan: vi.fn(() => of(teacherPlan)),
    updatePlan: vi.fn(() => of(teacherPlan)),
    updateSeller: vi.fn((request: object) => of({ ...request, isComplete: true })),
    createBankAccount: vi.fn(() => of(account)),
    updateBankAccount: vi.fn(() => of(account)),
    removeBankAccount: vi.fn(() => of(undefined)),
    ...overrides
  };
  const toaster = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({ imports: [component as never], providers: [
    { provide: BillingAdminService, useValue: billing }, { provide: ToasterService, useValue: toaster }] });
  const fixture = TestBed.createComponent(component as never);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance as T, billing, toaster, element: fixture.nativeElement as HTMLElement };
}

describe('Abonelik ve Ödeme pages', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('are three SystemAdmin-only routes and one menu section', () => {
    for (const path of ['billing/orders', 'billing/subscriptions', 'billing/products', 'billing/plans', 'billing/settings']) {
      const route = DASHBOARD_ROUTES.find(item => item.path === path);
      expect(route?.data?.['role'], path).toBe('SystemAdmin');
      expect(route?.canActivate?.length, path).toBe(1);
    }

    const profile = signal({ roles: ['SystemAdmin'], permissions: [ADMIN_PERMISSIONS.operationsView] as string[] });
    TestBed.configureTestingModule({ imports: [SidebarComponent], providers: [
      { provide: AuthService, useValue: { userProfile: profile, hasPermission: (permission: string) => profile().permissions.includes(permission) } },
      { provide: ToasterService, useValue: { confirm: vi.fn() } },
      { provide: LayoutService, useValue: { isSidebarCollapsed: signal(false), toggleSidebar: vi.fn() } }] });
    const sidebar = TestBed.createComponent(SidebarComponent).componentInstance;
    const section = () => sidebar.menuItems().find(item => item.label === 'Abonelik ve Ödeme');
    expect(section()?.children?.map((child: { route: string }) => child.route))
      .toEqual(['/dashboard/billing/orders', '/dashboard/billing/subscriptions', '/dashboard/billing/products', '/dashboard/billing/plans', '/dashboard/billing/settings']);
    // Another manager with every permission still does not see it: the service answers the system admin only.
    profile.set({ roles: ['Editor'], permissions: Object.values(ADMIN_PERMISSIONS) as string[] });
    expect(section()).toBeUndefined();
  });

  it('shows the server explanation for a refused change and a fallback otherwise', () => {
    expect(billingErrorMessage(refused('IBAN geçersiz.'), 'Kaydedilemedi.')).toBe('IBAN geçersiz.');
    expect(billingErrorMessage(new HttpErrorResponse({ status: 500, error: { detail: 'stack' } }), 'Kaydedilemedi.')).toBe('Kaydedilemedi.');
    expect(billingErrorMessage(new Error('x'), 'Kaydedilemedi.')).toBe('Kaydedilemedi.');
  });

  it('products: each product has its own subscription switch and approval mode, saved only when changed', () => {
    const { component, billing, element, fixture } = setup(BillingProductsComponent);
    expect(element.querySelectorAll('[data-testid^="settings-"]').length).toBe(2);
    expect(component.settingsChanged(coaching)).toBe(false);
    expect((element.querySelector('[data-testid="settings-coaching"] button[type=submit]') as HTMLButtonElement).disabled).toBe(true);

    component.settingsDrafts()[coaching.id].requireSubscription = true;
    component.settingsDrafts()[coaching.id].bankTransferApproval = 'Automatic';
    expect(component.settingsChanged(coaching)).toBe(true);
    expect(component.settingsChanged(courses)).toBe(false);
    component.saveSettings(coaching);

    expect(billing.updateProductSettings).toHaveBeenCalledWith('p-coaching', { requireSubscription: true, bankTransferApproval: 'Automatic' });
    fixture.detectChanges();
    expect(component.products()[0].requireSubscription).toBe(true);
    expect(component.products()[1].requireSubscription).toBe(false);
    expect(element.querySelector('[data-testid="product-coaching"]')?.textContent).toContain('abonelik isteniyor · otomatik onay');
  });

  it('products: a new entitlement starts with the product code and deleting asks first', () => {
    const { component, billing, element, fixture } = setup(BillingProductsComponent);
    component.startEntitlement(coaching);
    expect(component.entitlementDraft()?.key).toBe('coaching.');
    component.entitlementDraft()!.key = ' coaching.parent ';
    component.entitlementDraft()!.name = 'Veli erişimi';
    component.saveEntitlement();
    expect(billing.addEntitlement).toHaveBeenCalledWith('p-coaching', { key: 'coaching.parent', name: 'Veli erişimi', hasLimit: false });

    expect(billing.removeEntitlement).not.toHaveBeenCalled();
    component.removingId.set('e-student');
    fixture.detectChanges();
    expect(element.textContent).toContain('Silinsin mi?');
    component.removeEntitlement(coaching.entitlements[0]);
    expect(billing.removeEntitlement).toHaveBeenCalledWith('e-student');
  });

  it('products: a refused change shows why', () => {
    const { component, element, fixture } = setup(BillingProductsComponent,
      { removeEntitlement: vi.fn(() => throwError(() => refused('Bu hakkı veren plan var; önce plandan çıkarın.'))) });
    component.removeEntitlement(coaching.entitlements[0]);
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Bu hakkı veren plan var');
  });

  it('plans: the list names buyer, seats and products, and filters by product', () => {
    const { component, element } = setup(BillingPlansComponent);
    const row = element.querySelector('tbody tr')?.textContent ?? '';
    expect(row).toContain('Öğretmen 10');
    expect(row).toContain('Öğretmen');
    expect(row).toContain('Eduİvme Koçluk');
    component.productFilter.set('course-management');
    expect(component.visiblePlans()).toEqual([]);
    component.productFilter.set('coaching');
    expect(component.visiblePlans().length).toBe(1);
  });

  it('plans: editing loads who gets what, and the request carries buyer and seat-holder entitlements', () => {
    const { component, billing } = setup(BillingPlansComponent);
    component.start(teacherPlan);
    const draft = component.draft()!;
    const choices = draft.groups.flatMap(group => group.choices);
    expect(choices.find(choice => choice.key === 'coaching.teacher')).toMatchObject({ buyer: true, seatHolder: false });
    expect(choices.find(choice => choice.key === 'coaching.student')).toMatchObject({ buyer: false, seatHolder: true });
    expect(draft.features).toBe('Bir\nİki');

    component.save();
    expect(billing.updatePlan).toHaveBeenCalledWith('plan-1', expect.objectContaining({
      buyerType: 'Teacher', seatCount: 10, extraSeatPrice: null, features: ['Bir', 'İki'],
      entitlements: [
        { entitlementId: 'e-student', holder: 'SeatHolder', limitValue: null },
        { entitlementId: 'e-teacher', holder: 'Buyer', limitValue: null }
      ]
    }));
  });

  it('plans: an individual plan has no seats, an institution plan no trial, and a limit goes with its entitlement', () => {
    const { component, billing } = setup(BillingPlansComponent);
    component.start(teacherPlan);
    const draft = component.draft()!;
    draft.buyerType = 'Individual';
    component.syncBuyer();
    expect(draft.hasSeats).toBe(false);
    expect(draft.groups.flatMap(group => group.choices).some(choice => choice.seatHolder)).toBe(false);
    expect(component.toRequest(draft).seatCount).toBeNull();

    component.start();
    const institution = component.draft()!;
    Object.assign(institution, { name: 'Kurs 100', buyerType: 'Institution', price: 2000, durationDays: 365, trialDays: 14 });
    component.syncBuyer();
    const workspace = institution.groups.flatMap(group => group.choices).find(choice => choice.key === 'course-management.workspace')!;
    workspace.buyer = true;
    workspace.limitValue = 100;
    component.save();
    expect(billing.createPlan).toHaveBeenCalledWith(expect.objectContaining({
      buyerType: 'Institution', trialDays: 0, seatCount: null,
      entitlements: [{ entitlementId: 'e-workspace', holder: 'Buyer', limitValue: 100 }]
    }));
  });

  it('plans: a plan that gives nothing is not sent', () => {
    const { component, billing } = setup(BillingPlansComponent);
    component.start();
    component.draft()!.name = 'Boş';
    component.save();
    expect(billing.createPlan).not.toHaveBeenCalled();
    expect(component.error()).toBe('Plan en az bir hak vermelidir.');
  });

  it('settings: seller state follows the server and empty fields are sent as null', () => {
    const { component, billing, element, fixture } = setup(BillingSettingsComponent);
    expect(element.querySelector('[data-testid="seller-state"]')?.textContent).toContain('Eksik');
    component.sellerDraft = { title: ' Deneme Ltd. ', address: 'Bir adres', taxOffice: 'Daire', taxNumber: '1234567890', mersisNumber: '',
      email: 'satici@example.com', phone: '0212', alertEmail: '' };
    component.saveSeller();
    expect(billing.updateSeller).toHaveBeenCalledWith(expect.objectContaining({ title: 'Deneme Ltd.', mersisNumber: null, alertEmail: null }));
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="seller-state"]')?.textContent).toContain('Satış belgeleri hazır');
  });

  it('settings: several bank accounts are listed, edited with their formatted IBAN and deleted after asking', () => {
    const { component, billing, element, fixture } = setup(BillingSettingsComponent);
    expect(element.querySelector('tbody')?.textContent).toContain('TR33 0006 1005 1978 6457 8413 26');
    component.startAccount(account);
    expect(component.accountDraft()).toMatchObject({ id: 'a-1', iban: 'TR33 0006 1005 1978 6457 8413 26', statementTemplate: 'Halkbank' });
    component.saveAccount();
    expect(billing.updateBankAccount).toHaveBeenCalledWith('a-1', expect.objectContaining({ bankName: 'Birinci Banka', statementTemplate: 'Halkbank', instructions: null }));

    component.startAccount();
    expect(component.accountDraft()).toMatchObject({ id: null, sortOrder: 2, isActive: true });
    component.removingId.set('a-1');
    fixture.detectChanges();
    expect(element.textContent).toContain('Silinsin mi?');
    expect(billing.removeBankAccount).not.toHaveBeenCalled();
    component.removeAccount(account);
    expect(billing.removeBankAccount).toHaveBeenCalledWith('a-1');
  });

  it('settings: a refused IBAN shows the server explanation and keeps the form open', () => {
    const { component, element, fixture } = setup(BillingSettingsComponent,
      { createBankAccount: vi.fn(() => throwError(() => refused('IBAN geçersiz; TR ile başlayan 26 karakterlik IBAN girin.'))) });
    component.startAccount();
    Object.assign(component.accountDraft()!, { bankName: 'Banka', accountHolder: 'Sahip', iban: 'TR00' });
    component.saveAccount();
    fixture.detectChanges();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('IBAN geçersiz');
    expect(component.accountDraft()).not.toBeNull();
  });
});
