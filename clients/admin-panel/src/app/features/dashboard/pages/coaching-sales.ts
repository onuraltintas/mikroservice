import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingManagementService, CoachingPayment, CoachingSubscriptionPlan } from '../../../core/services/coaching-management.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { saveCoachingMessageFile } from '../../../../../shared/coaching-messages.api';
import { coachingErrorMessage, coachingMoney, normalizeCoachingCoupon, COACHING_COUPON_PATTERN } from '../../../../../shared/coaching-sales.api';

export interface CoachingCoupon {
  id: string; code: string; kind: 'Percent' | 'Amount'; value: number; validFrom: string | null; validUntil: string | null; maxUses: number | null;
  usedCount: number; planIds: string[]; isActive: boolean; createdAt: string;
}
export interface CoachingCouponRequest {
  code: string; kind: 'Percent' | 'Amount'; value: number; validFrom: string | null; validUntil: string | null; maxUses: number | null; planIds: string[]; isActive: boolean;
}
export interface CoachingSellerInfo { title: string | null; address: string | null; taxOffice: string | null; taxNumber: string | null; mersisNumber: string | null; email: string | null; phone: string | null }
export interface CoachingWithdrawal {
  transferId: string; userName: string; userEmail: string; planName: string; amount: number; currency: string; approvedAt: string | null;
  withdrawalStatus: string | null; reason: string | null; requestedAt: string | null; reviewNote: string | null; reviewedAt: string | null;
}
export interface CoachingInvoice { id: string; paymentId: string; invoiceNumber: string; issuedOn: string; fileName: string; sizeBytes: number }

const INVOICE_MAX_BYTES = 10 * 1024 * 1024;

/** The admin side of the O10 sales API. */
export class CoachingSalesAdminApi {
  constructor(private readonly http: HttpClient, private readonly apiUrl: string) {}
  private get url() { return `${this.apiUrl}/coaching-admin/sales`; }
  coupons() { return this.http.get<{ data: CoachingCoupon[] }>(`${this.url}/coupons`).pipe(map(r => r.data)); }
  createCoupon(request: CoachingCouponRequest) { return this.http.post<{ data: CoachingCoupon }>(`${this.url}/coupons`, request).pipe(map(r => r.data)); }
  updateCoupon(id: string, request: CoachingCouponRequest) { return this.http.put<{ data: CoachingCoupon }>(`${this.url}/coupons/${encodeURIComponent(id)}`, request).pipe(map(r => r.data)); }
  seller() { return this.http.get<{ data: CoachingSellerInfo }>(`${this.url}/seller`).pipe(map(r => r.data)); }
  saveSeller(request: CoachingSellerInfo) { return this.http.put<{ data: CoachingSellerInfo }>(`${this.url}/seller`, request).pipe(map(r => r.data)); }
  withdrawals() { return this.http.get<{ data: CoachingWithdrawal[] }>(`${this.url}/withdrawals`).pipe(map(r => r.data)); }
  reviewWithdrawal(transferId: string, approve: boolean, note: string | null) {
    return this.http.post<{ success: boolean }>(`${this.url}/withdrawals/${encodeURIComponent(transferId)}/review`, { approve, note });
  }
  invoices(paymentIds: string[]) {
    let params = new HttpParams();
    for (const id of paymentIds) params = params.append('paymentIds', id);
    return this.http.get<{ data: CoachingInvoice[] }>(`${this.url}/invoices`, { params }).pipe(map(r => r.data));
  }
  attachInvoice(paymentId: string, invoiceNumber: string, issuedOn: string, file: Blob, fileName: string) {
    const params = new HttpParams().set('invoiceNumber', invoiceNumber).set('issuedOn', issuedOn).set('fileName', fileName);
    return this.http.post<{ data: CoachingInvoice }>(`${this.url}/payments/${encodeURIComponent(paymentId)}/invoice`, file,
      { params, headers: { 'Content-Type': 'application/pdf' } }).pipe(map(r => r.data));
  }
  invoice(paymentId: string) { return this.http.get(`${this.url}/payments/${encodeURIComponent(paymentId)}/invoice`, { responseType: 'blob' }); }
  removeInvoice(paymentId: string) { return this.http.delete<{ success: boolean }>(`${this.url}/payments/${encodeURIComponent(paymentId)}/invoice`); }
}

