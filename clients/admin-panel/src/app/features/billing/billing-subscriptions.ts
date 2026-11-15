import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { BillingAdminService, BillingAdminSubscription, BillingProduct } from '../../core/services/billing-admin.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

/** "Abonelik ve Ödeme › Abonelikler": who has access through which plan until when, in every product. */
@Component({
  selector: 'app-billing-subscriptions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-subscriptions-title">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Abonelik ve Ödeme</p>
        <h1 id="billing-subscriptions-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Abonelikler</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Onaylanan ödemelerin açtığı abonelikler. Bitiş tarihi, onaylı dönemlerin toplamıdır; yenileme mevcut dönemin bittiği gün başlar.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div> }

      <div class="data-card">
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <label class="text-sm text-gray-700 dark:text-gray-200" for="subscription-product-filter">Ürün</label>
          <select id="subscription-product-filter" [ngModel]="product()" (ngModelChange)="product.set($event); load()" name="subscriptionProduct">
            <option value="">Tüm ürünler</option>
            @for (item of products(); track item.id) { <option [value]="item.code">{{ item.name }}</option> }
          </select>
        </div>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Hesap</th><th>Plan</th><th>Ürünler</th><th>Başlangıç</th><th>Bitiş</th><th>Koltuk</th><th>Durum</th></tr></thead>
            <tbody>
              @for (row of subscriptions(); track row.subscription.id) {
                <tr>
                  <td><strong>{{ row.accountName }}</strong><div class="muted">{{ row.accountEmail || '—' }}</div></td>
                  <td>{{ row.subscription.planName }}</td>
                  <td>{{ productNames(row) }}</td>
                  <td>{{ row.subscription.startsAt | date:'dd.MM.yyyy' }}</td>
                  <td>{{ row.subscription.endsAt | date:'dd.MM.yyyy' }}</td>
                  <td>{{ row.subscription.seatCount ?? '—' }}</td>
                  <td><span class="badge" [class.on]="row.subscription.isCurrent" [class.off]="!row.subscription.isCurrent">{{ stateLabel(row) }}</span></td>
                </tr>
              } @empty { <tr><td colspan="7" class="empty">{{ loading() ? 'Yükleniyor…' : 'Abonelik yok.' }}</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES]
})
export class BillingSubscriptionsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);

  readonly subscriptions = signal<BillingAdminSubscription[]>([]);
  readonly products = signal<BillingProduct[]>([]);
  readonly product = signal('');
  readonly loading = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({ products: this.billing.getProducts(), subscriptions: this.billing.getSubscriptions('') })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ products, subscriptions }) => { this.products.set(products); this.subscriptions.set(subscriptions); },
        error: () => this.error.set('Abonelikler yüklenemedi.')
      });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.billing.getSubscriptions(this.product()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: subscriptions => this.subscriptions.set(subscriptions),
      error: () => this.error.set('Abonelikler yüklenemedi.')
    });
  }

  productNames(row: BillingAdminSubscription): string {
    return row.subscription.productCodes.map(code => this.products().find(product => product.code === code)?.name ?? code).join(', ');
  }

  stateLabel(row: BillingAdminSubscription): string {
    if (row.subscription.isCurrent) return 'Sürüyor';
    return row.subscription.status === 'Cancelled' ? 'Sonlandırıldı' : new Date(row.subscription.startsAt) > new Date() ? 'Başlamadı' : 'Bitti';
  }
}
