import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  coachingMoney, COACHING_COUPON_PATTERN, CoachingCheckoutChoice, CoachingCouponQuote, CoachingSalesApi, CoachingSalesDocuments, coachingErrorMessage,
  normalizeCoachingCoupon
} from './coaching-sales.api';

/**
 * Roadmap O10: the part of a bank-transfer checkout that is the same for students, parents and teachers — an optional
 * discount code and the pre-information form and distance-sales contract the buyer must open and accept before paying.
 * The documents are drafts filled by the server; what the buyer accepted is stored with the notice.
 */
@Component({
  selector: 'edu-coaching-checkout', standalone: true, imports: [FormsModule],
  styles: [`
    :host{display:grid;gap:.75rem;margin-top:.75rem}
    .box{border:1px solid var(--ui-border,#e2e8f0);border-radius:.6rem;padding:.75rem;display:grid;gap:.5rem}
    .row{display:flex;flex-wrap:wrap;gap:.5rem;align-items:flex-end}
    .row label{flex:1 1 12rem;display:grid;gap:.25rem;font-size:.85rem}
    input[type=text]{width:100%;border:1px solid var(--ui-border,#cbd5e1);border-radius:.45rem;padding:.5rem .65rem;font:inherit;background:transparent;color:inherit;text-transform:uppercase}
    button{min-height:2.3rem;border:1px solid var(--ui-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.3rem .75rem;font:inherit;font-size:.85rem;font-weight:600;cursor:pointer}
    button:disabled{opacity:.55;cursor:not-allowed}
    .muted{color:var(--ui-text-muted,#64748b);font-size:.8rem;margin:0}
    .ok{color:var(--ui-success,#047857);font-size:.85rem;margin:0}
    .err{color:var(--ui-danger,#b91c1c);font-size:.85rem;margin:0}
    .total{font-weight:700}
    details{border-top:1px solid var(--ui-border,#e2e8f0);padding-top:.4rem}
    summary{cursor:pointer;font-weight:600;font-size:.88rem}
    pre{white-space:pre-wrap;overflow-wrap:anywhere;max-height:18rem;overflow-y:auto;font:inherit;font-size:.8rem;line-height:1.5;margin:.4rem 0 0;padding:.5rem;border-radius:.4rem;background:var(--ui-surface-muted,#f8fafc)}
    :host-context(.dark) pre{background:#0f172a}
    .accept{display:flex;gap:.5rem;align-items:flex-start;font-size:.85rem}
  `],
  template: `
    <div class="box">
      <div class="row">
        <label>İndirim kodu (isteğe bağlı)
          <input type="text" name="couponCode" data-testid="coupon-input" [(ngModel)]="couponInput" maxlength="30" autocomplete="off" [disabled]="!!quote()" />
        </label>
        @if (quote()) {
          <button type="button" data-testid="coupon-clear" (click)="clearCoupon()">Kodu kaldır</button>
        } @else {
          <button type="button" data-testid="coupon-apply" [disabled]="quoting() || !couponInput.trim()" (click)="applyCoupon()">{{ quoting() ? 'Kontrol ediliyor…' : 'Uygula' }}</button>
        }
      </div>
      @if (couponError()) { <p class="err" role="alert">{{ couponError() }}</p> }
      @if (quote(); as q) {
        <p class="ok" role="status" data-testid="coupon-quote">{{ q.code }} kodu uygulandı: {{ money(q.discount) }} {{ currency }} indirim.</p>
        <p class="total">Ödenecek tutar: <s class="muted">{{ money(q.listPrice) }}</s> {{ money(q.amount) }} {{ currency }}</p>
      } @else if (price > 0) {
        <p class="total">Ödenecek tutar: {{ money(price) }} {{ currency }}</p>
      }
    </div>

    <div class="box">
      @if (!documents()) {
        <p class="muted">Ödemeden önce ön bilgilendirme formunu ve mesafeli satış sözleşmesini {{ formal ? 'okumanız' : 'okuman' }} gerekir.</p>
        <button type="button" data-testid="open-sales-documents" [disabled]="loadingDocuments() || !planIdValue" (click)="openDocuments()">{{ loadingDocuments() ? 'Hazırlanıyor…' : 'Sözleşmeleri görüntüle' }}</button>
        @if (documentsError()) { <p class="err" role="alert">{{ documentsError() }}</p> }
      } @else {
        <details open><summary>Ön bilgilendirme formu</summary><pre data-testid="pre-information">{{ documents()!.preInformation }}</pre></details>
        <details><summary>Mesafeli satış sözleşmesi</summary><pre data-testid="distance-sales">{{ documents()!.distanceSales }}</pre></details>
        <label class="accept"><input type="checkbox" name="acceptSalesContract" data-testid="accept-sales-contract" [ngModel]="accepted()" (ngModelChange)="setAccepted($event)" />
          Ön bilgilendirme formunu ve mesafeli satış sözleşmesini okudum, kabul ediyorum.</label>
      }
    </div>
  `
})
export class CoachingCheckoutComponent {
  @Input({ required: true }) api!: CoachingSalesApi;
  @Input() price = 0;
  @Input() currency = 'TRY';
  /** Staff and institution pages speak with "siz", student pages with "sen". */
  @Input() formal = false;
  @Input() set planId(value: string | null | undefined) {
    if (value === this.planIdValue) return;
    this.planIdValue = value ?? null;
    this.reset();
  }
  @Output() readonly choiceChange = new EventEmitter<CoachingCheckoutChoice>();