type SalesTab = 'coupons' | 'seller' | 'withdrawals' | 'invoices';

/**
 * Roadmap O10: discount codes, the seller details printed on the pre-information form and distance-sales contract,
 * withdrawal (cayma) decisions, and the manually issued invoices attached to approved payments.
 */
@Component({
  selector: 'app-coaching-sales', standalone: true, imports: [DatePipe, FormsModule],
  styles: [`
    .tabs{display:flex;flex-wrap:wrap;gap:.4rem;margin-bottom:1rem}
    .tabs button{border:1px solid #cbd5e1;border-radius:.5rem;padding:.35rem .8rem;font-size:.85rem}
    .tabs button[aria-selected=true]{background:#4f46e5;border-color:#4f46e5;color:#fff}
    section{border:1px solid #e2e8f0;border-radius:.75rem;padding:1rem;margin-bottom:1rem}
    :host-context(.dark) section{border-color:#334155}
    .grid{display:grid;gap:.75rem;grid-template-columns:repeat(auto-fit,minmax(13rem,1fr))}
    label{display:grid;gap:.25rem;font-size:.85rem}
    input,select,textarea{border:1px solid #cbd5e1;border-radius:.4rem;padding:.4rem .55rem;background:transparent;color:inherit;font:inherit}
    table{width:100%;font-size:.85rem;border-collapse:collapse}
    th{text-align:left;font-weight:600}
    td,th{padding:.4rem .3rem;border-top:1px solid #e2e8f0;vertical-align:top}
    :host-context(.dark) td,:host-context(.dark) th{border-color:#334155}
    .muted{color:#64748b;font-size:.8rem}
    .primary{background:#4f46e5;color:#fff;border-radius:.5rem;padding:.4rem .9rem;font-weight:600}
    .secondary{border:1px solid #cbd5e1;border-radius:.5rem;padding:.3rem .7rem}
    button:disabled{opacity:.55;cursor:not-allowed}
    .actions{display:flex;flex-wrap:wrap;gap:.35rem}
    .note{border-left:4px solid #f59e0b;background:#fffbeb;color:#78350f;padding:.6rem .8rem;border-radius:.4rem;font-size:.85rem;margin-bottom:.75rem}
    :host-context(.dark) .note{background:#451a03;color:#fde68a}
    .plans{display:flex;flex-wrap:wrap;gap:.5rem}
    .plans label{display:flex;gap:.3rem;align-items:center}
  `],
  template: `
    <h1 class="mb-2 text-2xl font-bold">Satış araçları</h1>
    <p class="mb-4 max-w-3xl text-sm text-slate-600 dark:text-slate-300">İndirim kodları, sözleşmelerde görünen satıcı bilgileri, cayma talepleri ve elle kesilen faturaların kaydı.</p>
    @if (error()) { <p role="alert" class="mb-3 rounded-lg bg-red-50 p-3 text-sm text-red-700 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
    @if (success()) { <p role="status" class="mb-3 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200">{{ success() }}</p> }

    <div class="tabs" role="tablist">
      @for (item of tabs; track item.id) {
        <button type="button" role="tab" [attr.aria-selected]="tab() === item.id" [attr.data-testid]="'sales-tab-' + item.id" (click)="select(item.id)">{{ item.label }}@if (item.id === 'withdrawals' && pendingWithdrawals()) { ({{ pendingWithdrawals() }}) }</button>
      }
    </div>

    @switch (tab()) {
      @case ('coupons') {
        <section aria-labelledby="coupon-form-heading">
          <h2 id="coupon-form-heading" class="mb-3 font-semibold">{{ editingCouponId() ? 'İndirim kodunu düzenle' : 'Yeni indirim kodu' }}</h2>
          <form (ngSubmit)="saveCoupon()">
            <div class="grid">
              <label>Kod<input name="couponCode" [(ngModel)]="couponDraft.code" maxlength="30" required data-testid="coupon-code" /><span class="muted">3–30 harf, rakam ya da tire. Büyük harfe çevrilir.</span></label>
              <label>Tür<select name="couponKind" [(ngModel)]="couponDraft.kind"><option value="Percent">Yüzde (%)</option><option value="Amount">Tutar (TRY)</option></select></label>
              <label>{{ couponDraft.kind === 'Percent' ? 'İndirim yüzdesi' : 'İndirim tutarı' }}<input type="number" name="couponValue" [(ngModel)]="couponDraft.value" min="0.01" [max]="couponDraft.kind === 'Percent' ? 100 : 1000000" step="0.01" required /></label>
              <label>Başlangıç (isteğe bağlı)<input type="date" name="couponFrom" [(ngModel)]="couponFrom" /></label>
              <label>Bitiş (isteğe bağlı)<input type="date" name="couponUntil" [(ngModel)]="couponUntil" /></label>
              <label>Toplam kullanım sınırı<input type="number" name="couponMaxUses" [(ngModel)]="couponDraft.maxUses" min="1" placeholder="Sınırsız" /></label>
            </div>
            <fieldset class="mt-3"><legend class="text-sm font-medium">Geçerli planlar</legend>
              <p class="muted">Hiçbiri seçilmezse kod bütün bireysel, veli ve öğretmen planlarında geçer.</p>
              <div class="plans">@for (plan of couponPlans(); track plan.id) {
                <label><input type="checkbox" [checked]="couponDraft.planIds.includes(plan.id)" (change)="togglePlan(plan.id)" [attr.data-testid]="'coupon-plan-' + plan.id" />{{ plan.name }}</label>
              }</div>
            </fieldset>
            <label class="mt-3" style="display:flex;gap:.4rem;align-items:center"><input type="checkbox" name="couponActive" [(ngModel)]="couponDraft.isActive" />Etkin</label>
            <div class="actions mt-3"><button type="submit" class="primary" [disabled]="working()">{{ editingCouponId() ? 'Kaydet' : 'Kodu oluştur' }}</button>
              @if (editingCouponId()) { <button type="button" class="secondary" (click)="newCoupon()">Vazgeç</button> }</div>
          </form>
        </section>
        <section aria-labelledby="coupon-list-heading">
          <h2 id="coupon-list-heading" class="mb-3 font-semibold">İndirim kodları</h2>
          <table><thead><tr><th>Kod</th><th>İndirim</th><th>Geçerlilik</th><th>Kullanım</th><th>Durum</th><th></th></tr></thead><tbody>
            @for (coupon of coupons(); track coupon.id) {
              <tr [attr.data-testid]="'coupon-' + coupon.code"><td><strong>{{ coupon.code }}</strong><div class="muted">{{ planNames(coupon.planIds) }}</div></td>
                <td>{{ coupon.kind === 'Percent' ? '%' + coupon.value : money(coupon.value) + ' TRY' }}</td>
                <td>{{ coupon.validFrom ? (coupon.validFrom | date:'dd.MM.yyyy') : '—' }} – {{ coupon.validUntil ? (coupon.validUntil | date:'dd.MM.yyyy') : '—' }}</td>
                <td>{{ coupon.usedCount }}{{ coupon.maxUses ? ' / ' + coupon.maxUses : '' }}</td>
                <td>{{ coupon.isActive ? 'Etkin' : 'Kapalı' }}</td>
                <td class="actions"><button type="button" class="secondary" (click)="editCoupon(coupon)">Düzenle</button>
                  @if (coupon.isActive) { <button type="button" class="secondary" [attr.data-testid]="'coupon-disable-' + coupon.code" (click)="disableCoupon(coupon)">Kapat</button> }</td></tr>
            } @empty { <tr><td colspan="6" class="muted">Henüz indirim kodu yok.</td></tr> }
          </tbody></table>
        </section>
      }
      @case ('seller') {
        <section aria-labelledby="seller-heading">
          <h2 id="seller-heading" class="mb-3 font-semibold">Satıcı bilgileri</h2>
          <p class="note" data-testid="lawyer-note">Ön bilgilendirme formu ve mesafeli satış sözleşmesi metinleri <strong>taslaktır</strong>. Satışa açmadan önce bir avukata kontrol ettirin.
            Buraya girdiğiniz bilgiler, alıcıya gösterilen belgelerde satıcı olarak yer alır; boş alanlar belgede "girilmedi" diye görünür.</p>
          <form (ngSubmit)="saveSeller()">
            <div class="grid">
              <label>Unvan<input name="sellerTitle" [(ngModel)]="seller.title" maxlength="200" data-testid="seller-title" /></label>
              <label>Vergi dairesi<input name="sellerTaxOffice" [(ngModel)]="seller.taxOffice" maxlength="100" /></label>
              <label>Vergi / TC kimlik no<input name="sellerTaxNumber" [(ngModel)]="seller.taxNumber" maxlength="20" /></label>
              <label>MERSİS no (varsa)<input name="sellerMersis" [(ngModel)]="seller.mersisNumber" maxlength="20" /></label>
              <label>E-posta<input type="email" name="sellerEmail" [(ngModel)]="seller.email" maxlength="200" /></label>
              <label>Telefon<input name="sellerPhone" [(ngModel)]="seller.phone" maxlength="40" /></label>
            </div>
            <label class="mt-3">Adres<textarea name="sellerAddress" [(ngModel)]="seller.address" maxlength="500" rows="2"></textarea></label>
            <button type="submit" class="primary mt-3" [disabled]="working()">Kaydet</button>
          </form>
        </section>
      }
      @case ('withdrawals') {
        <section aria-labelledby="withdrawals-heading">
          <h2 id="withdrawals-heading" class="mb-3 font-semibold">Cayma talepleri</h2>
          <p class="muted mb-2">Onaylanan caymada abonelik hemen sona erer. İadeyi, kullanılan günleri düşerek 14 gün içinde alıcının ödeme yaptığı hesaba siz yaparsınız.</p>
          <table><thead><tr><th>Alıcı</th><th>Plan / tutar</th><th>Talep</th><th>Durum</th><th></th></tr></thead><tbody>
            @for (item of withdrawals(); track item.transferId) {
              <tr [attr.data-testid]="'withdrawal-' + item.transferId"><td><strong>{{ item.userName }}</strong><div class="muted">{{ item.userEmail }}</div></td>
                <td>{{ item.planName }}<div class="muted">{{ money(item.amount) }} {{ item.currency }} · onay {{ item.approvedAt | date:'dd.MM.yyyy' }}</div></td>
                <td>{{ item.requestedAt | date:'dd.MM.yyyy HH:mm' }}<div class="muted">{{ item.reason }}</div></td>
                <td>{{ withdrawalLabel(item.withdrawalStatus) }}@if (item.reviewNote) { <div class="muted">{{ item.reviewNote }}</div> }</td>
                <td>@if (item.withdrawalStatus === 'Requested') {
                  <div class="actions"><button type="button" class="secondary" [attr.data-testid]="'approve-withdrawal-' + item.transferId" [disabled]="working()" (click)="reviewWithdrawal(item, true)">Onayla</button>
                    <button type="button" class="secondary" [attr.data-testid]="'reject-withdrawal-' + item.transferId" [disabled]="working()" (click)="reviewWithdrawal(item, false)">Reddet</button></div>
                }</td></tr>
            } @empty { <tr><td colspan="5" class="muted">Cayma talebi yok.</td></tr> }
          </tbody></table>
        </section>
      }
      @case ('invoices') {
        <section aria-labelledby="invoices-heading">
          <h2 id="invoices-heading" class="mb-3 font-semibold">Faturalar</h2>
          <p class="muted mb-2">Faturayı muhasebe programınızda ya da GİB portalında kesin, PDF'ini buradan ilgili ödemeye ekleyin. Alıcı faturayı "Ödemelerim" bölümünden indirir.</p>
          <div class="actions mb-2"><input name="paymentSearch" [(ngModel)]="paymentSearch" placeholder="Ad, e-posta ya da referans" maxlength="100" /><button type="button" class="secondary" (click)="loadPayments(1)">Ara</button></div>
          <table><thead><tr><th>Ödeme</th><th>Tutar</th><th>Fatura</th></tr></thead><tbody>
            @for (payment of payments(); track payment.id) {
              <tr [attr.data-testid]="'payment-row-' + payment.id"><td><strong>{{ payment.userName }}</strong><div class="muted">{{ payment.planName }} · {{ payment.createdAt | date:'dd.MM.yyyy' }}</div></td>
                <td>{{ money(payment.amount) }} {{ payment.currency }}</td>
                <td>
                  @if (invoices()[payment.id]; as invoice) {
                    <div>{{ invoice.invoiceNumber }} · {{ invoice.issuedOn | date:'dd.MM.yyyy' }}</div>
                    <div class="actions"><button type="button" class="secondary" (click)="downloadInvoice(payment)">İndir</button>
                      <button type="button" class="secondary" [attr.data-testid]="'remove-invoice-' + payment.id" [disabled]="working()" (click)="removeInvoice(payment)">Kaldır</button></div>
                  } @else if (invoiceFor() === payment.id) {
                    <form class="grid" (ngSubmit)="attachInvoice(payment)">
                      <label>Fatura no<input name="invoiceNumber" [(ngModel)]="invoiceNumber" maxlength="40" required /></label>
                      <label>Düzenlenme tarihi<input type="date" name="invoiceDate" [(ngModel)]="invoiceDate" required /></label>
                      <label>PDF<input type="file" accept="application/pdf,.pdf" (change)="chooseInvoice($event)" /></label>
                      <div class="actions"><button type="submit" class="primary" [disabled]="working()">Ekle</button><button type="button" class="secondary" (click)="invoiceFor.set(null)">Vazgeç</button></div>
                    </form>
                  } @else { <button type="button" class="secondary" [attr.data-testid]="'add-invoice-' + payment.id" (click)="startInvoice(payment)">Fatura ekle</button> }
                </td></tr>
            } @empty { <tr><td colspan="3" class="muted">Ödeme kaydı yok.</td></tr> }
          </tbody></table>
          <div class="actions mt-2"><button type="button" class="secondary" [disabled]="paymentPage() <= 1" (click)="loadPayments(paymentPage() - 1)">Önceki</button>
            <span class="muted">Sayfa {{ paymentPage() }} / {{ paymentPages() }}</span>
            <button type="button" class="secondary" [disabled]="paymentPage() >= paymentPages()" (click)="loadPayments(paymentPage() + 1)">Sonraki</button></div>
        </section>
      }
    }
  `
})
export class CoachingSalesComponent implements OnInit {
  private readonly api = new CoachingSalesAdminApi(inject(HttpClient), environment.apiUrl);
  private readonly management = inject(CoachingManagementService);
  private readonly toaster = inject(ToasterService);
  readonly money = coachingMoney;
  readonly tabs: { id: SalesTab; label: string }[] = [
    { id: 'coupons', label: 'İndirim kodları' }, { id: 'seller', label: 'Satıcı ve sözleşmeler' },
    { id: 'withdrawals', label: 'Cayma talepleri' }, { id: 'invoices', label: 'Faturalar' }
  ];
  readonly tab = signal<SalesTab>('coupons');
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly working = signal(false);

