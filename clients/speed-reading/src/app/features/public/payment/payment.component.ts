import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  BankTransferPaymentRequest,
  BankTransferPaymentSettings,
  SalesDocuments,
  SubscriptionPlan,
  SubscriptionService
} from '../../../core/services/subscription.service';
import { AuthService } from '../../../core/services/auth.service';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    NavbarComponent,
    FooterComponent
  ],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss'
})
export class PaymentComponent implements OnInit {
  private readonly subscriptions = inject(SubscriptionService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly settings = signal<BankTransferPaymentSettings | null>(null);
  readonly requests = signal<BankTransferPaymentRequest[]>([]);
  readonly selectedPlanId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  /** Legal check Y1: the pre-information form and distance-sales contract of the selected plan. */
  readonly documents = signal<SalesDocuments | null>(null);
  readonly documentsError = signal<string | null>(null);
  /** The documents accepted with an earlier request, opened from the history. */
  readonly acceptedDocuments = signal<{ requestId: string; documents: SalesDocuments } | null>(null);
  /** The request whose withdrawal form is open. */
  readonly withdrawingId = signal<string | null>(null);
  readonly withdrawalBusy = signal(false);
  readonly withdrawalError = signal<string | null>(null);
  plans: SubscriptionPlan[] = [];
  paymentReference = '';
  payerName = '';
  note = '';
  documentsAccepted = false;
  withdrawalReason = '';

  ngOnInit(): void {
    const requestedPlanId = this.route.snapshot.queryParamMap.get('plan');
    if (requestedPlanId) this.selectedPlanId.set(requestedPlanId);

    if (!this.auth.isAuthenticated) {
      this.continueToLogin();
      return;
    }

    this.subscriptions.getPublicPlans().subscribe({
      next: plans => {
        this.plans = plans.filter(plan => !plan.isContactOnly);
        if (!this.selectedPlan) this.selectedPlanId.set(null);
        this.loadDocuments();
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
    this.subscriptions.getPublicBankTransferSettings().subscribe({
      next: settings => this.settings.set(settings),
      error: () => this.settings.set(null)
    });
    if (this.auth.isAuthenticated) {
      this.subscriptions.getMyBankTransferPaymentRequests().subscribe({
        next: requests => this.requests.set(requests),
        error: () => this.requests.set([])
      });
    }
  }

  get isAuthenticated(): boolean {
    return this.auth.isAuthenticated;
  }

  get selectedPlan(): SubscriptionPlan | null {
    return this.plans.find(plan => plan.id === this.selectedPlanId()) ?? null;
  }

  formatPrice(plan: SubscriptionPlan): string {
    return `${plan.price.toLocaleString('tr-TR')} TL`;
  }

  selectPlan(plan: SubscriptionPlan): void {
    this.selectedPlanId.set(plan.id);
    this.error.set(null);
    this.success.set(null);
    this.loadDocuments();
  }

  /** The documents name the plan and the price, so they are read again for every plan; an acceptance never carries over. */
  private loadDocuments(): void {
    this.documents.set(null);
    this.documentsError.set(null);
    this.documentsAccepted = false;
    const plan = this.selectedPlan;
    if (!plan || !this.auth.isAuthenticated) return;
    this.subscriptions.getSalesDocuments(plan.id).subscribe({
      next: documents => { if (this.selectedPlanId() === plan.id) this.documents.set(documents); },
      error: error => {
        if (this.selectedPlanId() === plan.id)
          this.documentsError.set(error?.error?.message ?? 'Satış belgeleri yüklenemedi. Lütfen daha sonra tekrar deneyin.');
      }
    });
  }

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
        this.requests.update(items => items.map(item => item.id === updated.id ? updated : item));
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

  continueToLogin(): void {
    const planId = this.selectedPlanId();
    const returnUrl = planId ? `/odeme?plan=${encodeURIComponent(planId)}` : '/odeme';
    void this.router.navigate(['/auth/login'], { queryParams: { returnUrl } });
  }

  submitBankTransferRequest(): void {
    if (!this.auth.isAuthenticated) {
      this.continueToLogin();
      return;
    }
    const plan = this.selectedPlan;
    if (!plan || !this.settings()) {
      this.error.set('Ödeme talebi için satışa açık bir plan ve banka bilgisi gereklidir.');
      return;
    }
    if (!this.paymentReference.trim()) {
      this.error.set('Banka işlem referansını girin.');
      return;
    }
    const documents = this.documents();
    if (!documents) {
      this.error.set(this.documentsError() ?? 'Satış belgeleri yüklenmeden ödeme talebi gönderilemez.');
      return;
    }
    if (!this.documentsAccepted) {
      this.error.set('Devam etmek için ön bilgilendirme formunu ve mesafeli satış sözleşmesini okuyup onaylayın.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.subscriptions.createBankTransferPaymentRequest({
      planId: plan.id,
      paymentReference: this.paymentReference.trim(),
      payerName: this.payerName.trim() || null,
      note: this.note.trim() || null,
      salesDocumentsVersion: documents.version
    }).subscribe({
      next: request => {
        this.requests.update(items => [request, ...items.filter(item => item.id !== request.id)]);
        this.success.set('Ödeme talebiniz alındı. Banka hareketi doğrulandıktan sonra erişiminiz açılacaktır.');
        this.paymentReference = '';
        this.payerName = '';
        this.note = '';
        this.documentsAccepted = false;
        this.submitting.set(false);
      },
      error: error => {
        this.error.set(error?.error?.message ?? 'Ödeme talebi oluşturulamadı. Bilgileri kontrol edip yeniden deneyin.');
        this.submitting.set(false);
      }
    });
  }
}
