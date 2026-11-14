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
    // 13.11.2026: no adult payer declaration is asked or sent any more.
    // Legal check Y1 (14.11.2026): nothing is sent before the sales documents are loaded and accepted.
    component.submitBankTransferRequest();
    expect(submit).not.toHaveBeenCalled();
    expect(component.error()).toContain('Satış belgeleri');
    component.documents.set({ version: '2026-11-14', preInformation: 'form', distanceSales: 'sözleşme' });
    component.submitBankTransferRequest();
    expect(submit).not.toHaveBeenCalled();
    expect(component.error()).toContain('okuyup onaylayın');
    component.documentsAccepted = true;
    component.submitBankTransferRequest();
    expect(submit).toHaveBeenCalledWith({ planId: 'plan-1', paymentReference: 'EFT-123', payerName: null, note: null, salesDocumentsVersion: '2026-11-14' });
    expect(component.error()).toBeNull();
    expect(component.documentsAccepted).toBeFalse();
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
      createBankTransferPaymentRequest: jasmine.createSpy('submit').and.returnValue(of({ id: 'request-1' })),
      getSalesDocuments: jasmine.createSpy('documents').and.returnValue(of({ version: '2026-11-14', preInformation: 'ÖN BİLGİLENDİRME FORMU', distanceSales: 'MESAFELİ SATIŞ SÖZLEŞMESİ' })),
      getAcceptedSalesDocuments: jasmine.createSpy('accepted').and.returnValue(of({ version: '2026-11-14', preInformation: 'onaylanan form', distanceSales: 'onaylanan sözleşme' })),
      requestBankTransferWithdrawal: jasmine.createSpy('withdraw').and.returnValue(of({ id: 'request-1', status: 'Approved', withdrawalStatus: 'Requested' }))
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
    component.documentsAccepted = true;
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

  // Legal check Y1 (14.11.2026): the sales documents, their acceptance and the withdrawal request.
  it('loads the sales documents of the selected plan and drops the acceptance when the plan changes', () => {
    const { component, subscriptions } = setup(true);
    subscriptions.getPublicPlans.and.returnValue(of([{ id: 'plan-1', price: 499, isContactOnly: false }, { id: 'plan-2', price: 899, isContactOnly: false }]));
    component.ngOnInit();
    expect(subscriptions.getSalesDocuments).toHaveBeenCalledWith('plan-1');
    expect(component.documents()?.version).toBe('2026-11-14');
    component.documentsAccepted = true;
    component.selectPlan({ id: 'plan-2' } as never);
    expect(subscriptions.getSalesDocuments).toHaveBeenCalledWith('plan-2');
    expect(component.documentsAccepted).toBeFalse();
  });

  it('shows the documents with an acceptance box and keeps the button off while they are missing', () => {
    TestBed.overrideComponent(PaymentComponent, {
      remove: { imports: [NavbarComponent, FooterComponent] },
      add: { imports: [PaymentTestNavbar, PaymentTestFooter] }
    });
    const { subscriptions } = setup(true);
    const fixture = TestBed.createComponent(PaymentComponent);
    fixture.detectChanges();
    const page: HTMLElement = fixture.nativeElement;
    expect(page.querySelector('[data-testid="sales-documents"]')?.textContent).toContain('MESAFELİ SATIŞ SÖZLEŞMESİ');
    expect(page.querySelector('[data-testid="accept-documents"]')).not.toBeNull();
    expect((page.querySelector('.request-form button[type="submit"]') as HTMLButtonElement).disabled).toBeFalse();

    subscriptions.getSalesDocuments.and.returnValue(throwError(() => ({ error: { message: 'Satış belgeleri henüz hazır değil.' } })));
    fixture.componentInstance.selectPlan({ id: 'plan-1' } as never);
    fixture.detectChanges();
    expect(page.querySelector('[data-testid="documents-error"]')?.textContent).toContain('henüz hazır değil');
    expect((page.querySelector('.request-form button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
  });

  it('offers withdrawal only for an approved payment inside its 14 days', () => {
    const { component } = setup(true);
    const later = new Date(Date.now() + 86_400_000).toISOString();
    const earlier = new Date(Date.now() - 86_400_000).toISOString();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later } as never)).toBeTrue();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: earlier } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Pending', withdrawableUntil: null } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later, withdrawalStatus: 'Requested' } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later, withdrawalStatus: 'Rejected' } as never)).toBeTrue();
    expect(component.statusLabel({ status: 'Approved', withdrawalStatus: 'Approved' } as never)).toBe('Cayıldı');
    expect(component.statusLabel({ status: 'Approved' } as never)).toBe('Onaylandı');
  });

  it('sends a withdrawal request and shows the answer in the history', () => {
    const { component, subscriptions } = setup(true);
    const request = { id: 'request-1', status: 'Approved', withdrawableUntil: new Date(Date.now() + 86_400_000).toISOString() } as never;
    component.requests.set([request]);
    component.openWithdrawal(request);
    component.withdrawalReason = ' Vazgeçtim ';
    component.submitWithdrawal(request);
    expect(subscriptions.requestBankTransferWithdrawal).toHaveBeenCalledWith('request-1', 'Vazgeçtim');
    expect(component.requests()[0].withdrawalStatus).toBe('Requested');
    expect(component.withdrawingId()).toBeNull();

    subscriptions.requestBankTransferWithdrawal.and.returnValue(throwError(() => ({ error: { message: 'Cayma süresi (14 gün) doldu.' } })));
    component.openWithdrawal(request);
    component.submitWithdrawal(request);
    expect(component.withdrawalError()).toBe('Cayma süresi (14 gün) doldu.');
    expect(component.withdrawalBusy()).toBeFalse();
    component.cancelWithdrawal();
    expect(component.withdrawingId()).toBeNull();
  });

  it('opens and closes the documents accepted with a request', () => {
    const { component, subscriptions } = setup(true);
    const request = { id: 'request-1', salesDocumentsVersion: '2026-11-14' } as never;
    component.toggleAcceptedDocuments(request);
    expect(subscriptions.getAcceptedSalesDocuments).toHaveBeenCalledWith('request-1');
    expect(component.acceptedDocuments()?.documents.preInformation).toBe('onaylanan form');
    component.toggleAcceptedDocuments(request);
    expect(component.acceptedDocuments()).toBeNull();
  });
});