  readonly coupons = signal<CoachingCoupon[]>([]);
  readonly couponPlans = signal<CoachingSubscriptionPlan[]>([]);
  readonly editingCouponId = signal<string | null>(null);
  couponDraft: CoachingCouponRequest = this.emptyCoupon();
  couponFrom = '';
  couponUntil = '';

  seller: CoachingSellerInfo = { title: null, address: null, taxOffice: null, taxNumber: null, mersisNumber: null, email: null, phone: null };

  readonly withdrawals = signal<CoachingWithdrawal[]>([]);
  readonly pendingWithdrawals = signal(0);

  readonly payments = signal<CoachingPayment[]>([]);
  readonly invoices = signal<Record<string, CoachingInvoice>>({});
  readonly paymentPage = signal(1);
  readonly paymentPages = signal(1);
  readonly invoiceFor = signal<string | null>(null);
  paymentSearch = '';
  invoiceNumber = '';
  invoiceDate = '';
  private invoiceFile: File | null = null;

  ngOnInit(): void {
    void this.loadCoupons();
    void this.loadWithdrawals();
    this.management.getPlans(true).subscribe({
      next: plans => this.couponPlans.set(plans.filter(plan => plan.audience !== 'Institution' && !plan.isContactOnly)),
      error: () => this.couponPlans.set([])
    });
  }

