import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { BankTransferPaymentRequest, SalesDocuments, SubscriptionService } from '../../../core/services/subscription.service';

/**
 * The buyer's payment requests with what belongs to each: the answer, the accepted sales documents, the invoice and the
 * withdrawal (cayma) request. Shown on the payment page and on the subscription page inside the app.
 */
@Component({
  selector: 'app-payment-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule],
  templateUrl: './payment-requests.component.html',
  styleUrl: './payment-requests.component.scss'
})
export class PaymentRequestsComponent {
  private readonly subscriptions = inject(SubscriptionService);

  @Input() requests: BankTransferPaymentRequest[] = [];
  /** A request changed on the server (a withdrawal was asked); the owner replaces it in its list. */
  @Output() changed = new EventEmitter<BankTransferPaymentRequest>();

  /** The documents accepted with a request, opened from the list. */
  readonly acceptedDocuments = signal<{ requestId: string; documents: SalesDocuments } | null>(null);
  /** The request whose withdrawal form is open. */
  readonly withdrawingId = signal<string | null>(null);
  readonly withdrawalBusy = signal(false);
  readonly withdrawalError = signal<string | null>(null);
  /** The request whose invoice is being downloaded. */
  readonly invoiceBusy = signal<string | null>(null);
  readonly invoiceError = signal<string | null>(null);
  withdrawalReason = '';

  statusLabel(request: BankTransferPaymentRequest): string {
    if (request.withdrawalStatus === 'Approved') return 'Cayıldı';
    return request.status === 'Pending' ? 'İnceleniyor' : request.status === 'Approved' ? 'Onaylandı' : 'Reddedildi';
  }

  canWithdraw(request: BankTransferPaymentRequest): boolean {
    return request.status === 'Approved' && !!request.withdrawableUntil && new Date(request.withdrawableUntil).getTime() > Date.now()
      && request.withdrawalStatus !== 'Requested' && request.withdrawalStatus !== 'Approved';
  }

  openWithdrawal(request: BankTransferPaymentRequest): void {
    this.withdrawingId.set(request.id);
    this.withdrawalReason = '';
    this.withdrawalError.set(null);
  }

  cancelWithdrawal(): void {
    this.withdrawingId.set(null);
    this.withdrawalError.set(null);
  }

  submitWithdrawal(request: BankTransferPaymentRequest): void {
    if (this.withdrawalBusy()) return;
    this.withdrawalBusy.set(true);
    this.withdrawalError.set(null);
    this.subscriptions.requestBankTransferWithdrawal(request.id, this.withdrawalReason.trim() || null).subscribe({
      next: updated => {
        this.changed.emit(updated);
        this.withdrawingId.set(null);
        this.withdrawalBusy.set(false);
      },
      error: error => {
        this.withdrawalError.set(error?.error?.message ?? 'Cayma talebi gönderilemedi. Lütfen yeniden deneyin.');
        this.withdrawalBusy.set(false);
      }
    });
  }

  toggleAcceptedDocuments(request: BankTransferPaymentRequest): void {
    if (this.acceptedDocuments()?.requestId === request.id) { this.acceptedDocuments.set(null); return; }
    this.subscriptions.getAcceptedSalesDocuments(request.id).subscribe({
      next: documents => this.acceptedDocuments.set({ requestId: request.id, documents }),
      error: () => this.acceptedDocuments.set(null)
    });
  }

  downloadInvoice(request: BankTransferPaymentRequest): void {
    if (this.invoiceBusy()) return;
    this.invoiceBusy.set(request.id);
    this.invoiceError.set(null);
    this.subscriptions.downloadInvoice(request.id).subscribe({
      next: file => {
        const url = URL.createObjectURL(file);
        const link = document.createElement('a');
        link.href = url;
        link.download = `fatura-${request.invoiceNumber ?? request.paymentReference}.pdf`.replace(/[\\/]/g, '-');
        link.click();
        URL.revokeObjectURL(url);
        this.invoiceBusy.set(null);
      },
      error: () => {
        this.invoiceError.set('Fatura indirilemedi. Lütfen yeniden deneyin.');
        this.invoiceBusy.set(null);
      }
    });
  }
}