  planIdValue: string | null = null;
  couponInput = '';
  readonly quote = signal<CoachingCouponQuote | null>(null);
  readonly quoting = signal(false);
  readonly couponError = signal<string | null>(null);
  readonly documents = signal<CoachingSalesDocuments | null>(null);
  readonly loadingDocuments = signal(false);
  readonly documentsError = signal<string | null>(null);
  readonly accepted = signal(false);
  readonly money = coachingMoney;

  async applyCoupon(): Promise<void> {
    const code = normalizeCoachingCoupon(this.couponInput);
    if (!this.planIdValue || this.quoting()) return;
    if (!COACHING_COUPON_PATTERN.test(code)) { this.couponError.set('İndirim kodu 3–30 harf, rakam ya da tireden oluşur.'); return; }
    this.quoting.set(true); this.couponError.set(null);
    try {
      const quote = await firstValueFrom(this.api.quoteCoupon(code, this.planIdValue));
      this.quote.set(quote); this.couponInput = quote.code;
      this.invalidateDocuments();
    } catch (response) {
      this.quote.set(null);
      this.couponError.set(coachingErrorMessage(response, 'İndirim kodu uygulanamadı.'));
    } finally { this.quoting.set(false); }
  }

  clearCoupon(): void {
    this.quote.set(null); this.couponInput = ''; this.couponError.set(null);
    this.invalidateDocuments();
  }

  async openDocuments(): Promise<void> {
    if (!this.planIdValue || this.loadingDocuments()) return;
    this.loadingDocuments.set(true); this.documentsError.set(null);
    try { this.documents.set(await firstValueFrom(this.api.documents(this.planIdValue, this.quote()?.code ?? null))); }
    catch (response) { this.documentsError.set(coachingErrorMessage(response, 'Sözleşme metinleri yüklenemedi.')); }
    finally { this.loadingDocuments.set(false); }
  }

  setAccepted(value: boolean): void {
    this.accepted.set(!!value && !!this.documents());
    this.emit();
  }

  /** Back to an empty checkout, for example after the notice was sent. */
  reset(): void {
    this.couponInput = ''; this.quote.set(null); this.couponError.set(null);
    this.documents.set(null); this.documentsError.set(null); this.accepted.set(false);
    this.emit();
  }

  /** The amount the server will price with, for showing next to the form. */
  amount(): number { return this.quote()?.amount ?? this.price; }

  private invalidateDocuments(): void {
    // The texts carry the price: a new code needs the buyer to read and accept them again.
    this.documents.set(null); this.accepted.set(false);
    this.emit();
  }

  private emit(): void {
    this.choiceChange.emit({ couponCode: this.quote()?.code ?? null, acceptSalesContract: this.accepted() });
  }
}
