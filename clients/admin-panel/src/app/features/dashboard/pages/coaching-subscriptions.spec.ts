import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { CoachingBankTransferRequest, CoachingManagementService, CoachingSubscriptionPlanRequest } from '../../../core/services/coaching-management.service';
import { IdentityService } from '../../../core/services/identity.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingSubscriptionsComponent } from './coaching-subscriptions';

describe('CoachingSubscriptionsComponent', () => {
  it('opens the persisted requirement setting first and preserves bank data when toggling', async () => {
    const saved = { requireActiveSubscription: false, currency: 'TRY', accountHolder: 'Original', bankName: 'Bank',
      iban: 'TR330006100519786457841326', paymentInstructions: 'Original instructions', bankTransferEnabled: true,
      isPubliclyAvailable: true, updatedAt: null };
    const service = { getPlans: vi.fn(() => of([])), getSubscriptionSettings: vi.fn(() => of(saved)),
      updateSubscriptionSettings: vi.fn(request => of({ ...saved, ...request })) };
    await TestBed.configureTestingModule({ imports: [CoachingSubscriptionsComponent], providers: [
      { provide: CoachingManagementService, useValue: service },
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: IdentityService, useValue: {} }, { provide: CoachingAdminService, useValue: {} },
      { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
      { provide: ToasterService, useValue: { success: vi.fn() } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(CoachingSubscriptionsComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance;
    expect(component.tab()).toBe('requirement');
    expect(component.tabs[0].label).toBe('Abonelik ayarı');
    expect(fixture.nativeElement.querySelector('input[name="accountHolder"]')).toBeNull();
    const toggle = fixture.nativeElement.querySelector('[role="switch"]') as HTMLInputElement;
    expect(toggle.checked).toBe(false);
    toggle.click(); await fixture.whenStable();
    fixture.nativeElement.querySelector('form button[type="submit"]').click(); await fixture.whenStable();
    expect(service.updateSubscriptionSettings).toHaveBeenLastCalledWith(expect.objectContaining({
      requireActiveSubscription: true, accountHolder: 'Original', bankName: 'Bank', bankTransferEnabled: true
    }));
    expect(fixture.nativeElement.textContent).toContain('Açık — abonelik gerekli');
    toggle.click(); await fixture.whenStable();
    fixture.nativeElement.querySelector('form button[type="submit"]').click(); await fixture.whenStable();
    expect(component.settings()?.requireActiveSubscription).toBe(false);
    component.selectTab('settings'); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="switch"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="accountHolder"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Banka ve ödeme ayarları');
    component.settingsDraft.requireActiveSubscription = true;
    component.settingsDraft.bankName = 'Updated bank'; await component.saveSettings();
    expect(service.updateSubscriptionSettings).toHaveBeenLastCalledWith(expect.objectContaining({
      requireActiveSubscription: false, bankName: 'Updated bank'
    }));
    component.settingsDraft.bankName = 'Unsaved bank';
    component.settingsDraft.requireActiveSubscription = true; await component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionSettings).toHaveBeenLastCalledWith(expect.objectContaining({
      requireActiveSubscription: true, bankName: 'Updated bank'
    }));
  });

  it('blocks failed-load defaults and overlapping settings requests', async () => {
    const saved = { requireActiveSubscription: false, currency: 'TRY', accountHolder: null, bankName: null,
      iban: null, paymentInstructions: null, bankTransferEnabled: false, isPubliclyAvailable: false, updatedAt: null };
    const service = { getPlans: () => of([]), getSubscriptionSettings: vi.fn(() => of(saved)), updateSubscriptionSettings: vi.fn(request => of({ ...saved, ...request })) };
    await TestBed.configureTestingModule({ imports: [CoachingSubscriptionsComponent], providers: [
      { provide: CoachingManagementService, useValue: service }, { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: IdentityService, useValue: {} }, { provide: CoachingAdminService, useValue: {} },
      { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
      { provide: ToasterService, useValue: { success: vi.fn() } }
    ] }).compileComponents();
    const component = TestBed.createComponent(CoachingSubscriptionsComponent).componentInstance;
    service.getSubscriptionSettings.mockImplementationOnce(() => throwError(() => new Error('offline')));
    component.ngOnInit(); await component.saveSettings(); await component.saveSubscriptionRequirement();
    expect(service.updateSubscriptionSettings).not.toHaveBeenCalled();
    component.loadSettings();
    const pending = new Subject<typeof saved>(); service.getSubscriptionSettings.mockReturnValue(pending);
    component.loadSettings(); component.loadSettings(); await component.saveSettings(); await component.saveSubscriptionRequirement();
    expect(service.getSubscriptionSettings).toHaveBeenCalledTimes(3);
    expect(service.updateSubscriptionSettings).not.toHaveBeenCalled();
    pending.next(saved); pending.complete();
    const save = new Subject<typeof saved>(); service.updateSubscriptionSettings.mockReturnValue(save);
    component.settingsDraft.requireActiveSubscription = true;
    const operation = component.saveSubscriptionRequirement(); component.loadSettings();
    expect(service.getSubscriptionSettings).toHaveBeenCalledTimes(3);
    save.next({ ...saved, requireActiveSubscription: true }); save.complete(); await operation;
    expect(component.settings()?.requireActiveSubscription).toBe(true);
  });
  it.each([false, true])('lets administrators create a teacher plan; contact-only=%s', async (contactOnly) => {
    const createdPlans: CoachingSubscriptionPlanRequest[] = [];
    const service = {
      getPlans: vi.fn(() => of([])), getSubscriptionSettings: vi.fn(() => of({ requireActiveSubscription: false, currency: 'TRY', accountHolder: null, bankName: null, iban: null, paymentInstructions: null, bankTransferEnabled: false, isPubliclyAvailable: false, updatedAt: null })),
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
    component.selectTab('plans');
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
      getPlans: vi.fn(() => of([])), getSubscriptionSettings: vi.fn(() => of({ requireActiveSubscription: false, currency: 'TRY', accountHolder: null, bankName: null, iban: null, paymentInstructions: null, bankTransferEnabled: false, isPubliclyAvailable: false, updatedAt: null })),
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
      getPlans: vi.fn(() => of([])), getSubscriptionSettings: vi.fn(() => of({ requireActiveSubscription: false, currency: 'TRY', accountHolder: null, bankName: null, iban: null, paymentInstructions: null, bankTransferEnabled: false, isPubliclyAvailable: false, updatedAt: null })),
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
