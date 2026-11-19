import { CommonModule } from '@angular/common';
import { CurrencyNamePipe } from './currency-name.pipe';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import {
  BUYER_TYPE_LABELS,
  BillingAdminService,
  BillingPlan,
  BillingPlanRequest,
  BillingProduct,
  BuyerType,
  billingErrorMessage
} from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

interface EntitlementChoice {
  entitlementId: string;
  name: string;
  key: string;
  hasLimit: boolean;
  buyer: boolean;
  seatHolder: boolean;
  limitValue: number | null;
}

interface PlanDraft {
  id: string | null;
  name: string;
  description: string;
  buyerType: BuyerType;
  price: number;
  durationDays: number;
  trialDays: number;
  hasSeats: boolean;
  seatCount: number | null;
  hasExtraSeats: boolean;
  extraSeatPrice: number | null;
  isContactOnly: boolean;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string;
  groups: { productName: string; choices: EntitlementChoice[] }[];
}

/**
 * "Abonelik ve Ödeme › Planlar": a price for a period, with the three settings that make every sales model optional
 * per product: who buys it, whether it has seats, and which entitlements it gives (several products = a bundle).
 */
@Component({
  selector: 'app-billing-plans',
  standalone: true,
  imports: [CurrencyNamePipe, CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-plans-title">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
          <h1 id="billing-plans-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Planlar</h1>
          <p class="mt-2 text-sm text-(--ui-text-muted)">Bir üründe hangi satış biçiminin olacağını planlar belirler: bireysel, öğretmen koltuğu ya da kurum planı açarsanız o satış başlar; planı kapatırsanız biter. Birden çok ürünün hakkını veren plan paket olur.</p>
        </div>
        <button type="button" class="primary" (click)="start()" [disabled]="loading()" data-testid="new-plan">Yeni plan</button>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      @if (draft(); as plan) {
        <form (ngSubmit)="save()" class="form-card" data-testid="plan-form">
          <h3>{{ plan.id ? 'Planı düzenle' : 'Yeni plan' }}</h3>
          <div class="form-grid">
            <label class="wide">Plan adı<input [(ngModel)]="plan.name" name="planName" required maxlength="200" /></label>
            <label class="wide">Açıklama<textarea [(ngModel)]="plan.description" name="planDescription" maxlength="2000"></textarea></label>
            <label>Kim alır?
              <select [(ngModel)]="plan.buyerType" (ngModelChange)="syncBuyer()" name="planBuyerType">
                <option value="Individual">Bireysel (öğrenci ya da velisi)</option>
                <option value="Teacher">Öğretmen</option>
                <option value="Institution">Kurum</option>
              </select>
            </label>
            <label>Süre (gün)<input type="number" [(ngModel)]="plan.durationDays" name="planDurationDays" min="1" max="3650" required /></label>
            <label>Fiyat (TL)<input type="number" [(ngModel)]="plan.price" name="planPrice" min="0" step="0.01" required [disabled]="plan.isContactOnly" /></label>
            <label>Ücretsiz deneme (gün)<input type="number" [(ngModel)]="plan.trialDays" name="planTrialDays" min="0" max="90" [disabled]="plan.buyerType === 'Institution'" />
              <span class="muted">{{ plan.buyerType === 'Institution' ? 'Kurum planlarında deneme olmaz.' : '0 yazarsanız deneme sunulmaz.' }}</span></label>
            <label class="check"><input type="checkbox" [(ngModel)]="plan.isContactOnly" (ngModelChange)="syncContactOnly()" name="planContactOnly" /> Fiyat yerine "iletişime geçin" göster</label>
            <label>Sıra<input type="number" [(ngModel)]="plan.sortOrder" name="planSortOrder" min="0" max="10000" /></label>
          </div>

          @if (plan.buyerType !== 'Individual') {
            <h3 class="mt-4">Koltuklar</h3>
            <div class="form-grid">
              <label class="check"><input type="checkbox" [(ngModel)]="plan.hasSeats" (ngModelChange)="syncSeats()" name="planHasSeats" /> Plan koltuk içerir</label>
              <label>Koltuk sayısı<input type="number" [(ngModel)]="plan.seatCount" name="planSeatCount" min="1" max="100000" [disabled]="!plan.hasSeats" /></label>
              @if (plan.buyerType === 'Institution') {
                <label class="check"><input type="checkbox" [(ngModel)]="plan.hasExtraSeats" name="planHasExtraSeats" [disabled]="!plan.hasSeats" /> Ek koltuk satılabilir</label>
                <label>Ek koltuk fiyatı (TL, dönem boyu)<input type="number" [(ngModel)]="plan.extraSeatPrice" name="planExtraSeatPrice" min="0.01" step="0.01" [disabled]="!plan.hasSeats || !plan.hasExtraSeats" /></label>
              }
            </div>
          }

          <h3 class="mt-4">Verdiği haklar</h3>
          <p class="muted mb-2">"Alıcıya": planı satın alan hesap. "Koltuk sahibine": koltuğun verildiği her kullanıcı.</p>
          @for (group of plan.groups; track group.productName) {
            <p class="mt-2 text-sm font-semibold text-(--ui-text)">{{ group.productName }}</p>
            <div class="entitlement-grid mt-1">
              @for (choice of group.choices; track choice.entitlementId) {
                <div class="entitlement-row">
                  <span>{{ choice.name }} <span class="muted font-mono">{{ choice.key }}</span></span>
                  <label class="check"><input type="checkbox" [(ngModel)]="choice.buyer" [name]="'buyer-' + choice.entitlementId" /> Alıcıya</label>
                  <label class="check"><input type="checkbox" [(ngModel)]="choice.seatHolder" [name]="'seat-' + choice.entitlementId" [disabled]="!plan.hasSeats" /> Koltuk sahibine</label>
                  @if (choice.hasLimit) {
                    <input type="number" [(ngModel)]="choice.limitValue" [name]="'limit-' + choice.entitlementId" min="1" max="100000" placeholder="Sınır" [attr.aria-label]="choice.name + ' sınırı'" [disabled]="!choice.buyer && !choice.seatHolder" />
                  } @else { <span></span> }
                </div>
              }
            </div>
          } @empty { <p class="muted">Önce "Ürünler ve haklar" sayfasında hak tanımlayın.</p> }

          <h3 class="mt-4">Görünüm</h3>
          <div class="form-grid">
            <label class="wide">Plan kartındaki özellikler (her satıra bir tane)<textarea [(ngModel)]="plan.features" name="planFeatures" rows="4"></textarea></label>
            <label class="check"><input type="checkbox" [(ngModel)]="plan.isActive" name="planActive" /> Etkin (satın alınabilir)</label>
            <label class="check"><input type="checkbox" [(ngModel)]="plan.isPublic" name="planPublic" /> Satın alma sayfasında göster</label>
          </div>
          <div class="form-actions"><button type="button" class="secondary" (click)="draft.set(null)">Vazgeç</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      <div class="data-card">
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <label class="text-sm text-(--ui-text)" for="plan-product-filter">Ürün</label>
          <select id="plan-product-filter" [ngModel]="productFilter()" (ngModelChange)="productFilter.set($event)" name="planProductFilter">
            <option value="">Tüm ürünler</option>
            @for (product of products(); track product.id) { <option [value]="product.code">{{ product.name }}</option> }
          </select>
        </div>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Plan</th><th>Kim alır</th><th>Fiyat</th><th>Süre</th><th>Koltuk</th><th>Ürünler</th><th>Durum</th><th></th></tr></thead>
            <tbody>
              @for (plan of visiblePlans(); track plan.id) {
                <tr>
                  <td><strong>{{ plan.name }}</strong>@if (plan.trialDays) { <div class="muted">{{ plan.trialDays }} gün deneme</div> }</td>
                  <td>{{ buyerLabel(plan.buyerType) }}</td>
                  <td>{{ plan.isContactOnly ? 'İletişime geçin' : (plan.price | number:'1.2-2') + ' ' + (plan.currency | currencyName) }}</td>
                  <td>{{ plan.durationDays }} gün</td>
                  <td>{{ plan.seatCount ?? '—' }}@if (plan.extraSeatPrice) { <div class="muted">ek koltuk {{ plan.extraSeatPrice | number:'1.2-2' }}</div> }</td>
                  <td>{{ productNames(plan) }}@if (plan.productCodes.length > 1) { <div><span class="badge">Paket</span></div> }</td>
                  <td><span class="badge" [class.on]="plan.isActive" [class.off]="!plan.isActive">{{ plan.isActive ? 'Etkin' : 'Kapalı' }}</span>@if (!plan.isPublic) { <div class="muted">Gizli</div> }</td>
                  <td class="actions"><button type="button" (click)="start(plan)">Düzenle</button></td>
                </tr>
              } @empty { <tr><td colspan="8" class="empty">{{ loading() ? 'Yükleniyor…' : 'Plan yok. Satışa başlamak için bir plan ekleyin.' }}</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES]
})
export class BillingPlansComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly products = signal<BillingProduct[]>([]);
  readonly plans = signal<BillingPlan[]>([]);
  readonly draft = signal<PlanDraft | null>(null);
  readonly productFilter = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly visiblePlans = computed(() => {
    const code = this.productFilter();
    return code ? this.plans().filter(plan => plan.productCodes.includes(code)) : this.plans();
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({ products: this.billing.getProducts(), plans: this.billing.getPlans() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ products, plans }) => { this.products.set(products); this.plans.set(plans); },
        error: () => this.error.set('Planlar yüklenemedi.')
      });
  }

  buyerLabel(type: BuyerType): string { return BUYER_TYPE_LABELS[type] ?? type; }

  productNames(plan: BillingPlan): string {
    return plan.productCodes.map(code => this.products().find(product => product.code === code)?.name ?? code).join(', ') || '—';
  }

  start(plan?: BillingPlan): void {
    const given = (entitlementId: string, holder: 'Buyer' | 'SeatHolder') =>
      plan?.entitlements.find(item => item.entitlementId === entitlementId && item.holder === holder);
    this.draft.set({
      id: plan?.id ?? null,
      name: plan?.name ?? '',
      description: plan?.description ?? '',
      buyerType: plan?.buyerType ?? 'Individual',
      price: plan?.price ?? 0,
      durationDays: plan?.durationDays ?? 30,
      trialDays: plan?.trialDays ?? 0,
      hasSeats: plan?.seatCount != null,
      seatCount: plan?.seatCount ?? null,
      hasExtraSeats: plan?.extraSeatPrice != null,
      extraSeatPrice: plan?.extraSeatPrice ?? null,
      isContactOnly: plan?.isContactOnly ?? false,
      isActive: plan?.isActive ?? true,
      isPublic: plan?.isPublic ?? true,
      sortOrder: plan?.sortOrder ?? this.plans().length + 1,
      features: (plan?.features ?? []).join('\n'),
      groups: this.products().filter(product => product.entitlements.length > 0).map(product => ({
        productName: product.name,
        choices: product.entitlements.map(entitlement => ({
          entitlementId: entitlement.id,
          name: entitlement.name,
          key: entitlement.key,
          hasLimit: entitlement.hasLimit,
          buyer: !!given(entitlement.id, 'Buyer'),
          seatHolder: !!given(entitlement.id, 'SeatHolder'),
          limitValue: (given(entitlement.id, 'Buyer') ?? given(entitlement.id, 'SeatHolder'))?.limitValue ?? null
        }))
      }))
    });
  }

  /** A seat belongs to a teacher or an institution; an institution gets no trial. */
  syncBuyer(): void {
    const plan = this.draft();
    if (!plan) return;
    if (plan.buyerType === 'Individual') { plan.hasSeats = false; this.syncSeats(); }
    if (plan.buyerType !== 'Institution') { plan.hasExtraSeats = false; plan.extraSeatPrice = null; }
    if (plan.buyerType === 'Institution') plan.trialDays = 0;
  }

  syncSeats(): void {
    const plan = this.draft();
    if (!plan || plan.hasSeats) return;
    plan.seatCount = null;
    plan.hasExtraSeats = false;
    plan.extraSeatPrice = null;
    plan.groups.forEach(group => group.choices.forEach(choice => choice.seatHolder = false));
  }

  syncContactOnly(): void {
    const plan = this.draft();
    if (plan?.isContactOnly) plan.price = 0;
  }

  /** What the form sends; the server checks the rules again. */
  toRequest(plan: PlanDraft): BillingPlanRequest {
    const number = (value: unknown) => value === null || value === undefined || value === '' ? null : Number(value);
    return {
      name: plan.name.trim(),
      description: plan.description.trim() || null,
      buyerType: plan.buyerType,
      price: plan.isContactOnly ? 0 : Number(plan.price) || 0,
      durationDays: Number(plan.durationDays) || 0,
      trialDays: plan.buyerType === 'Institution' ? 0 : Number(plan.trialDays) || 0,
      seatCount: plan.hasSeats ? number(plan.seatCount) : null,
      extraSeatPrice: plan.hasSeats && plan.hasExtraSeats ? number(plan.extraSeatPrice) : null,
      isContactOnly: plan.isContactOnly,
      isActive: plan.isActive,
      isPublic: plan.isPublic,
      sortOrder: Number(plan.sortOrder) || 0,
      features: plan.features.split('\n').map(item => item.trim()).filter(item => item.length > 0),
      entitlements: plan.groups.flatMap(group => group.choices).flatMap(choice => {
        const limitValue = choice.hasLimit ? number(choice.limitValue) : null;
        return [
          ...(choice.buyer ? [{ entitlementId: choice.entitlementId, holder: 'Buyer' as const, limitValue }] : []),
          ...(choice.seatHolder && plan.hasSeats ? [{ entitlementId: choice.entitlementId, holder: 'SeatHolder' as const, limitValue }] : [])
        ];
      })
    };
  }

  save(): void {
    const plan = this.draft();
    if (!plan) return;
    const request = this.toRequest(plan);
    if (request.entitlements.length === 0) { this.error.set('Plan en az bir hak vermelidir.'); return; }
    this.saving.set(true);
    this.error.set('');
    (plan.id ? this.billing.updatePlan(plan.id, request) : this.billing.createPlan(request))
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => { this.draft.set(null); this.toaster.success('Plan kaydedildi.'); this.load(); },
        error: error => this.error.set(billingErrorMessage(error, 'Plan kaydedilemedi.'))
      });
  }
}
