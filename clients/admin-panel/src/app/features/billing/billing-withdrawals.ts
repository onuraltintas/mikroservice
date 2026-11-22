import { CommonModule } from '@angular/common';
import { CurrencyNamePipe } from './currency-name.pipe';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { BillingAdminService, BillingWithdrawal, WITHDRAWAL_STATUS_LABELS, WithdrawalStatus, billingErrorMessage } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

/**
 * "Abonelik ve Ödeme › Cayma talepleri": a buyer may withdraw within 14 days of the approval. Accepting ends the access
 * and marks the payment as reversed; the money goes back by a bank transfer the admin makes.
 */
@Component({
  selector: 'app-billing-withdrawals',
  standalone: true,
  imports: [CurrencyNamePipe, CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-withdrawals-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
        <h1 id="billing-withdrawals-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Cayma talepleri</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Alıcı, ödemesinin onayından sonra 14 gün içinde cayabilir. Kabul ederseniz abonelik sona erer ve ödeme kaydı "geri alındı" olur; tutarı alıcının ödeme yaptığı hesaba bankadan siz iade edersiniz. Ret, alıcının göreceği bir gerekçe ister.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      <div class="data-card">
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <label class="text-sm text-(--ui-text)" for="withdrawal-status">Durum</label>
          <select id="withdrawal-status" [ngModel]="status()" (ngModelChange)="status.set($event); load()" name="withdrawalStatus">
            <option value="">Tümü</option>
            @for (option of statuses; track option) { <option [value]="option">{{ label(option) }}</option> }
          </select>
        </div>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Alıcı</th><th>Plan</th><th>Tutar</th><th>Talep</th><th>Durum</th><th></th></tr></thead>
            <tbody>
              @for (row of withdrawals(); track row.id) {
                <tr [attr.data-testid]="'withdrawal-' + row.paymentCode">
                  <td><strong>{{ row.accountName }}</strong><div class="muted">{{ row.accountEmail || '—' }}</div></td>
                  <td>{{ row.planName }}<div class="muted font-mono">{{ row.paymentCode }}</div></td>
                  <td class="nowrap"><strong>{{ row.amount | number:'1.2-2' }} {{ row.currency | currencyName }}</strong></td>
                  <td>{{ row.requestedAt | date:'dd.MM.yyyy HH:mm' }}@if (row.reason) { <div class="muted">"{{ row.reason }}"</div> }</td>
                  <td><span class="badge" [class.on]="row.status === 'Approved'" [class.off]="row.status === 'Rejected'">{{ label(row.status) }}</span>
                    @if (row.reviewNote) { <div class="muted note">{{ row.reviewNote }}</div> }</td>
                  <td class="actions">@if (row.status === 'Requested' && review()?.id !== row.id) {
                    <button type="button" (click)="start(row, true)">Kabul et</button>
                    <button type="button" class="danger" (click)="start(row, false)">Reddet</button>
                  }</td>
                </tr>
                @if (review(); as current) {
                  @if (current.id === row.id) {
                    <tr class="review-row"><td colspan="6">
                      <form (ngSubmit)="confirm()" class="review" data-testid="withdrawal-review">
                        @if (current.approve) {
                          <p>Kabul edilince {{ row.accountName }} için {{ row.planName }} erişimi sona erer. <strong>{{ row.amount | number:'1.2-2' }} {{ row.currency | currencyName }}</strong> tutarını alıcının hesabına bankadan siz iade edeceksiniz.</p>
                          <label>Alıcıya not <span class="muted">(isteğe bağlı)</span><textarea [(ngModel)]="current.note" name="withdrawalNote" maxlength="1000"></textarea></label>
                        } @else {
                          <label>Ret gerekçesi (alıcı görür)<textarea [(ngModel)]="current.note" name="withdrawalNote" required minlength="5" maxlength="1000"></textarea></label>
                        }
                        <div class="form-actions">
                          <button type="button" class="secondary" (click)="review.set(null)">Vazgeç</button>
                          <button type="submit" class="primary" [disabled]="saving() || (!current.approve && current.note.trim().length < 5)">{{ current.approve ? 'Caymayı kabul et' : 'Talebi reddet' }}</button>
                        </div>
                      </form>
                    </td></tr>
                  }
                }
              } @empty { <tr><td colspan="6" class="empty">{{ loading() ? 'Yükleniyor…' : (status() === 'Requested' ? 'Bekleyen cayma talebi yok.' : 'Cayma talebi yok.') }}</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .nowrap { white-space: nowrap; }
    .note { max-width: 18rem; white-space: pre-line; }
    .review-row td { background: color-mix(in srgb, var(--ui-brand) 6%, transparent); }
    .review { display: grid; gap: .75rem; max-width: 46rem; margin-left: auto; font-size: .875rem; color: var(--ui-text); }
    .review label { display: flex; flex-direction: column; gap: .25rem; }
  `]
})
export class BillingWithdrawalsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly statuses: WithdrawalStatus[] = ['Requested', 'Approved', 'Rejected'];
  readonly withdrawals = signal<BillingWithdrawal[]>([]);
  readonly status = signal<WithdrawalStatus | ''>('Requested');
  readonly review = signal<{ id: string; approve: boolean; note: string } | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  label(status: WithdrawalStatus): string { return WITHDRAWAL_STATUS_LABELS[status] ?? status; }

  load(): void {
    this.loading.set(true);
    this.review.set(null);
    this.billing.getWithdrawals(this.status()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: withdrawals => this.withdrawals.set(withdrawals),
      error: () => this.error.set('Cayma talepleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  start(row: BillingWithdrawal, approve: boolean): void {
    this.error.set('');
    this.review.set({ id: row.id, approve, note: '' });
  }

  confirm(): void {
    const current = this.review();
    if (!current) return;
    this.saving.set(true);
    this.error.set('');
    this.billing.reviewWithdrawal(current.id, current.approve, current.note.trim()).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.toaster.success(current.approve ? 'Cayma kabul edildi; abonelik sona erdi.' : 'Cayma talebi reddedildi.'); this.load(); },
      error: error => { this.error.set(billingErrorMessage(error, 'İşlem tamamlanamadı.')); this.load(); }
    });
  }
}
