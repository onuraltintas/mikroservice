import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BankTransferPaymentRequest, SubscriptionService, UserSubscription } from '../../../core/services/subscription.service';
import { PaymentRequestsComponent } from '../../public/payment/payment-requests.component';

/**
 * Billing parity (14.11.2026): the subscriber's own page inside the app. It shows whether the subscription is active and
 * until when, and every payment request with its answer, accepted sales documents, invoice and withdrawal (cayma) request.
 * Buying or renewing stays on the public payment page.
 */
@Component({
  selector: 'app-subscription-page',
  standalone: true,
  imports: [CommonModule, RouterModule, PaymentRequestsComponent],
  templateUrl: './subscription-page.component.html',
  styleUrl: './subscription-page.component.scss'
})
export class SubscriptionPageComponent implements OnInit {
  private readonly service = inject(SubscriptionService);

  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly subscriptions = signal<UserSubscription[]>([]);
  readonly requests = signal<BankTransferPaymentRequest[]>([]);

  /** Active subscriptions, the one that runs longest first; an open-ended one comes before any dated one. */
  readonly active = computed(() => this.subscriptions()
    .filter(item => item.isActive)
    .sort((a, b) => this.endTime(b) - this.endTime(a)));

  /** The last subscription that ended, shown when nothing is active. */
  readonly lastEnded = computed(() => this.active().length ? null : this.subscriptions()
    .filter(item => !!item.endDate)
    .sort((a, b) => this.endTime(b) - this.endTime(a))[0] ?? null);

  /** The running subscription ends within a week: renewing now continues from its end date. */
  readonly endsSoon = computed(() => {
    const current = this.active()[0];
    return !!current?.endDate && new Date(current.endDate).getTime() - Date.now() <= 7 * 86_400_000;
  });

  ngOnInit(): void {
    forkJoin({
      subscriptions: this.service.getMySubscriptions().pipe(catchError(() => { this.failed.set(true); return of([] as UserSubscription[]); })),
      requests: this.service.getMyBankTransferPaymentRequests().pipe(catchError(() => { this.failed.set(true); return of([] as BankTransferPaymentRequest[]); }))
    }).subscribe(({ subscriptions, requests }) => {
      this.subscriptions.set(subscriptions);
      this.requests.set(requests);
      this.loading.set(false);
    });
  }

  replaceRequest(updated: BankTransferPaymentRequest): void {
    this.requests.update(items => items.map(item => item.id === updated.id ? updated : item));
  }

  private endTime(subscription: UserSubscription): number {
    return subscription.endDate ? new Date(subscription.endDate).getTime() : Number.MAX_SAFE_INTEGER;
  }
}