  select(tab: SalesTab): void {
    this.tab.set(tab); this.error.set(null); this.success.set(null);
    if (tab === 'seller') void this.loadSeller();
    if (tab === 'withdrawals') void this.loadWithdrawals();
    if (tab === 'invoices') void this.loadPayments(this.paymentPage());
  }

  // ---- Coupons -----------------------------------------------------------------------------------------------------

  async loadCoupons(): Promise<void> {
    try { this.coupons.set(await firstValueFrom(this.api.coupons())); }
    catch { this.error.set('İndirim kodları yüklenemedi.'); }
  }

  newCoupon(): void { this.editingCouponId.set(null); this.couponDraft = this.emptyCoupon(); this.couponFrom = ''; this.couponUntil = ''; }

  editCoupon(coupon: CoachingCoupon): void {
    this.editingCouponId.set(coupon.id);
    this.couponDraft = { code: coupon.code, kind: coupon.kind, value: coupon.value, validFrom: coupon.validFrom, validUntil: coupon.validUntil,
      maxUses: coupon.maxUses, planIds: [...coupon.planIds], isActive: coupon.isActive };
    this.couponFrom = coupon.validFrom?.slice(0, 10) ?? ''; this.couponUntil = coupon.validUntil?.slice(0, 10) ?? '';
  }

