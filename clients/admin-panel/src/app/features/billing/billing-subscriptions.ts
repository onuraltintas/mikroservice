import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { BillingAdminService, BillingAdminSubscription, BillingPlan, BillingProduct, BillingSeatPool, billingErrorMessage } from '../../core/services/billing-admin.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

/** "Abonelik ve Ödeme › Abonelikler": who has access through which plan until when, in every product. */
@Component({
  selector: 'app-billing-subscriptions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-subscriptions-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
        <h1 id="billing-subscriptions-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Abonelikler</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Onaylanan ödemelerin, ücretsiz denemelerin ve elle verilenlerin açtığı abonelikler. Bitiş tarihi, onaylı dönemlerin toplamıdır; yenileme mevcut dönemin bittiği gün başlar.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      @if (granting()) {
        <form class="form-card" (ngSubmit)="grant()" data-testid="grant-form">
          <h3>Elle abonelik ver</h3>
          <p class="muted mb-3">Ödeme alınmadan verilen abonelik (tanıtım, sözleşmeyle satış, telafi). Aynı planın süren aboneliği varsa süre onun sonuna eklenir. Ödeme kaydı oluşmaz.</p>
          <div class="form-grid">
            <label>Plan
              <select [(ngModel)]="form.planId" name="grantPlan" required>
                <option value="">Plan seçin</option>
                @for (plan of plans(); track plan.id) { <option [value]="plan.id">{{ plan.name }} ({{ buyerLabel(plan) }})</option> }
              </select>
            </label>
            <label>{{ grantsInstitution() ? 'Kurum kimliği' : 'Kullanıcı kimliği' }}
              <input [(ngModel)]="form.holderId" name="grantHolder" required placeholder="00000000-0000-0000-0000-000000000000" />
              <span class="muted">{{ grantsInstitution() ? 'Kullanıcı İşlemleri › Kurumlar listesinde kurumun altındaki "Kimliği kopyala".' : 'Kullanıcılar listesinde kullanıcının Detay penceresindeki "Kimlik numarası".' }}</span>
            </label>
            <label>{{ grantsInstitution() ? 'Kurumun adı' : 'Adı ve soyadı' }}
              <input [(ngModel)]="form.displayName" name="grantName" required maxlength="200" />
            </label>
            <label>E-posta (isteğe bağlı; bildirimler buraya gider)
              <input [(ngModel)]="form.email" name="grantEmail" type="email" maxlength="256" />
            </label>
            <label>Süre (gün; boşsa planın süresi)
              <input [(ngModel)]="form.days" name="grantDays" type="number" min="1" max="3650" />
            </label>
            <label class="wide">Gerekçe
              <input [(ngModel)]="form.note" name="grantNote" required maxlength="500" />
            </label>
          </div>
          <div class="form-actions">
            <button type="button" class="secondary" (click)="granting.set(false)">Vazgeç</button>
            <button type="submit" class="primary" [disabled]="saving()">Aboneliği ver</button>
          </div>
        </form>
      } @else {
        <div><button type="button" class="primary" (click)="openGrant()">Elle abonelik ver</button></div>
      }

      <div class="data-card">
        <div class="mb-3 flex flex-wrap items-center gap-2">
          <label class="text-sm text-(--ui-text)" for="subscription-product-filter">Ürün</label>
          <select id="subscription-product-filter" [ngModel]="product()" (ngModelChange)="product.set($event); load()" name="subscriptionProduct">
            <option value="">Tüm ürünler</option>
            @for (item of products(); track item.id) { <option [value]="item.code">{{ item.name }}</option> }
          </select>
        </div>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Hesap</th><th>Plan</th><th>Ürünler</th><th>Başlangıç</th><th>Bitiş</th><th>Koltuk</th><th>Durum</th><th></th></tr></thead>
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
                  <td class="actions">
                    <button type="button" (click)="begin(row, 'extend')">Uzat</button>
                    @if (row.subscription.isCurrent) { <button type="button" class="danger" (click)="begin(row, 'cancel')">Sonlandır</button> }
                    @if (row.subscription.seatCount !== null) {
                    <button type="button" (click)="toggleSeats(row)" [attr.aria-expanded]="pool()?.subscriptionId === row.subscription.id">{{ pool()?.subscriptionId === row.subscription.id ? 'Koltukları gizle' : 'Koltuklar' }}</button>
                  }
                  </td>
                </tr>
                @if (acting(); as act) {
                  @if (act.id === row.subscription.id) {
                    <tr class="seat-row"><td colspan="8" data-testid="manual-action">
                      <form class="flex flex-wrap items-end gap-2" (ngSubmit)="confirm()">
                        @if (act.kind === 'extend') {
                          <label class="text-sm">Eklenecek gün
                            <input [(ngModel)]="actionDays" name="actionDays" type="number" min="1" max="3650" required />
                          </label>
                        } @else {
                          <p class="text-sm warn">Abonelik şimdi sona erer ve erişim kapanır. Ödeme kaydı değişmez; para iadesi için siparişte "onayı geri al" kullanılır.</p>
                        }
                        <label class="text-sm grow">Gerekçe
                          <input [(ngModel)]="actionNote" name="actionNote" required maxlength="500" />
                        </label>
                        <button type="submit" class="primary" [disabled]="saving()">{{ act.kind === 'extend' ? 'Süreyi uzat' : 'Evet, sonlandır' }}</button>
                        <button type="button" class="secondary" (click)="acting.set(null)">Vazgeç</button>
                      </form>
                    </td></tr>
                  }
                }
                @if (pool(); as seats) {
                  @if (seats.subscriptionId === row.subscription.id) {
                    <tr class="seat-row"><td colspan="8" data-testid="seat-pool">
                      <p class="text-sm"><strong>{{ seats.used }} / {{ seats.capacity }}</strong> koltuk dolu@if (seats.extraSeats) { <span> · {{ seats.extraSeats }} ek koltuk, {{ seats.extraSeatsUntil | date:'dd.MM.yyyy' }} tarihine kadar</span> }@if (seats.used > seats.capacity) { <span class="warn"> · {{ seats.used - seats.capacity }} koltuk kapasite dışında: erişimi yok</span> }</p>
                      <p class="muted">Koltukları ürünün kendisi verir (öğretmen ya da kurum yöneticisi, üründeki ekrandan). Buradan yalnız geri alınabilir. Kullanıcılar kimlik numarasıyla görünür.</p>
                      <table class="data-table mt-2">
                        <thead><tr><th>Kullanıcı</th><th>Verildi</th><th>Durum</th><th></th></tr></thead>
                        <tbody>
                          @for (seat of seats.seats; track seat.userId) {
                            <tr>
                              <td class="font-mono">{{ seat.userId }}</td>
                              <td>{{ seat.assignedAt | date:'dd.MM.yyyy HH:mm' }}</td>
                              <td><span class="badge" [class.on]="seat.isCovered" [class.off]="seat.isSuspended">{{ seat.isSuspended ? 'Geri alındı' : seat.isCovered ? 'Erişimi var' : 'Kapasite dışı' }}</span></td>
                              <td class="actions">@if (!seat.isSuspended) {
                                @if (releasing() === seat.userId) {
                                  <span class="muted">Koltuk geri alınsın mı?</span>
                                  <button type="button" class="danger" (click)="release(seats.subscriptionId, seat.userId)" [disabled]="saving()">Evet, geri al</button>
                                  <button type="button" (click)="releasing.set(null)">Vazgeç</button>
                                } @else { <button type="button" class="danger" (click)="releasing.set(seat.userId)">Geri al</button> }
                              }</td>
                            </tr>
                          } @empty { <tr><td colspan="4" class="empty">Henüz koltuk verilmedi.</td></tr> }
                        </tbody>
                      </table>
                    </td></tr>
                  }
                }
              } @empty { <tr><td colspan="8" class="empty">{{ loading() ? 'Yükleniyor…' : 'Abonelik yok.' }}</td></tr> }
            </tbody>
          </table>
        </div>
      </div>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .seat-row > td { background: color-mix(in srgb, var(--ui-brand) 6%, transparent); }
    .warn { color: #b45309; }
    .grow { flex: 1 1 16rem; display: flex; flex-direction: column; gap: .25rem; }
    form label { display: flex; flex-direction: column; gap: .25rem; }
  `]
})
export class BillingSubscriptionsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);

  readonly subscriptions = signal<BillingAdminSubscription[]>([]);
  readonly products = signal<BillingProduct[]>([]);
  readonly product = signal('');
  readonly pool = signal<BillingSeatPool | null>(null);
  readonly releasing = signal<string | null>(null);
  readonly plans = signal<BillingPlan[]>([]);
  readonly granting = signal(false);
  readonly acting = signal<{ id: string; kind: 'extend' | 'cancel' } | null>(null);
  form = { planId: '', holderId: '', displayName: '', email: '', days: null as number | null, note: '' };
  actionDays: number | null = 30;
  actionNote = '';
  readonly saving = signal(false);
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

  openGrant(): void {
    this.form = { planId: '', holderId: '', displayName: '', email: '', days: null, note: '' };
    this.error.set('');
    this.granting.set(true);
    if (this.plans().length === 0) {
      this.billing.getPlans().subscribe({ next: plans => this.plans.set(plans), error: () => this.error.set('Planlar yüklenemedi.') });
    }
  }

  grantsInstitution(): boolean { return this.plans().find(plan => plan.id === this.form.planId)?.buyerType === 'Institution'; }

  buyerLabel(plan: BillingPlan): string {
    return plan.buyerType === 'Institution' ? 'kurum' : plan.buyerType === 'Teacher' ? 'öğretmen' : 'bireysel';
  }

  grant(): void {
    const id = this.form.holderId.trim();
    if (!this.form.planId) { this.error.set('Plan seçin.'); return; }
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) { this.error.set('Kimlik numarası geçersiz.'); return; }
    if (this.form.displayName.trim().length < 3) { this.error.set('Ad en az 3 karakter olmalı.'); return; }
    if (this.form.note.trim().length < 3) { this.error.set('Gerekçe yazın.'); return; }
    const institution = this.grantsInstitution();
    this.saving.set(true);
    this.error.set('');
    this.billing.grantSubscription({
      userId: institution ? null : id, organizationId: institution ? id : null, displayName: this.form.displayName.trim(),
      email: this.form.email.trim() || null, planId: this.form.planId, days: this.form.days || null, note: this.form.note.trim()
    }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.granting.set(false); this.load(); },
      error: error => this.error.set(billingErrorMessage(error, 'Abonelik verilemedi.'))
    });
  }

  begin(row: BillingAdminSubscription, kind: 'extend' | 'cancel'): void {
    this.actionDays = 30;
    this.actionNote = '';
    this.error.set('');
    this.acting.set({ id: row.subscription.id, kind });
  }

  confirm(): void {
    const act = this.acting();
    if (!act) return;
    const note = this.actionNote.trim();
    if (note.length < 3) { this.error.set('Gerekçe yazın.'); return; }
    if (act.kind === 'extend' && !(Number(this.actionDays) >= 1)) { this.error.set('Eklenecek gün en az 1 olmalı.'); return; }
    this.saving.set(true);
    this.error.set('');
    const call = act.kind === 'extend' ? this.billing.extendSubscription(act.id, Math.floor(Number(this.actionDays)), note) : this.billing.cancelSubscription(act.id, note);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.acting.set(null); this.load(); },
      error: error => this.error.set(billingErrorMessage(error, act.kind === 'extend' ? 'Süre uzatılamadı.' : 'Abonelik sonlandırılamadı.'))
    });
  }

  toggleSeats(row: BillingAdminSubscription): void {
    this.releasing.set(null);
    if (this.pool()?.subscriptionId === row.subscription.id) { this.pool.set(null); return; }
    this.billing.getSeats(row.subscription.id).subscribe({ next: pool => this.pool.set(pool), error: () => this.error.set('Koltuklar yüklenemedi.') });
  }

  release(subscriptionId: string, userId: string): void {
    this.saving.set(true);
    this.error.set('');
    this.billing.releaseSeat(subscriptionId, userId).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: pool => { this.pool.set(pool); this.releasing.set(null); },
      error: error => this.error.set(billingErrorMessage(error, 'Koltuk geri alınamadı.'))
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
