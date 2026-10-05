import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { CoachingBankTransferRequest, CoachingManagementService, CoachingSubscriptionPlanRequest } from '../../../core/services/coaching-management.service';
import { IdentityService } from '../../../core/services/identity.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingSubscriptionsComponent } from './coaching-subscriptions';

describe('CoachingSubscriptionsComponent', () => {
  it.each([false, true])('lets administrators create a teacher plan; contact-only=%s', async (contactOnly) => {
    const createdPlans: CoachingSubscriptionPlanRequest[] = [];
    const service = {
      getPlans: vi.fn(() => of([])),
      createPlan: vi.fn((request: CoachingSubscriptionPlanRequest) => {
        createdPlans.push(request);
        return of({ data: { id: 'teacher-plan-id' } });
      })
    };

    await TestBed.configureTestingModule({
      imports: [CoachingSubscriptionsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CoachingManagementService, useValue: service },
        { provide: IdentityService, useValue: { getAllUsers: vi.fn(() => of({ items: [] })) } },
        { provide: InstitutionService, useValue: { getAll: vi.fn(() => of({ items: [] })) } },
        { provide: CoachingAdminService, useValue: { getStudentRoster: vi.fn(() => of({ students: [], totalCount: 0 })) } },
        { provide: ToasterService, useValue: { success: vi.fn(), warning: vi.fn(), confirm: vi.fn(), prompt: vi.fn() } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoachingSubscriptionsComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(component.adultPayerLabel({ adultPayerDeclarationVersion: null, adultPayerDeclaredAt: null })).toBe('Yetişkin beyanı kaydı yok (eski talep)');
    expect(component.adultPayerLabel({ adultPayerDeclarationVersion: 1, adultPayerDeclaredAt: '2026-10-05T09:00:00Z' })).toContain('yaş doğrulaması değildir');
    component.startPlanCreate();
    expect(component.planDraft.isPublic).toBe(false);
    component.planDraft = {
      ...component.planDraft,
      name: 'Bağımsız öğretmen',
      slug: 'bagimsiz-ogretmen',
      audience: 'Teacher',
      price: contactOnly ? 0 : 1000,
      isContactOnly: contactOnly,
      includedStudentSeats: contactOnly ? null : 12
    };

    fixture.detectChanges();
    const seatInput = fixture.nativeElement.querySelector('input[name="planSeats"]') as HTMLInputElement;
    expect(seatInput.required).toBe(!contactOnly);

    await component.savePlan();

    expect(createdPlans).toHaveLength(1);
    expect(createdPlans[0]).toMatchObject({ audience: 'Teacher', includedStudentSeats: contactOnly ? null : 12, name: 'Bağımsız öğretmen' });
  });

  it('requires a visible reason before rejecting an EFT request', async () => {
    const service = {
      getPlans: vi.fn(() => of([])),
      getTransferRequests: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 })),
      reviewTransferRequest: vi.fn(() => of(undefined))
    };
    const toaster = {
      success: vi.fn(), warning: vi.fn(), confirm: vi.fn().mockResolvedValue(true), prompt: vi.fn().mockResolvedValue('   ')
    };
    await TestBed.configureTestingModule({
      imports: [CoachingSubscriptionsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CoachingManagementService, useValue: service },
        { provide: IdentityService, useValue: { getAllUsers: vi.fn(() => of({ items: [] })) } },
        { provide: InstitutionService, useValue: { getAll: vi.fn(() => of({ items: [] })) } },
        { provide: CoachingAdminService, useValue: { getStudentRoster: vi.fn(() => of({ students: [], totalCount: 0 })) } },
        { provide: ToasterService, useValue: toaster }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoachingSubscriptionsComponent);
    fixture.detectChanges();
    await fixture.componentInstance.reviewRequest({ id: 'request-1', userName: 'Ada', amount: 500, currency: 'TRY' } as CoachingBankTransferRequest, 'Rejected');

    expect(service.reviewTransferRequest).not.toHaveBeenCalled();
    expect(toaster.warning).toHaveBeenCalledWith('EFT bildirimi için ret gerekçesi zorunludur.');
  });

  it('warns that deleting an approved EFT request preserves access and payment history', async () => {
    const service = {
      getPlans: vi.fn(() => of([])),
      getTransferRequests: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 })),
      deleteTransferRequest: vi.fn(() => of(undefined))
    };
    const toaster = {
      success: vi.fn(), warning: vi.fn(), confirm: vi.fn().mockResolvedValue(true), prompt: vi.fn()
    };
    await TestBed.configureTestingModule({
      imports: [CoachingSubscriptionsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CoachingManagementService, useValue: service },
        { provide: IdentityService, useValue: { getAllUsers: vi.fn(() => of({ items: [] })) } },
        { provide: InstitutionService, useValue: { getAll: vi.fn(() => of({ items: [] })) } },
        { provide: CoachingAdminService, useValue: { getStudentRoster: vi.fn(() => of({ students: [], totalCount: 0 })) } },
        { provide: ToasterService, useValue: toaster }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CoachingSubscriptionsComponent);
    fixture.detectChanges();
    await fixture.componentInstance.deleteRequest({ id: 'request-1', status: 'Approved' } as CoachingBankTransferRequest);

    expect(toaster.confirm.mock.calls[0][0]).toContain('Açılmış abonelik ve ödeme geçmişi korunur; erişim kapanmaz.');
    expect(service.deleteTransferRequest).toHaveBeenCalledWith('request-1');
  });
});