  togglePlan(planId: string): void {
    const ids = this.couponDraft.planIds;
    this.couponDraft = { ...this.couponDraft, planIds: ids.includes(planId) ? ids.filter(id => id !== planId) : [...ids, planId] };
  }

  async saveCoupon(): Promise<void> {
    const code = normalizeCoachingCoupon(this.couponDraft.code);
    const value = Number(this.couponDraft.value);
    if (!COACHING_COUPON_PATTERN.test(code)) { this.error.set('Kod 3–30 harf, rakam ya da tireden oluşmalı.'); return; }
    if (!(value > 0) || (this.couponDraft.kind === 'Percent' && value > 100)) { this.error.set(this.couponDraft.kind === 'Percent' ? 'Yüzde 0 ile 100 arasında olmalı.' : 'Tutar sıfırdan büyük olmalı.'); return; }
    // A date picks the whole day in Turkey: from 00:00 of the first day to the end of the last day.
    const validFrom = this.couponFrom ? new Date(`${this.couponFrom}T00:00:00+03:00`).toISOString() : null;
    const validUntil = this.couponUntil ? new Date(`${this.couponUntil}T23:59:59+03:00`).toISOString() : null;
    if (validFrom && validUntil && validFrom > validUntil) { this.error.set('Bitiş tarihi başlangıçtan önce olamaz.'); return; }
    const maxUses = this.couponDraft.maxUses ? Math.trunc(Number(this.couponDraft.maxUses)) : null;
    const request: CoachingCouponRequest = { ...this.couponDraft, code, value, validFrom, validUntil, maxUses };
    await this.run(async () => {
      const id = this.editingCouponId();
      const saved = await firstValueFrom(id ? this.api.updateCoupon(id, request) : this.api.createCoupon(request));
      this.coupons.update(items => id ? items.map(item => item.id === id ? saved : item) : [saved, ...items]);
      this.newCoupon();
      return id ? 'İndirim kodu güncellendi.' : `${saved.code} indirim kodu oluşturuldu.`;
    }, 'İndirim kodu kaydedilemedi.');
  }

