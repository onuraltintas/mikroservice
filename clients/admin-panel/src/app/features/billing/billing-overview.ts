import { CommonModule } from '@angular/common';
import { currencyName } from './currency-name.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { BillingAdminService, BillingOverview, BillingProduct } from '../../core/services/billing-admin.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

/** "Abonelik ve Ödeme › Genel bakış": what waits for a decision, what ends soon and what was approved this month. */
@Component({
  selector: 'app-billing-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="space-y-6" aria-labelledby="billing-overview-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
        <h1 id="billing-overview-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Genel bakış</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Karar bekleyen işler, yakında biten abonelikler ve onaylanan ödemeler. Aylar Türkiye saatine göredir.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      <div class="flex flex-wrap items-center gap-2">
        <label class="text-sm text-(--ui-text)" for="overview-product-filter">Ürün</label>
        <select id="overview-product-filter" [ngModel]="product()" (ngModelChange)="product.set($event); load()" name="overviewProduct">
          <option value="">Tüm ürünler</option>
          @for (item of products(); track item.id) { <option [value]="item.code">{{ item.name }}</option> }
        </select>
      </div>

      @if (overview(); as data) {
        <div class="tiles">
          <a class="tile" routerLink="/dashboard/billing/orders" data-testid="tile-orders">
            <span class="tile-label">Onay bekleyen sipariş</span>
            <strong>{{ data.waitingOrders }}</strong>
            <span class="muted">{{ data.declaredOrders }} tanesi "ödemeyi yaptım" dedi</span>
          </a>
          <a class="tile" routerLink="/dashboard/billing/withdrawals" data-testid="tile-withdrawals">
            <span class="tile-label">Cayma talebi</span>
            <strong>{{ data.withdrawalRequests }}</strong>
            <span class="muted">yanıt bekliyor</span>
          </a>
          <a class="tile" routerLink="/dashboard/billing/statements" data-testid="tile-transactions">
            <span class="tile-label">Eşleşmeyen banka hareketi</span>
            <strong>{{ data.unmatchedTransactions }}</strong>
            <span class="muted">bütün ürünler</span>
          </a>
          <a class="tile" routerLink="/dashboard/billing/subscriptions" data-testid="tile-subscriptions">
            <span class="tile-label">Süren abonelik</span>
            <strong>{{ data.currentSubscriptions }}</strong>
            <span class="muted">{{ data.endingSoon.length }} tanesi 14 gün içinde bitiyor</span>
          </a>
          <div class="tile" data-testid="tile-approved">
            <span class="tile-label">Bu ay onaylanan</span>
            <strong>{{ data.approvedThisMonth | number:'1.2-2' }} {{ currency(data) }}</strong>
            <span class="muted">Geçen ay: {{ data.approvedLastMonth | number:'1.2-2' }} {{ currency(data) }}</span>
          </div>
        </div>

        <div class="data-card">
          <h2 class="mb-3 font-semibold text-(--ui-text)">14 gün içinde biten abonelikler</h2>
          <div class="overflow-x-auto">
            <table class="data-table">
              <thead><tr><th>Hesap</th><th>Plan</th><th>Bitiş</th><th>Koltuk</th></tr></thead>
              <tbody>
                @for (row of data.endingSoon; track row.subscription.id) {
                  <tr>
                    <td><strong>{{ row.accountName }}</strong><div class="muted">{{ row.accountEmail || '—' }}</div></td>
                    <td>{{ row.subscription.planName }}</td>
                    <td>{{ row.subscription.endsAt | date:'dd.MM.yyyy' }}</td>
                    <td>{{ row.subscription.seatCount ?? '—' }}</td>
                  </tr>
                } @empty { <tr><td colspan="4" class="empty">14 gün içinde biten abonelik yok.</td></tr> }
              </tbody>
            </table>
          </div>
        </div>
      } @else if (loading()) {
        <p class="muted" role="status">Yükleniyor…</p>
      }
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .tiles { display: grid; gap: .75rem; grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); }
    .tile { display: grid; gap: .25rem; border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); padding: 1rem; color: var(--ui-text); }
    a.tile:hover { border-color: var(--ui-brand); }
    a.tile:focus-visible { outline: 3px solid var(--ui-brand); outline-offset: 2px; }
    .tile strong { font-size: 1.5rem; }
    .tile-label { font-size: .8rem; color: var(--ui-text-muted); }
  `]
})
export class BillingOverviewComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);

  readonly overview = signal<BillingOverview | null>(null);
  readonly products = signal<BillingProduct[]>([]);
  readonly product = signal('');
  readonly loading = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.billing.getProducts().subscribe({ next: products => this.products.set(products), error: () => this.products.set([]) });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.billing.getOverview(this.product()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: overview => this.overview.set(overview),
      error: () => { this.overview.set(null); this.error.set('Genel bakış yüklenemedi.'); }
    });
  }

  currency(data: BillingOverview): string { return currencyName(data.currency); }
}
