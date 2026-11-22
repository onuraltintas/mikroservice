import { CommonModule } from '@angular/common';
import { CurrencyNamePipe } from './currency-name.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize, forkJoin } from 'rxjs';
import {
  BillingAdminOrder,
  BillingAdminService,
  BillingOrderStatus,
  BillingProduct,
  ORDER_STATUS_LABELS,
  billingErrorMessage
} from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

type ReviewKind = 'approve' | 'reject' | 'reverse' | 'invoice' | 'removeInvoice';

/**
 * "Abonelik ve Ödeme › Siparişler ve ödemeler": the bank-transfer queue of every product. Orders the buyer says are
 * paid come first; an approval opens the subscription, a refusal and a reversal need a reason the buyer will read.
 */
@Component({
  selector: 'app-billing-orders',
  standalone: true,
  imports: [CurrencyNamePipe, CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-orders-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
        <h1 id="billing-orders-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Siparişler ve ödemeler</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Havale/EFT ile verilen siparişler. Banka hesabınızda ödeme kodunu ve tutarı gördüğünüz siparişi onaylayın; onay aboneliği kendiliğinden açar. Ödeme bildirilen siparişler en üsttedir.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      <div class="data-card">
        <div class="mb-3 flex flex-wrap items-end gap-3">
          <label class="filter">Durum
            <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" name="orderStatus" data-testid="order-status-filter">
              <option value="">Tümü</option>
              @for (option of statuses; track option) { <option [value]="option">{{ statusLabel(option) }}</option> }
            </select>
          </label>
          <label class="filter">Ürün
            <select [ngModel]="product()" (ngModelChange)="product.set($event); load()" name="orderProduct">
              <option value="">Tüm ürünler</option>
              @for (item of products(); track item.id) { <option [value]="item.code">{{ item.name }}</option> }
            </select>
          </label>
          <button type="button" class="secondary" (click)="load()" [disabled]="loading()">Yenile</button>
        </div>

        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Alıcı</th><th>Plan</th><th>Tutar</th><th>Ödeme kodu</th><th>Sipariş</th><th>Ödeme bildirimi</th><th>Durum</th><th></th></tr></thead>
            <tbody>
              @for (row of orders(); track row.order.id) {
                <tr [attr.data-testid]="'order-' + row.order.paymentCode">
                  <td><strong>{{ row.accountName }}</strong><div class="muted">{{ row.accountEmail || '—' }}</div></td>
                  <td>{{ row.order.planName }}<div class="muted">{{ productNames(row) }} · {{ row.order.durationDays }} gün</div></td>
                  <td class="amount">{{ row.order.amount | number:'1.2-2' }} {{ row.order.currency | currencyName }}
                    @if (row.order.vatAmount) { <div class="muted">Net {{ row.order.amount - row.order.vatAmount | number:'1.2-2' }} + KDV (%{{ row.order.vatRate }}) {{ row.order.vatAmount | number:'1.2-2' }}</div> }
                    @if (row.order.couponCode) { <div class="muted">Kupon {{ row.order.couponCode }}: −{{ row.order.discount | number:'1.2-2' }}</div> }</td>
                  <td class="font-mono code">{{ row.order.paymentCode }}</td>
                  <td>{{ row.order.createdAt | date:'dd.MM.yyyy HH:mm' }}@if (row.order.status === 'Pending') { <div class="muted">Son gün {{ row.order.expiresAt | date:'dd.MM.yyyy' }}</div> }</td>
                  <td>
                    @if (row.order.paidDeclaredAt) { {{ row.order.paidDeclaredAt | date:'dd.MM.yyyy HH:mm' }} } @else { <span class="muted">Bildirilmedi</span> }
                    @if (row.order.payerNote) { <div class="muted">"{{ row.order.payerNote }}"</div> }
                    @if (row.order.hasReceipt) { <div><button type="button" class="link" (click)="openReceipt(row)">Dekontu aç</button></div> }
                  </td>
                  <td><span class="badge" [class.on]="row.order.status === 'Paid'" [class.off]="row.order.status !== 'Paid' && row.order.status !== 'Pending'">{{ statusLabel(row.order.status) }}</span>
                    @if (row.order.withdrawal === 'Requested') { <div><span class="badge warn">Cayma talebi var</span></div> }
                    @if (row.order.hasInvoice) { <div><button type="button" class="link" (click)="openInvoice(row)">Faturayı aç</button></div> }
                    @if (row.order.reviewNote) { <div class="muted note">{{ row.order.reviewNote }}</div> }</td>
                  <td class="actions">
                    @if (review()?.id === row.order.id) {
                      <span class="muted">Aşağıda onaylayın</span>
                    } @else if (row.order.status === 'Pending') {
                      <button type="button" class="approve" (click)="start(row, 'approve')">Onayla</button>
                      <button type="button" class="danger" (click)="start(row, 'reject')">Reddet</button>
                    } @else if (row.order.status === 'Paid') {
                      @if (row.order.hasInvoice) {
                        <button type="button" (click)="start(row, 'removeInvoice')">Faturayı kaldır</button>
                      } @else {
                        <button type="button" (click)="start(row, 'invoice')">Fatura ekle</button>
                      }
                      <button type="button" class="danger" (click)="start(row, 'reverse')">Onayı geri al</button>
                    }
                  </td>
                </tr>
                @if (review(); as current) {
                  @if (current.id === row.order.id) {
                    <tr class="review-row"><td colspan="8">
                      <form (ngSubmit)="confirm()" class="review" data-testid="review-form">
                        @switch (current.kind) {
                          @case ('approve') {
                            <p><strong>{{ row.order.amount | number:'1.2-2' }} {{ row.order.currency | currencyName }}</strong> tutarındaki ödemeyi banka hesabınızda <strong class="font-mono">{{ row.order.paymentCode }}</strong> koduyla gördünüz mü? Onay, {{ row.accountName }} için {{ row.order.planName }} aboneliğini {{ row.order.durationDays }} gün açar.</p>
                          }
                          @case ('reject') {
                            <label>Ret gerekçesi (alıcı görür)<textarea [(ngModel)]="current.note" name="reviewNote" required minlength="5" maxlength="1000"></textarea></label>
                          }
                          @case ('invoice') {
                            <div class="invoice-grid">
                              <label>Fatura numarası<input [(ngModel)]="current.number" name="invoiceNumber" required minlength="3" maxlength="40" /></label>
                              <label>Fatura tarihi<input type="date" [(ngModel)]="current.issuedOn" name="invoiceDate" required /></label>
                              <label>Fatura (PDF, en çok 10 MB)<input type="file" accept="application/pdf,.pdf" (change)="chooseInvoice($event)" data-testid="invoice-file" /></label>
                            </div>
                          }
                          @case ('removeInvoice') {
                            <p>{{ row.accountName }} için yüklenen fatura kaldırılsın mı? Alıcı faturayı artık indiremez; yenisini ekleyebilirsiniz.</p>
                          }
                          @case ('reverse') {
                            <p>Onay geri alınırsa bu ödemenin kullanılmamış günleri abonelikten düşer; ödeme kaydı "geri alındı" olarak kalır.</p>
                            <label>Gerekçe (alıcı görür)<textarea [(ngModel)]="current.note" name="reviewNote" required minlength="5" maxlength="1000"></textarea></label>
                          }
                        }
                        <div class="form-actions">
                          <button type="button" class="secondary" (click)="review.set(null)">Vazgeç</button>
                          <button type="submit" class="primary" [disabled]="saving() || !canConfirm()">{{ confirmLabel(current.kind) }}</button>
                        </div>
                      </form>
                    </td></tr>
                  }
                }
              } @empty { <tr><td colspan="8" class="empty">{{ loading() ? 'Yükleniyor…' : (status() === 'Pending' ? 'Bekleyen sipariş yok.' : 'Sipariş yok.') }}</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .filter { display: flex; flex-direction: column; gap: .25rem; font-size: .8rem; color: var(--ui-text); }
    .amount, .code { white-space: nowrap; }
    .amount { font-weight: 600; }
    .note { max-width: 16rem; white-space: pre-line; }
    .link { color: var(--ui-brand); text-decoration: underline; font-size: .8rem; }
    .actions .approve { border-color: var(--ds-success); color: var(--ds-success); }
    .review-row td { background: color-mix(in srgb, var(--ui-brand) 6%, transparent); }
    .review { display: grid; gap: .75rem; max-width: 46rem; margin-left: auto; font-size: .875rem; color: var(--ui-text); }
    .review label { display: flex; flex-direction: column; gap: .25rem; }
    .invoice-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .75rem; }
    .badge.warn { background: var(--ds-warning-soft); color: var(--ds-warning); }
    @media (max-width: 640px) { .invoice-grid { grid-template-columns: 1fr; } }
  `]
})
export class BillingOrdersComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly statuses: BillingOrderStatus[] = ['Pending', 'Paid', 'Rejected', 'Cancelled', 'Expired', 'Withdrawn'];
  readonly orders = signal<BillingAdminOrder[]>([]);
  readonly products = signal<BillingProduct[]>([]);
  readonly status = signal<BillingOrderStatus | ''>('Pending');
  readonly product = signal('');
  readonly review = signal<{ id: string; kind: ReviewKind; note: string; number: string; issuedOn: string } | null>(null);
  private invoiceFile: File | null = null;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({ products: this.billing.getProducts(), orders: this.billing.getOrders(this.status(), this.product()) })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ products, orders }) => { this.products.set(products); this.orders.set(orders); },
        error: () => this.error.set('Siparişler yüklenemedi.')
      });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.review.set(null);
    this.billing.getOrders(this.status(), this.product()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: orders => this.orders.set(orders),
      error: () => this.error.set('Siparişler yüklenemedi.')
    });
  }

  statusLabel(status: BillingOrderStatus): string { return ORDER_STATUS_LABELS[status] ?? status; }

  productNames(row: BillingAdminOrder): string {
    return row.order.productCodes.map(code => this.products().find(product => product.code === code)?.name ?? code).join(', ');
  }

  confirmLabel(kind: ReviewKind): string {
    return { approve: 'Evet, ödemeyi onayla', reject: 'Siparişi reddet', reverse: 'Onayı geri al', invoice: 'Faturayı yükle', removeInvoice: 'Evet, faturayı kaldır' }[kind];
  }

  /** A refusal and a reversal need a reason; an invoice needs its number, date and file. */
  canConfirm(): boolean {
    const current = this.review();
    if (!current) return false;
    if (current.kind === 'reject' || current.kind === 'reverse') return current.note.trim().length >= 5;
    if (current.kind === 'invoice') return current.number.trim().length >= 3 && !!current.issuedOn && !!this.invoiceFile;
    return true;
  }

  start(row: BillingAdminOrder, kind: ReviewKind): void {
    this.error.set('');
    this.invoiceFile = null;
    const today = new Date();
    this.review.set({ id: row.order.id, kind, note: '', number: '',
      issuedOn: `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}` });
  }

  chooseInvoice(event: Event): void {
    this.invoiceFile = (event.target as HTMLInputElement).files?.[0] ?? null;
    // The button's state depends on the file, which is not a signal.
    this.review.update(current => current ? { ...current } : current);
  }

  openInvoice(row: BillingAdminOrder): void {
    this.billing.getInvoice(row.order.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => this.error.set('Fatura açılamadı.')
    });
  }

  confirm(): void {
    const current = this.review();
    if (!current) return;
    const note = current.note.trim();
    if (!this.canConfirm()) return;
    const request: Observable<unknown> = current.kind === 'approve' ? this.billing.approveOrder(current.id)
      : current.kind === 'reject' ? this.billing.rejectOrder(current.id, note)
      : current.kind === 'reverse' ? this.billing.reverseApproval(current.id, note)
      : current.kind === 'invoice' ? this.billing.attachInvoice(current.id, current.number.trim(), current.issuedOn, this.invoiceFile!)
      : this.billing.removeInvoice(current.id);
    this.saving.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.toaster.success({ approve: 'Ödeme onaylandı; abonelik açıldı.', reject: 'Sipariş reddedildi.', reverse: 'Onay geri alındı.',
          invoice: 'Fatura yüklendi.', removeInvoice: 'Fatura kaldırıldı.' }[current.kind]);
        this.load();
      },
      error: error => {
        this.error.set(billingErrorMessage(error, 'İşlem tamamlanamadı.'));
        // Somebody else may have reviewed it meanwhile: show what is true now.
        this.review.set(null);
        this.billing.getOrders(this.status(), this.product()).subscribe({ next: orders => this.orders.set(orders) });
      }
    });
  }

  /** The receipt is the buyer's document: opened in a new tab from memory, never through a public address. */
  openReceipt(row: BillingAdminOrder): void {
    this.billing.getReceipt(row.order.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => this.error.set('Dekont açılamadı.')
    });
  }
}
