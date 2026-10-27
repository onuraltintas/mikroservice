import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { CoachingSubscriptionComponent } from './coaching-subscription.component';

describe('CoachingSubscriptionComponent', () => {
  it('does not submit a payment without an explicit adult payer declaration', async () => {
    const fixture = setup();
    fixture.detectChanges(); await fixture.whenStable();
    fixture.componentInstance.selectedPlanId.set('individual-plan');
    fixture.componentInstance.paymentReference = 'EFT-123';
    await fixture.componentInstance.submitRequest();
    const service = TestBed.inject(CoachingManagementService);
    expect(service.createMyCoachingBankTransferRequest).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('18 yaş');
  });
  it('shows individual Coaching plans and excludes institution plans', async () => {
    const fixture = setup();
    fixture.detectChanges(); await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci planı');
    expect(fixture.nativeElement.textContent).not.toContain('Kurum planı');
  });

  it('submits a selected plan with trimmed transfer details and updates request history', async () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    fixture.detectChanges(); await fixture.whenStable();
    component.selectedPlanId.set('individual-plan');
    component.paymentReference = '  EFT-2026-01  ';
    component.payerName = '  Ada Öğrenci  ';
    component.note = '  Mart dönemi  ';
    component.adultPayerDeclaration = true;

    await component.submitRequest();

    const service = TestBed.inject(CoachingManagementService) as unknown as {
      createMyCoachingBankTransferRequest: ReturnType<typeof vi.fn>;
    };
    expect(service.createMyCoachingBankTransferRequest).toHaveBeenCalledWith({
      planId: 'individual-plan', paymentReference: 'EFT-2026-01', payerName: 'Ada Öğrenci', note: 'Mart dönemi', adultPayerDeclaration: true
    });
    expect(component.requests()[0].id).toBe('request-1');
    expect(component.success()).toContain('incelemeye alındı');
    expect(component.adultPayerDeclaration).toBe(false);
  });

  it('explains why the student was sent here when a learning page needed a subscription', async () => {
    const fixture = setup({ required: '1' });
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[data-testid="subscription-required"]')?.textContent).toContain('etkin bir Koçluk aboneliği');
  });

  it('shows no subscription-required notice on a normal visit', async () => {
    const fixture = setup();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[data-testid="subscription-required"]')).toBeNull();
  });

  function setup(query: Record<string, string> = {}) {
    const plan = (id: string, name: string, audience: 'Individual' | 'Institution'): CoachingSubscriptionPlan => ({
      id, slug: id, name, description: '', audience, price: 100, isContactOnly: false, billingPeriod: 'Annual',
      durationDays: 365, includedStudentSeats: audience === 'Institution' ? 20 : null, features: [], isActive: true, isPublic: true, sortOrder: 0
    });
    const service = {
      getPublicSubscriptionPlans: () => of([plan('individual-plan', 'Öğrenci planı', 'Individual'), plan('institution-plan', 'Kurum planı', 'Institution')]),
      getPublicBankTransferSettings: () => of({ currency: 'TRY', accountHolder: 'Edu İvme', bankName: 'Banka', iban: 'TR…', paymentInstructions: null }),
      getMyCoachingSubscriptionAccess: () => of({ hasAccess: true, enforcementEnabled: false, planName: null, status: null, accessUntil: null, subscriptions: [] }),
      getMyCoachingBankTransferRequests: () => of([]),
      createMyCoachingBankTransferRequest: vi.fn(() => of({
        id: 'request-1', status: 'Pending', paymentReference: 'EFT-2026-01', createdAt: '2026-03-01T00:00:00Z',
        plan: plan('individual-plan', 'Öğrenci planı', 'Individual'), reviewNote: null
      }))
    };
    TestBed.configureTestingModule({ imports: [CoachingSubscriptionComponent], providers: [
      { provide: CoachingManagementService, useValue: service },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } }
    ] });
    return TestBed.createComponent(CoachingSubscriptionComponent);
  }
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
