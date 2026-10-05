import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
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
