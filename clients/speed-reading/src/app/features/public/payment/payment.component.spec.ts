import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { PaymentComponent } from './payment.component';

describe('PaymentComponent adult payer declaration', () => {
  it('requires explicit approval and clears it after a successful request', () => {
    const submit = jasmine.createSpy('submit').and.returnValue(of({ id: 'request-1' }));
    TestBed.configureTestingModule({
      imports: [PaymentComponent],
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: true } },
        { provide: ActivatedRoute, useValue: {} },
        { provide: Router, useValue: {} },
        { provide: SubscriptionService, useValue: { createBankTransferPaymentRequest: submit } }
      ]
    });
    const component = TestBed.runInInjectionContext(() => new PaymentComponent());
    component.plans = [{ id: 'plan-1' } as never];
    component.selectedPlanId.set('plan-1');
    component.settings.set({ bankName: 'Bank' } as never);
    component.paymentReference = ' EFT-123 ';
    component.submitBankTransferRequest();
    expect(submit).not.toHaveBeenCalled();
    expect(component.error()).toContain('18 yaş');
    Object.assign(component, { adultPayerDeclaration: true });
    component.submitBankTransferRequest();
    expect(submit).toHaveBeenCalledWith({ planId: 'plan-1', paymentReference: 'EFT-123', payerName: null, note: null, adultPayerDeclaration: true });
    expect((component as unknown as { adultPayerDeclaration: boolean }).adultPayerDeclaration).toBe(false);
  });
});

describe('PaymentComponent signed-in EFT flow', () => {
  function setup(authenticated: boolean) {
    const auth = { isAuthenticated: authenticated };
    const router = { navigate: jasmine.createSpy('navigate').and.resolveTo(true) };
    const subscriptions = {
      getPublicPlans: jasmine.createSpy('plans').and.returnValue(of([{ id: 'plan-1', isContactOnly: false }])),
      getPublicBankTransferSettings: jasmine.createSpy('settings').and.returnValue(of({ bankName: 'Bank' })),
      getMyBankTransferPaymentRequests: jasmine.createSpy('history').and.returnValue(of([])),
      createBankTransferPaymentRequest: jasmine.createSpy('submit').and.returnValue(of({ id: 'request-1' }))
    };
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: auth },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ plan: 'plan-1' }) } } },
      { provide: Router, useValue: router },
      { provide: SubscriptionService, useValue: subscriptions }
    ] });
    const component = TestBed.runInInjectionContext(() => new PaymentComponent());
    return { component, auth, router, subscriptions };
  }

  it('does not load bank details or payment history if the session is missing', () => {
    const { component, router, subscriptions } = setup(false);
    component.ngOnInit();
    expect(subscriptions.getPublicBankTransferSettings).not.toHaveBeenCalled();
    expect(subscriptions.getMyBankTransferPaymentRequests).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/odeme?plan=plan-1' } });
  });

  it('returns to login before validating an expired-session payment form', () => {
    const { component, router, subscriptions } = setup(false);
    component.selectedPlanId.set('plan-1');
    component.submitBankTransferRequest();
    expect(subscriptions.createBankTransferPaymentRequest).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/odeme?plan=plan-1' } });
  });

  it('loads bank details and the users own payment history after login', () => {
    const { component, subscriptions } = setup(true);
    component.ngOnInit();
    expect(component.selectedPlanId()).toBe('plan-1');
    expect(subscriptions.getPublicBankTransferSettings).toHaveBeenCalledTimes(1);
    expect(subscriptions.getMyBankTransferPaymentRequests).toHaveBeenCalledTimes(1);
  });
});
