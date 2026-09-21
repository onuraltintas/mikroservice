import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
  const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 25 };

  function createComponent() {
    const service = {
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
