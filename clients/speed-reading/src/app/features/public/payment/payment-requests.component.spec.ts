import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { SubscriptionService } from '../../../core/services/subscription.service';
import { PaymentRequestsComponent } from './payment-requests.component';

// Legal check Y1 and billing parity (14.11.2026): what belongs to a payment request — the accepted sales documents,
// the invoice and the withdrawal request — on the payment page and on the subscription page.
describe('PaymentRequestsComponent', () => {
  beforeAll(() => registerLocaleData(localeTr));
  function setup() {
    const subscriptions = {
      getAcceptedSalesDocuments: jasmine.createSpy('accepted').and.returnValue(of({ version: '2026-11-14', preInformation: 'onaylanan form', distanceSales: 'onaylanan sözleşme' })),
      requestBankTransferWithdrawal: jasmine.createSpy('withdraw').and.returnValue(of({ id: 'request-1', status: 'Approved', withdrawalStatus: 'Requested' })),
      downloadInvoice: jasmine.createSpy('invoice').and.returnValue(of(new Blob(['%PDF-'], { type: 'application/pdf' })))
    };
    TestBed.configureTestingModule({ imports: [PaymentRequestsComponent], providers: [{ provide: SubscriptionService, useValue: subscriptions }] });
    const fixture = TestBed.createComponent(PaymentRequestsComponent);
    return { fixture, component: fixture.componentInstance, subscriptions };
  }
  const later = () => new Date(Date.now() + 86_400_000).toISOString();

  it('offers withdrawal only for an approved payment inside its 14 days', () => {
    const { component } = setup();
    const earlier = new Date(Date.now() - 86_400_000).toISOString();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later() } as never)).toBeTrue();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: earlier } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Pending', withdrawableUntil: null } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later(), withdrawalStatus: 'Requested' } as never)).toBeFalse();
    expect(component.canWithdraw({ status: 'Approved', withdrawableUntil: later(), withdrawalStatus: 'Rejected' } as never)).toBeTrue();
    expect(component.statusLabel({ status: 'Approved', withdrawalStatus: 'Approved' } as never)).toBe('Cayıldı');
    expect(component.statusLabel({ status: 'Approved' } as never)).toBe('Onaylandı');
  });

  it('sends a withdrawal request and hands the changed request to its owner', () => {
    const { component, subscriptions } = setup();
    const changed = jasmine.createSpy('changed');
    component.changed.subscribe(changed);
    const request = { id: 'request-1', status: 'Approved', withdrawableUntil: later() } as never;
    component.openWithdrawal(request);
    component.withdrawalReason = ' Vazgeçtim ';
    component.submitWithdrawal(request);
    expect(subscriptions.requestBankTransferWithdrawal).toHaveBeenCalledWith('request-1', 'Vazgeçtim');
    expect(changed).toHaveBeenCalledWith(jasmine.objectContaining({ id: 'request-1', withdrawalStatus: 'Requested' }));
    expect(component.withdrawingId()).toBeNull();

    subscriptions.requestBankTransferWithdrawal.and.returnValue(throwError(() => ({ error: { message: 'Cayma süresi (14 gün) doldu.' } })));
    component.openWithdrawal(request);
    component.submitWithdrawal(request);
    expect(component.withdrawalError()).toBe('Cayma süresi (14 gün) doldu.');
    expect(component.withdrawalBusy()).toBeFalse();
    expect(changed).toHaveBeenCalledTimes(1);
    component.cancelWithdrawal();
    expect(component.withdrawingId()).toBeNull();
  });

  it('opens and closes the documents accepted with a request', () => {
    const { component, subscriptions } = setup();
    const request = { id: 'request-1', salesDocumentsVersion: '2026-11-14' } as never;
    component.toggleAcceptedDocuments(request);
    expect(subscriptions.getAcceptedSalesDocuments).toHaveBeenCalledWith('request-1');
    expect(component.acceptedDocuments()?.documents.preInformation).toBe('onaylanan form');
    component.toggleAcceptedDocuments(request);
    expect(component.acceptedDocuments()).toBeNull();
  });

  it('shows the invoice link only when an invoice is recorded and downloads the PDF', () => {
    const { fixture, component, subscriptions } = setup();
    const page: HTMLElement = fixture.nativeElement;
    component.requests = [
      { id: 'request-1', planName: 'Yıllık plan', paymentReference: 'EFT-1', status: 'Approved', createdAt: '2026-11-14T10:00:00Z', invoiceNumber: 'ONL2026000001', salesDocumentsVersion: '2026-11-14', withdrawableUntil: later() },
      { id: 'request-2', planName: 'Yıllık plan', paymentReference: 'EFT-2', status: 'Pending', createdAt: '2026-11-14T11:00:00Z' }
    ] as never;
    fixture.detectChanges();
    const links = page.querySelectorAll('[data-testid="download-invoice"]');
    expect(links.length).toBe(1);
    expect(links[0].textContent).toContain('ONL2026000001');
    expect(page.querySelectorAll('[data-testid="open-withdrawal"]').length).toBe(1);
    expect(page.querySelectorAll('[data-testid="show-accepted-documents"]').length).toBe(1);

    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    component.downloadInvoice(component.requests[0]);
    expect(subscriptions.downloadInvoice).toHaveBeenCalledWith('request-1');
    expect(click).toHaveBeenCalled();
    expect(component.invoiceBusy()).toBeNull();

    subscriptions.downloadInvoice.and.returnValue(throwError(() => new Error('offline')));
    component.downloadInvoice(component.requests[0]);
    fixture.detectChanges();
    expect(component.invoiceError()).toContain('indirilemedi');
    expect(page.textContent).toContain('Fatura indirilemedi');
  });
});
