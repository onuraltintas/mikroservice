import { CommonModule } from '@angular/common';
import { currencyName } from './currency-name.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { BillingAdminService, BillingOverview, BillingProduct } from '../../core/services/billing-admin.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';
import { AdminBarsComponent, AdminPoint } from '../../shared/components/admin-charts';

/** "Abonelik ve Ödeme › Genel bakış": what waits for a decision, what ends soon and what was approved this month. */
@Component({
  selector: 'app-billing-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AdminBarsComponent],
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
          <admin-bars [title]="'Onaylanan ödemeler (' + currency(data) + ')'" [points]="approvedBars(data)" tone="honey" />
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
    /* Lively design (21.11.2026): each count wears the soft tone of what it means; links lift on hover. */
    .tile { display: grid; gap: .25rem; border: 0; border-radius: 16px; background: var(--tone-soft, var(--ui-surface)); padding: 1rem 1.1rem; color: var(--ui-text);
      text-decoration: none; transition: transform .2s ease, box-shadow .2s ease; animation: tile-rise .5s cubic-bezier(.2, .8, .2, 1) both; }
    .tile:nth-child(2) { animation-delay: 60ms; } .tile:nth-child(3) { animation-delay: 120ms; } .tile:nth-child(4) { animation-delay: 180ms; } .tile:nth-child(5) { animation-delay: 240ms; }
    [data-testid='tile-orders'] { --tone: var(--ds-warning); --tone-soft: var(--ds-warning-soft); }
    [data-testid='tile-withdrawals'] { --tone: var(--ds-violet); --tone-soft: var(--ds-violet-soft); }
    [data-testid='tile-transactions'] { --tone: var(--ds-danger); --tone-soft: var(--ds-danger-soft); }
    [data-testid='tile-subscriptions'] { --tone: var(--ds-success); --tone-soft: var(--ds-success-soft); }
    [data-testid='tile-approved'] { --tone: var(--ds-info); --tone-soft: var(--ds-info-soft); }
    a.tile:hover { transform: translateY(-3px); box-shadow: var(--ds-shadow-hover, var(--ds-shadow)); }
    a.tile:focus-visible { outline: 3px solid var(--ds-accent); outline-offset: 2px; }
    .tile strong { color: var(--tone, var(--ui-text)); font-size: 1.6rem; line-height: 1.15; font-variant-numeric: tabular-nums; }
    .tile-label { font-size: .85rem; font-weight: 600; color: var(--ui-text); }
    @keyframes tile-rise { from { opacity: 0; transform: translateY(10px); } }
    @media (prefers-reduced-motion: reduce) { .tile { animation: none; transition: none; } a.tile:hover { transform: none; } }
  `]
})
export class BillingOverviewComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);

  approvedBars(data: BillingOverview): AdminPoint[] {
    return [{ label: 'Geçen ay', value: data.approvedLastMonth }, { label: 'Bu ay', value: data.approvedThisMonth }];
  }

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
