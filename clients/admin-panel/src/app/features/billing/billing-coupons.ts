import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { BillingAdminService, BillingCoupon, BillingPlan, CouponKind, billingErrorMessage } from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';
import { AdminMeterComponent } from '../../shared/components/admin-charts';

interface CouponDraft {
  id: string | null;
  code: string;
  kind: CouponKind;
  value: number;
  startsOn: string;
  endsOn: string;
  maxUses: number | null;
  planIds: Record<string, boolean>;
  isActive: boolean;
}

/** "Abonelik ve Ödeme › Kuponlar": discount codes for every product's plans. */
@Component({
  selector: 'app-billing-coupons',
  standalone: true,
  imports: [AdminMeterComponent, CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-coupons-title">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
          <h1 id="billing-coupons-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Kuponlar</h1>
          <p class="mt-2 text-sm text-(--ui-text-muted)">Bir hesap bir kuponu bir kez kullanır. Ödeme bekleyen siparişler de kullanım sayılır; reddedilen, düşen ya da onayı geri alınan sipariş kullanımını geri verir.</p>
        </div>
        <button type="button" class="primary" (click)="start()" data-testid="new-coupon">Yeni kupon</button>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      @if (draft(); as coupon) {
        <form (ngSubmit)="save()" class="form-card" data-testid="coupon-form">
          <h3>{{ coupon.id ? 'Kuponu düzenle' : 'Yeni kupon' }}</h3>
          <div class="form-grid">
            <label>Kod<input [(ngModel)]="coupon.code" name="couponCode" required maxlength="30" [disabled]="!!coupon.id" placeholder="OKUL-25" />
              <span class="muted">Harf, rakam, tire. Siparişlerde bu kod yazar; sonradan değişmez.</span></label>
            <label>İndirim türü
              <select [(ngModel)]="coupon.kind" name="couponKind"><option value="Percent">Yüzde</option><option value="Amount">Tutar (TL)</option></select>
            </label>
            <label>{{ coupon.kind === 'Percent' ? 'Yüzde (1–100)' : 'Tutar (TL)' }}<input type="number" [(ngModel)]="coupon.value" name="couponValue" min="0.01" step="0.01" required /></label>
            <label>Toplam kullanım sınırı<input type="number" [(ngModel)]="coupon.maxUses" name="couponMaxUses" min="1" placeholder="Sınırsız" /></label>
            <label>Başlangıç günü<input type="date" [(ngModel)]="coupon.startsOn" name="couponStartsOn" /></label>
            <label>Son gün (dahil)<input type="date" [(ngModel)]="coupon.endsOn" name="couponEndsOn" /></label>
            <fieldset class="wide plans">
              <legend>Geçerli planlar <span class="muted">(hiçbiri seçilmezse bütün planlarda geçerlidir)</span></legend>
              @for (plan of plans(); track plan.id) {
                <label class="check"><input type="checkbox" [(ngModel)]="coupon.planIds[plan.id]" [name]="'couponPlan-' + plan.id" /> {{ plan.name }}</label>
              } @empty { <span class="muted">Plan yok.</span> }
            </fieldset>
            <label class="check"><input type="checkbox" [(ngModel)]="coupon.isActive" name="couponActive" /> Etkin</label>
          </div>
          <div class="form-actions"><button type="button" class="secondary" (click)="draft.set(null)">Vazgeç</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      <div class="data-card overflow-x-auto">
        <table class="data-table">
          <thead><tr><th>Kod</th><th>İndirim</th><th>Geçerlilik</th><th>Kullanım</th><th>Planlar</th><th>Durum</th><th></th></tr></thead>
          <tbody>
            @for (coupon of coupons(); track coupon.id) {
              <tr>
                <td class="font-mono"><strong>{{ coupon.code }}</strong></td>
                <td>{{ coupon.kind === 'Percent' ? '%' + coupon.value : (coupon.value | number:'1.2-2') + ' TL' }}</td>
                <td>{{ validity(coupon.startsAt ? (coupon.startsAt | date:'dd.MM.yyyy') : null, coupon.endsAt ? (lastDay(coupon.endsAt) | date:'dd.MM.yyyy') : null) }}</td>
                <td>
                  <span class="usage">{{ coupon.usedCount }}{{ coupon.maxUses ? ' / ' + coupon.maxUses : '' }}
                    @if (coupon.maxUses) { <admin-meter [value]="coupon.usedCount" [max]="coupon.maxUses" [tone]="coupon.usedCount >= coupon.maxUses ? 'danger' : 'honey'"
                      [label]="'Kullanım: ' + coupon.usedCount + ' / ' + coupon.maxUses" /> }
                  </span>
                </td>
                <td>{{ planNames(coupon) }}</td>
                <td><span class="badge" [class.on]="coupon.isActive" [class.off]="!coupon.isActive">{{ coupon.isActive ? 'Etkin' : 'Kapalı' }}</span></td>
                <td class="actions"><button type="button" (click)="start(coupon)">Düzenle</button></td>
              </tr>
            } @empty { <tr><td colspan="7" class="empty">{{ loading() ? 'Yükleniyor…' : 'Kupon yok.' }}</td></tr> }
          </tbody>
        </table>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .plans { border: 1px solid var(--ui-border); border-radius: .5rem; padding: .75rem; display: grid; gap: .35rem; }
    .plans legend { padding: 0 .25rem; font-size: .875rem; color: var(--ui-text); }
  `]
})
export class BillingCouponsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly coupons = signal<BillingCoupon[]>([]);
  readonly plans = signal<BillingPlan[]>([]);
  readonly draft = signal<CouponDraft | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({ coupons: this.billing.getCoupons(), plans: this.billing.getPlans() }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ coupons, plans }) => { this.coupons.set(coupons); this.plans.set(plans); },
      error: () => this.error.set('Kuponlar yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  planNames(coupon: BillingCoupon): string {
    return coupon.planIds.length === 0 ? 'Bütün planlar'
      : coupon.planIds.map(id => this.plans().find(plan => plan.id === id)?.name ?? '—').join(', ');
  }

  /** The server keeps the first moment the code no longer works; the admin thinks in "last day". */
  lastDay(endsAt: string): Date { return new Date(new Date(endsAt).getTime() - 1); }
  /** "Süresiz", "20.11.2026'dan itibaren", "31.12.2026'ya kadar" or a range (it read "— – süresiz"). */
  validity(from: string | null, to: string | null): string {
    if (from && to) return `${from} – ${to}`;
    if (to) return `${to} tarihine kadar`;
    if (from) return `${from} tarihinden itibaren`;
    return 'Süresiz';
  }

  start(coupon?: BillingCoupon): void {
    const day = (value: Date) => `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
    this.draft.set({
      id: coupon?.id ?? null,
      code: coupon?.code ?? '',
      kind: coupon?.kind ?? 'Percent',
      value: coupon?.value ?? 10,
      startsOn: coupon?.startsAt ? day(new Date(coupon.startsAt)) : '',
      endsOn: coupon?.endsAt ? day(this.lastDay(coupon.endsAt)) : '',
      maxUses: coupon?.maxUses ?? null,
      planIds: Object.fromEntries((coupon?.planIds ?? []).map(id => [id, true])),
      isActive: coupon?.isActive ?? true
    });
  }

  /** Days are the admin's local days: a code works from the start of its first day to the end of its last. */
  toRequest(coupon: CouponDraft) {
    const local = (value: string, addDays: number) => {
      const [year, month, date] = value.split('-').map(Number);
      return new Date(year, month - 1, date + addDays).toISOString();
    };
    return {
      code: coupon.code.trim(),
      kind: coupon.kind,
      value: Number(coupon.value) || 0,
      startsAt: coupon.startsOn ? local(coupon.startsOn, 0) : null,
      endsAt: coupon.endsOn ? local(coupon.endsOn, 1) : null,
      maxUses: coupon.maxUses === null || (coupon.maxUses as unknown) === '' ? null : Number(coupon.maxUses),
      planIds: Object.entries(coupon.planIds).filter(([, chosen]) => chosen).map(([id]) => id),
      isActive: coupon.isActive
    };
  }

  save(): void {
    const coupon = this.draft();
    if (!coupon) return;
    const request = this.toRequest(coupon);
    this.saving.set(true);
    this.error.set('');
    (coupon.id ? this.billing.updateCoupon(coupon.id, request) : this.billing.createCoupon(request)).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.draft.set(null); this.toaster.success('Kupon kaydedildi.'); this.load(); },
      error: error => this.error.set(billingErrorMessage(error, 'Kupon kaydedilemedi.'))
    });
  }
}
