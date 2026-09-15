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
  plans: SubscriptionPlan[] = [];
  paymentReference = '';
  payerName = '';
  note = '';

  ngOnInit(): void {
    const requestedPlanId = this.route.snapshot.queryParamMap.get('plan');
    if (requestedPlanId) this.selectedPlanId.set(requestedPlanId);

    this.subscriptions.getPublicPlans().subscribe({
      next: plans => {
        this.plans = plans;
        if (!this.selectedPlan) this.selectedPlanId.set(null);
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
  }

  continueToLogin(): void {
    const planId = this.selectedPlan?.id;
    const returnUrl = planId ? `/odeme?plan=${encodeURIComponent(planId)}` : '/odeme';
    void this.router.navigate(['/auth/login'], { queryParams: { returnUrl } });
  }

  submitBankTransferRequest(): void {
    const plan = this.selectedPlan;
    if (!plan || !this.settings()) {
      this.error.set('Ödeme talebi için satışa açık bir plan ve banka bilgisi gereklidir.');
      return;
    }
    if (!this.auth.isAuthenticated) {
      this.continueToLogin();
      return;
    }
    if (!this.paymentReference.trim()) {
      this.error.set('Banka işlem referansını girin.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.subscriptions.createBankTransferPaymentRequest({
      planId: plan.id,
      paymentReference: this.paymentReference.trim(),
      payerName: this.payerName.trim() || null,
      note: this.note.trim() || null
    }).subscribe({
      next: request => {
        this.requests.update(items => [request, ...items.filter(item => item.id !== request.id)]);
        this.success.set('Ödeme talebiniz alındı. Banka hareketi doğrulandıktan sonra erişiminiz açılacaktır.');
        this.paymentReference = '';
        this.payerName = '';
        this.note = '';
        this.submitting.set(false);
      },
      error: error => {
        this.error.set(error?.error?.message ?? 'Ödeme talebi oluşturulamadı. Bilgileri kontrol edip yeniden deneyin.');
        this.submitting.set(false);
      }
    });
  }
}
