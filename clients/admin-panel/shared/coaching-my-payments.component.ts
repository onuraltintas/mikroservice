import { DatePipe } from '@angular/common';
import { Component, Input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { saveCoachingMessageFile } from './coaching-messages.api';
import { CoachingBuyerPayment, CoachingSalesApi, coachingMoney, CoachingSalesDocuments, coachingErrorMessage, coachingWithdrawalLabel } from './coaching-sales.api';

/**
 * Roadmap O10: the buyer's approved payments with the invoice the admin recorded, the documents accepted at checkout,
 * and the 14-day withdrawal (cayma) request.
 */
@Component({
  selector: 'edu-coaching-my-payments', standalone: true, imports: [DatePipe, FormsModule],
  styles: [`
    :host{display:block}
    h2,h3{margin:0 0 .5rem;font-size:1rem;font-weight:600}
    ul{list-style:none;margin:0;padding:0;display:grid}
    li{border-top:1px solid var(--ui-border,#e2e8f0);padding:.7rem 0;display:grid;gap:.4rem;font-size:.88rem}
    .head{display:flex;flex-wrap:wrap;justify-content:space-between;gap:.5rem}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem;margin:0}
    .err{color:var(--ui-danger,#b91c1c);font-size:.85rem;margin:0}
    .ok{color:var(--ui-success,#047857);font-size:.85rem;margin:0}
    .actions{display:flex;flex-wrap:wrap;gap:.4rem}
    button{min-height:2.2rem;border:1px solid var(--ui-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.25rem .7rem;font:inherit;font-size:.82rem;font-weight:600;cursor:pointer}
    button:disabled{opacity:.55;cursor:not-allowed}
    textarea{width:100%;border:1px solid var(--ui-border,#cbd5e1);border-radius:.45rem;padding:.5rem;font:inherit;background:transparent;color:inherit}
    pre{white-space:pre-wrap;overflow-wrap:anywhere;max-height:16rem;overflow-y:auto;font:inherit;font-size:.78rem;line-height:1.5;margin:.3rem 0 0;padding:.5rem;border-radius:.4rem;background:var(--ui-surface-muted,#f8fafc)}
    :host-context(.dark) pre{background:#0f172a}
    .tag{border-radius:999px;padding:.05rem .5rem;font-size:.75rem;font-weight:700;background:var(--ui-surface-muted,#f1f5f9)}
  `],
  template: `
    <h2>{{ heading }}</h2>
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
    @if (success()) { <p class="ok" role="status">{{ success() }}</p> }
    @if (loading()) { <p class="muted" role="status">Ödemeler yükleniyor…</p> }
    <ul>
      @for (payment of payments(); track payment.id) {
        <li [attr.data-testid]="'payment-' + payment.id">
          <div class="head">
            <span><strong>{{ payment.planName }}</strong> · {{ money(payment.amount) }} {{ payment.currency }}
              @if (payment.discountAmount > 0) { <span class="muted">({{ payment.couponCode }} ile {{ money(payment.discountAmount) }} {{ payment.currency }} indirim)</span> }
            </span>
            <span class="muted">{{ payment.createdAt | date:'dd.MM.yyyy' }}</span>
          </div>
          @if (withdrawalLabel(payment); as label) { <span class="tag" [attr.data-testid]="'withdrawal-status-' + payment.id">{{ label }}</span> }
          <div class="actions">
            @if (payment.invoiceNumber) {
              <button type="button" [attr.data-testid]="'invoice-' + payment.id" [disabled]="busyId() === payment.id" (click)="downloadInvoice(payment)">Fatura {{ payment.invoiceNumber }} indir</button>
            } @else { <span class="muted">Fatura henüz eklenmedi.</span> }
            @if (payment.hasSalesDocuments && payment.transferId) {
              <button type="button" [attr.data-testid]="'documents-' + payment.id" (click)="toggleDocuments(payment)">{{ openDocumentsId() === payment.id ? 'Sözleşmeleri gizle' : 'Kabul edilen sözleşmeler' }}</button>
            }
            @if (canWithdraw(payment) && withdrawingId() !== payment.id) {
              <button type="button" [attr.data-testid]="'withdraw-' + payment.id" (click)="startWithdrawal(payment)">Cayma hakkını kullan</button>
            }
          </div>
          @if (canWithdraw(payment)) { <p class="muted">Cayma hakkı {{ payment.withdrawUntil | date:'dd.MM.yyyy HH:mm' }} tarihine kadar kullanılabilir.</p> }
          @if (openDocumentsId() === payment.id && documents(); as docs) {
            <pre data-testid="accepted-pre-information">{{ docs.preInformation }}</pre>
            <pre data-testid="accepted-distance-sales">{{ docs.distanceSales }}</pre>
          }
          @if (withdrawingId() === payment.id) {
            <form (ngSubmit)="submitWithdrawal(payment)">
              <label class="muted">Gerekçe (isteğe bağlı)<textarea name="withdrawalReason" [(ngModel)]="withdrawalReason" maxlength="500" rows="2"></textarea></label>
              <p class="muted">Onaylandığında abonelik sona erer ve ödenen tutar, kullanılan günler düşülerek 14 gün içinde iade edilir.</p>
              <div class="actions"><button type="submit" data-testid="confirm-withdrawal" [disabled]="busyId() === payment.id">Cayma talebi gönder</button><button type="button" (click)="withdrawingId.set(null)">Vazgeç</button></div>
            </form>
          }
        </li>
      } @empty { @if (!loading()) { <li class="muted">Onaylanmış bir ödeme{{ formal ? 'niz' : 'n' }} yok.</li> } }
    </ul>
  `
})
export class CoachingMyPaymentsComponent implements OnInit {
  @Input({ required: true }) api!: CoachingSalesApi;
  @Input() heading = 'Ödemelerim';
  @Input() formal = false;

  readonly payments = signal<CoachingBuyerPayment[]>([]);
  readonly money = coachingMoney;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly openDocumentsId = signal<string | null>(null);
  readonly documents = signal<CoachingSalesDocuments | null>(null);
  readonly withdrawingId = signal<string | null>(null);
  withdrawalReason = '';

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    try { this.payments.set(await firstValueFrom(this.api.myPayments())); this.error.set(null); }
    catch { this.error.set('Ödemeler yüklenemedi.'); }
    finally { this.loading.set(false); }
  }

  withdrawalLabel(payment: CoachingBuyerPayment): string | null { return coachingWithdrawalLabel(payment.withdrawalStatus); }

  canWithdraw(payment: CoachingBuyerPayment): boolean {
    // The server only sets the deadline while a withdrawal is possible (approved, none pending, within 14 days).
    return !!payment.transferId && payment.withdrawalStatus !== 'Requested' && payment.withdrawalStatus !== 'Approved'
      && !!payment.withdrawUntil && new Date(payment.withdrawUntil).getTime() > Date.now();
  }

  async downloadInvoice(payment: CoachingBuyerPayment): Promise<void> {
    if (this.busyId()) return;
    this.busyId.set(payment.id); this.error.set(null);
    try { saveCoachingMessageFile(await firstValueFrom(this.api.invoice(payment.id)), `fatura-${payment.invoiceNumber ?? payment.id}.pdf`); }
    catch { this.error.set('Fatura indirilemedi.'); }
    finally { this.busyId.set(null); }
  }

  async toggleDocuments(payment: CoachingBuyerPayment): Promise<void> {
    if (this.openDocumentsId() === payment.id) { this.openDocumentsId.set(null); return; }
    if (!payment.transferId) return;
    this.error.set(null);
    try { this.documents.set(await firstValueFrom(this.api.acceptedDocuments(payment.transferId))); this.openDocumentsId.set(payment.id); }
    catch { this.error.set('Sözleşme metinleri yüklenemedi.'); }
  }

  startWithdrawal(payment: CoachingBuyerPayment): void {
    this.withdrawingId.set(payment.id); this.withdrawalReason = ''; this.error.set(null); this.success.set(null);
  }

  async submitWithdrawal(payment: CoachingBuyerPayment): Promise<void> {
    if (!payment.transferId || this.busyId()) return;
    this.busyId.set(payment.id); this.error.set(null);
    try {
      const updated = await firstValueFrom(this.api.requestWithdrawal(payment.transferId, this.withdrawalReason));
      this.payments.update(items => items.map(item => item.id === payment.id ? { ...item, ...updated } : item));
      this.withdrawingId.set(null);
      this.success.set(this.formal ? 'Cayma talebiniz alındı. Sonucu bu ekrandan takip edebilirsiniz.' : 'Cayma talebin alındı. Sonucu bu ekrandan takip edebilirsin.');
    } catch (response) {
      this.error.set(coachingErrorMessage(response, 'Cayma talebi gönderilemedi.'));
    } finally { this.busyId.set(null); }
  }
}