  async disableCoupon(coupon: CoachingCoupon): Promise<void> {
    await this.run(async () => {
      const saved = await firstValueFrom(this.api.updateCoupon(coupon.id, { code: coupon.code, kind: coupon.kind, value: coupon.value,
        validFrom: coupon.validFrom, validUntil: coupon.validUntil, maxUses: coupon.maxUses, planIds: coupon.planIds, isActive: false }));
      this.coupons.update(items => items.map(item => item.id === coupon.id ? saved : item));
      return `${coupon.code} kapatıldı; yeni ödemelerde kullanılamaz.`;
    }, 'İndirim kodu kapatılamadı.');
  }

  planNames(ids: string[]): string {
    if (!ids.length) return 'Bütün planlar';
    return ids.map(id => this.couponPlans().find(plan => plan.id === id)?.name ?? 'Kaldırılmış plan').join(', ');
  }

  // ---- Seller ------------------------------------------------------------------------------------------------------

  async loadSeller(): Promise<void> {
    try { this.seller = { ...await firstValueFrom(this.api.seller()) }; }
    catch { this.error.set('Satıcı bilgileri yüklenemedi.'); }
  }

  async saveSeller(): Promise<void> {
    const clean = (value: string | null) => value?.trim() || null;
    const request: CoachingSellerInfo = { title: clean(this.seller.title), address: clean(this.seller.address), taxOffice: clean(this.seller.taxOffice),
      taxNumber: clean(this.seller.taxNumber), mersisNumber: clean(this.seller.mersisNumber), email: clean(this.seller.email), phone: clean(this.seller.phone) };
    await this.run(async () => { this.seller = { ...await firstValueFrom(this.api.saveSeller(request)) }; return 'Satıcı bilgileri kaydedildi. Yeni ödemelerin belgelerinde kullanılır.'; },
      'Satıcı bilgileri kaydedilemedi.');
  }

