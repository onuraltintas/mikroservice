import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { CoachingSubscriptionComponent } from './coaching-subscription.component';

describe('CoachingSubscriptionComponent', () => {
  it('does not submit a payment without an explicit adult payer declaration', () => {
    const fixture = setup();
    fixture.detectChanges();
    fixture.componentInstance.selectedPlanId.set('individual-plan');
    fixture.componentInstance.paymentReference = 'EFT-123';
    fixture.componentInstance.submitRequest();
    const service = TestBed.inject(CoachingManagementService);
    expect(service.createMyCoachingBankTransferRequest).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('18 yaş');
  });
  it('shows individual Coaching plans and excludes institution plans', () => {
    const fixture = setup();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci planı');
    expect(fixture.nativeElement.textContent).not.toContain('Kurum planı');
  });

  it('submits a selected plan with trimmed transfer details and updates request history', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    fixture.detectChanges();
    component.selectedPlanId.set('individual-plan');
    component.paymentReference = '  EFT-2026-01  ';
    component.payerName = '  Ada Öğrenci  ';
    component.note = '  Mart dönemi  ';
    component.adultPayerDeclaration = true;

    component.submitRequest();

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

  function setup() {
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
      { provide: CoachingManagementService, useValue: service }
    ] });
    return TestBed.createComponent(CoachingSubscriptionComponent);
  }
});
