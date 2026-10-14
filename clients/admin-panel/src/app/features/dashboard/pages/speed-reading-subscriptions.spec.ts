import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  SpeedReadingAdminService,
  SpeedReadingPlan,
  SpeedReadingSubscription
} from '../../../core/services/speed-reading-admin.service';
import { IdentityService } from '../../../core/services/identity.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { SpeedReadingSubscriptionsComponent } from './speed-reading-subscriptions';

describe('SpeedReadingSubscriptionsComponent', () => {
  it('distinguishes historical requests from an adult payer self-declaration', () => {
    const { component } = createComponent();
    expect(component.adultPayerLabel({ adultPayerDeclarationVersion: null, adultPayerDeclaredAt: null })).toBe('Yetişkin beyanı kaydı yok (eski talep)');
    expect(component.adultPayerLabel({ adultPayerDeclarationVersion: 1, adultPayerDeclaredAt: '2026-10-05T09:00:00Z' })).toContain('yaş doğrulaması değildir');
  });
  const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 25 };

  function createComponent() {
    const service = {
      getSubscriptionRequirementSettings: vi.fn(() => of({ requireActiveSubscription: true, updatedAt: null })),
      updateSubscriptionRequirementSettings: vi.fn((required: boolean) => of({ requireActiveSubscription: required, updatedAt: null })),
      getSubscriptionProducts: vi.fn(() => of([])),
      getSubscriptionPlans: vi.fn(() => of([])),
      getUserSubscriptions: vi.fn(() => of(emptyPage)),
      getBankTransferRequests: vi.fn(() => of(emptyPage)),
      updateUserSubscription: vi.fn(() => of({})),
      createInstitutionAccess: vi.fn(() => of({ createdCount: 2, existingCount: 0 })),
      deleteBankTransferRequest: vi.fn(() => of(undefined))
    };
    const toaster = {
      confirm: vi.fn(() => Promise.resolve(true)),
      success: vi.fn(),
      error: vi.fn(),
      prompt: vi.fn()
    };

    TestBed.configureTestingModule({
      imports: [SpeedReadingSubscriptionsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: SpeedReadingAdminService, useValue: service },
        { provide: IdentityService, useValue: { getAllUsers: vi.fn(() => of({ items: [] })) } },
        { provide: InstitutionService, useValue: { getAll: vi.fn(() => of({ items: [] })) } },
        { provide: ToasterService, useValue: toaster }
      ]
    });

    return {
      component: TestBed.createComponent(SpeedReadingSubscriptionsComponent).componentInstance,
      service,
      toaster
    };
  }

  it('renders the off/on switch and the saved state in the admin settings form', async () => {
    createComponent();
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(SpeedReadingSubscriptionsComponent); fixture.detectChanges(); await fixture.whenStable();
    const toggle = fixture.nativeElement.querySelector('[role="switch"]') as HTMLInputElement;
    expect(toggle.checked).toBe(true);
    toggle.click(); await fixture.whenStable();
    fixture.nativeElement.querySelector('form button[type="submit"]').click(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Kapalı — abonelik gerekli değil');
    toggle.click(); await fixture.whenStable();
    fixture.nativeElement.querySelector('form button[type="submit"]').click(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Açık — abonelik gerekli');
  });

  it('loads the saved requirement first and saves off then on without changing bank settings', () => {
    const { component, service } = createComponent();
    component.ngOnInit();
    expect(component.selectedTab()).toBe('settings');
    expect(component.requireActiveSubscription).toBe(true);
    component.requireActiveSubscription = false; component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionRequirementSettings).toHaveBeenLastCalledWith(false);
    expect(component.subscriptionRequirement()?.requireActiveSubscription).toBe(false);
    component.requireActiveSubscription = true; component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionRequirementSettings).toHaveBeenLastCalledWith(true);
    expect(component.subscriptionRequirement()?.requireActiveSubscription).toBe(true);
  });
  it('keeps the published state when saving fails', () => {
    const { component, service } = createComponent(); component.ngOnInit();
    service.updateSubscriptionRequirementSettings.mockImplementation(() => throwError(() => new Error('offline')));
    component.requireActiveSubscription = false; component.saveSubscriptionRequirement();
    expect(component.subscriptionRequirement()?.requireActiveSubscription).toBe(true);
    expect(component.saving()).toBe(false); expect(component.error()).toContain('kaydedilemedi');
  });
  it('does not save a default value if the persisted setting could not be loaded', () => {
    const { component, service } = createComponent();
    service.getSubscriptionRequirementSettings.mockImplementation(() => throwError(() => new Error('offline')));
    component.ngOnInit(); component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionRequirementSettings).not.toHaveBeenCalled();
  });

  it('prevents overlapping settings reads and writes from reverting a saved switch', () => {
    const { component, service } = createComponent(); component.ngOnInit();
    const pending = new Subject<{ requireActiveSubscription: boolean; updatedAt: null }>();
    service.getSubscriptionRequirementSettings.mockReturnValue(pending);
    component.loadSubscriptionRequirement(); component.loadSubscriptionRequirement();
    component.requireActiveSubscription = false; component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionRequirementSettings).not.toHaveBeenCalled();
    expect(service.getSubscriptionRequirementSettings).toHaveBeenCalledTimes(2);
    pending.next({ requireActiveSubscription: true, updatedAt: null }); pending.complete();
    const save = new Subject<{ requireActiveSubscription: boolean; updatedAt: null }>();
    service.updateSubscriptionRequirementSettings.mockReturnValue(save);
    component.requireActiveSubscription = false; component.saveSubscriptionRequirement(); component.loadSubscriptionRequirement();
    expect(service.getSubscriptionRequirementSettings).toHaveBeenCalledTimes(2);
    save.next({ requireActiveSubscription: false, updatedAt: null }); save.complete();
    expect(component.subscriptionRequirement()?.requireActiveSubscription).toBe(false);
  });

  it('loads plans when the user opens the subscriptions tab', () => {
    const { component, service } = createComponent();

    component.selectTab('subscriptions');

    expect(service.getSubscriptionPlans).toHaveBeenCalledTimes(1);
    expect(service.getUserSubscriptions).toHaveBeenCalledTimes(1);
  });

  it('permanently deletes a bank transfer request after confirmation', async () => {
    const { component, service, toaster } = createComponent();
    const request = {
      id: 'request-1',
      userId: 'user-1',
      userName: 'Ada',
      userEmail: 'ada@example.test',
      planId: 'plan-1',
      planName: 'Bireysel',
      amount: 1000,
      currency: 'TRY',
      paymentReference: 'EFT-1',
      payerName: null,
      note: null,
      status: 'Approved',
      subscriptionId: 'subscription-1',
      reviewedBy: 'admin-1',
      reviewedAt: '2026-09-21T00:00:00Z',
      reviewNote: null,
      createdAt: '2026-09-21T00:00:00Z',
      updatedAt: '2026-09-21T00:00:00Z'
    } as any;

    await component.deleteBankTransferRequest(request);

    expect(toaster.confirm).toHaveBeenCalled();
    expect(service.deleteBankTransferRequest).toHaveBeenCalledWith('request-1');
    expect(toaster.success).toHaveBeenCalledWith('EFT talebi kalıcı olarak silindi.');
  });

  it('updates an existing manual subscription with editable fields', () => {
    const { component, service } = createComponent();
    const subscription = {
      id: 'subscription-1',
      userId: 'user-1',
      userName: 'Ada',
      userEmail: 'ada@example.test',
      plan: { id: 'plan-1', name: 'Aylık' } as SpeedReadingPlan,
      productSlug: 'hizliokuma',
      productName: 'Hızlı Okuma',
      status: 'Active',
      startDate: '2026-08-01T00:00:00Z',
      endDate: null,
      notes: null,
      createdAt: '2026-08-01T00:00:00Z',
      isActive: true
    } as SpeedReadingSubscription;

    component.editSubscription(subscription);
    component.subscriptionUpdateStatus = 'Cancelled';
    component.subscriptionDraft.endDate = '2026-09-30';
    component.subscriptionDraft.notes = 'Kullanıcı talebi';
    component.saveSubscription();

    expect(service.updateUserSubscription).toHaveBeenCalledWith('subscription-1', {
      status: 'Cancelled',
      endDate: '2026-09-30',
      notes: 'Kullanıcı talebi'
    });
  });

  it('approves one-year access for the selected institution students in one request', () => {
    const { component, service } = createComponent();
    component.institutions.set([{ id: 'institution-1', name: 'Örnek Okul' } as any]);
    component.institutionStudents.set([
      { userId: 'student-1', fullName: 'Ada', email: 'ada@example.test' },
      { userId: 'student-2', fullName: 'Can', email: 'can@example.test' }
    ] as any);
    component.institutionId = 'institution-1';
    component.institutionPlanId = 'annual-plan';
    component.institutionStartDate = '2026-09-14';
    component.institutionPaymentReference = 'EFT-2026-0001';
    component.selectedInstitutionStudentIds.add('student-1');
    component.selectedInstitutionStudentIds.add('student-2');

    component.approveInstitutionAccess();

    expect(service.createInstitutionAccess).toHaveBeenCalledWith({
      institutionId: 'institution-1',
      planId: 'annual-plan',
      startDate: '2026-09-14',
      recipients: [
        { userId: 'student-1', userName: 'Ada', userEmail: 'ada@example.test' },
        { userId: 'student-2', userName: 'Can', userEmail: 'can@example.test' }
      ],
      paymentReference: 'EFT-2026-0001',
      notes: 'Örnek Okul'
    });
  });
});
