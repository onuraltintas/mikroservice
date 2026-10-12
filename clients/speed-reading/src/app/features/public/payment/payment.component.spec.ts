import { Component, Input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { PaymentComponent } from './payment.component';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({ selector: 'app-navbar', standalone: true, template: '' })
class PaymentTestNavbar { @Input() forceOpaque = false; }

@Component({ selector: 'app-footer', standalone: true, template: '' })
class PaymentTestFooter {}

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
      getPublicPlans: jasmine.createSpy('plans').and.returnValue(of([{ id: 'plan-1', price: 499, isContactOnly: false }])),
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

  it('does not render bank details or the request form after the session expires', () => {
    TestBed.overrideComponent(PaymentComponent, {
      remove: { imports: [NavbarComponent, FooterComponent] },
      add: { imports: [PaymentTestNavbar, PaymentTestFooter] }
    });
    const { auth } = setup(true);
    const fixture = TestBed.createComponent(PaymentComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.bank-details')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="paymentReference"]')).not.toBeNull();
    auth.isAuthenticated = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.bank-details')).toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="paymentReference"]')).toBeNull();
  });

  it('clears a missing or contact-only selection before payment', () => {
    const { component, subscriptions } = setup(true);
    subscriptions.getPublicPlans.and.returnValue(of([{ id: 'plan-1', isContactOnly: true }]));
    component.ngOnInit();
    expect(component.plans).toEqual([]);
    expect(component.selectedPlanId()).toBeNull();
    component.submitBankTransferRequest();
    expect(subscriptions.createBankTransferPaymentRequest).not.toHaveBeenCalled();
    expect(component.error()).toContain('satışa açık');
  });

  it('requires a payment reference even after login', () => {
    const { component, subscriptions } = setup(true);
    component.ngOnInit();
    component.paymentReference = ' ';
    component.submitBankTransferRequest();
    expect(subscriptions.createBankTransferPaymentRequest).not.toHaveBeenCalled();
    expect(component.error()).toContain('referans');
  });

  it('shows a failed request without reporting payment success', () => {
    const { component, subscriptions } = setup(true);
    component.ngOnInit();
    component.paymentReference = 'EFT-123';
    component.adultPayerDeclaration = true;
    subscriptions.createBankTransferPaymentRequest.and.returnValue(throwError(() => ({ error: { message: 'Yeniden deneyin.' } })));
    component.submitBankTransferRequest();
    expect(component.error()).toBe('Yeniden deneyin.');
    expect(component.submitting()).toBeFalse();
    expect(component.success()).toBeNull();
  });

  it('clears failed bank and history loads and finishes plan loading', () => {
    const { component, subscriptions } = setup(true);
    subscriptions.getPublicPlans.and.returnValue(throwError(() => new Error('plans')));
    subscriptions.getPublicBankTransferSettings.and.returnValue(throwError(() => new Error('bank')));
    subscriptions.getMyBankTransferPaymentRequests.and.returnValue(throwError(() => new Error('history')));
    component.ngOnInit();
    expect(component.loading()).toBeFalse();
    expect(component.settings()).toBeNull();
    expect(component.requests()).toEqual([]);
  });

  it('supports selecting a plan and login without a selected plan', () => {
    const { component, router } = setup(true);
    component.error.set('previous error');
    component.success.set('previous success');
    component.selectPlan({ id: 'plan-1' } as never);
    expect(component.selectedPlanId()).toBe('plan-1');
    expect(component.error()).toBeNull();
    expect(component.success()).toBeNull();
    expect(component.formatPrice({ price: 499 } as never)).toBe('499 TL');
    component.selectedPlanId.set(null);
    component.continueToLogin();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/odeme' } });
  });
});