  // ---- Withdrawals -------------------------------------------------------------------------------------------------

  async loadWithdrawals(): Promise<void> {
    try {
      const items = await firstValueFrom(this.api.withdrawals());
      this.withdrawals.set(items); this.pendingWithdrawals.set(items.filter(item => item.withdrawalStatus === 'Requested').length);
    } catch { this.error.set('Cayma talepleri yüklenemedi.'); }
  }

  withdrawalLabel(status: string | null): string {
    return ({ Requested: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' } as Record<string, string>)[status ?? ''] ?? '—';
  }

  async reviewWithdrawal(item: CoachingWithdrawal, approve: boolean): Promise<void> {
    let note: string | null = null;
    if (approve) {
      if (!await this.toaster.confirm(`${item.userName} için cayma onaylansın mı? ${item.planName} aboneliği hemen sona erer; ${coachingMoney(item.amount)} ${item.currency} iadeyi 14 gün içinde siz yaparsınız.`)) return;
    } else {
      note = (await this.toaster.prompt('Ret gerekçesi (alıcıya gösterilir)', ''))?.trim() || null;
      if (!note) { this.error.set('Cayma talebini reddetmek için gerekçe yazın.'); return; }
    }
    await this.run(async () => {
      await firstValueFrom(this.api.reviewWithdrawal(item.transferId, approve, note));
      await this.loadWithdrawals();
      return approve ? 'Cayma onaylandı; abonelik sona erdi. İadeyi bankadan yapmayı unutmayın.' : 'Cayma talebi reddedildi.';
    }, 'Cayma talebi işlenemedi.');
  }

  // ---- Invoices ----------------------------------------------------------------------------------------------------

  async loadPayments(page: number): Promise<void> {
    try {
      const result = await firstValueFrom(this.management.getPayments(page, 25, this.paymentSearch, ''));
      this.payments.set(result.items); this.paymentPage.set(result.pageNumber || page);
      this.paymentPages.set(Math.max(1, result.totalPages ?? Math.ceil(result.totalCount / Math.max(1, result.pageSize))));
      const ids = result.items.map(item => item.id);
      const invoices = ids.length ? await firstValueFrom(this.api.invoices(ids)) : [];
      this.invoices.set(Object.fromEntries(invoices.map(invoice => [invoice.paymentId, invoice])));
    } catch { this.error.set('Ödemeler yüklenemedi.'); }
  }

  startInvoice(payment: CoachingPayment): void {
    this.invoiceFor.set(payment.id); this.invoiceNumber = ''; this.invoiceFile = null;
    this.invoiceDate = new Date().toLocaleDateString('sv-SE', { timeZone: 'Europe/Istanbul' });
  }

  chooseInvoice(event: Event): void { this.invoiceFile = (event.target as HTMLInputElement).files?.[0] ?? null; }

  async attachInvoice(payment: CoachingPayment): Promise<void> {
    const number = this.invoiceNumber.trim();
    const file = this.invoiceFile;
    if (!number || !this.invoiceDate) { this.error.set('Fatura numarası ve tarihi gerekli.'); return; }
    if (!file) { this.error.set('Faturanın PDF dosyasını seçin.'); return; }
    if (file.size > INVOICE_MAX_BYTES) { this.error.set('Fatura PDF en fazla 10 MB olabilir.'); return; }
    if (!/\.pdf$/i.test(file.name) && file.type !== 'application/pdf') { this.error.set('Yalnız PDF fatura eklenebilir.'); return; }
    await this.run(async () => {
      const fileName = /\.pdf$/i.test(file.name) ? file.name : `${file.name}.pdf`;
      const invoice = await firstValueFrom(this.api.attachInvoice(payment.id, number, this.invoiceDate, file, fileName));
      this.invoices.update(items => ({ ...items, [payment.id]: invoice }));
      this.invoiceFor.set(null); this.invoiceFile = null;
      return `${invoice.invoiceNumber} numaralı fatura eklendi.`;
    }, 'Fatura eklenemedi.');
  }

  async downloadInvoice(payment: CoachingPayment): Promise<void> {
    const invoice = this.invoices()[payment.id];
    try { saveCoachingMessageFile(await firstValueFrom(this.api.invoice(payment.id)), invoice?.fileName ?? 'fatura.pdf'); }
    catch { this.error.set('Fatura indirilemedi.'); }
  }

  async removeInvoice(payment: CoachingPayment): Promise<void> {
    if (!await this.toaster.confirm('Bu ödemenin fatura kaydı ve PDF\'i kaldırılsın mı? Alıcı faturayı artık indiremez.')) return;
    await this.run(async () => {
      await firstValueFrom(this.api.removeInvoice(payment.id));
      this.invoices.update(items => { const next = { ...items }; delete next[payment.id]; return next; });
      return 'Fatura kaldırıldı.';
    }, 'Fatura kaldırılamadı.');
  }

  private async run(action: () => Promise<string>, fallback: string): Promise<void> {
    if (this.working()) return;
    this.working.set(true); this.error.set(null); this.success.set(null);
    try { this.success.set(await action()); }
    catch (response) { this.error.set(coachingErrorMessage(response, fallback)); }
    finally { this.working.set(false); }
  }

  private emptyCoupon(): CoachingCouponRequest {
    return { code: '', kind: 'Percent', value: 10, validFrom: null, validUntil: null, maxUses: null, planIds: [], isActive: true };
  }
}
