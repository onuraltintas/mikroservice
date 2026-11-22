import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  BankTransferApproval,
  BillingAdminService,
  BillingEntitlement,
  BillingProduct,
  billingErrorMessage
} from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

interface ProductDraft { id: string | null; code: string; name: string; isActive: boolean; sortOrder: number; }
interface EntitlementDraft { id: string | null; productId: string; productCode: string; key: string; name: string; hasLimit: boolean; }
interface SettingsDraft { requireSubscription: boolean; bankTransferApproval: BankTransferApproval; businessSales: boolean; }

/**
 * "Abonelik ve ödeme › Ürünler ve haklar": what is sold, the access keys each product understands, and the two
 * settings every product has on its own: whether a subscription is needed, and how its bank transfers are approved.
 */
@Component({
  selector: 'app-billing-products',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-products-title">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve ödeme</p>
          <h1 id="billing-products-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Ürünler ve haklar</h1>
          <p class="mt-2 text-sm text-(--ui-text-muted)">Her ürünün abonelik isteyip istemediğini ve havale/EFT ödemelerinin nasıl onaylanacağını buradan belirlersiniz. Haklar, planların kullanıcıya açtığı erişimlerdir.</p>
        </div>
        <button type="button" class="primary" (click)="startProduct()" data-testid="new-product">Yeni ürün</button>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      @if (productDraft(); as draft) {
        <form (ngSubmit)="saveProduct()" class="form-card" data-testid="product-form">
          <h3>{{ draft.id ? 'Ürünü düzenle' : 'Yeni ürün' }}</h3>
          <div class="form-grid">
            <label>Ürün adı<input [(ngModel)]="draft.name" name="productName" required maxlength="200" /></label>
            <label>Ürün kodu<input [(ngModel)]="draft.code" name="productCode" required maxlength="40" [disabled]="!!draft.id" placeholder="ornek-urun" />
              <span class="muted">Küçük harf, rakam ve tire. Ürün servisi bu kodla sorar; sonradan değişmez.</span></label>
            <label>Sıra<input type="number" [(ngModel)]="draft.sortOrder" name="productSortOrder" min="0" max="10000" /></label>
            <label class="check"><input type="checkbox" [(ngModel)]="draft.isActive" name="productActive" /> Etkin</label>
          </div>
          <div class="form-actions"><button type="button" class="secondary" (click)="productDraft.set(null)">Vazgeç</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      @if (loading()) { <p class="muted" role="status">Yükleniyor…</p> }

      @for (product of products(); track product.id) {
        <section class="data-card" [attr.aria-labelledby]="'product-' + product.id" [attr.data-testid]="'product-' + product.code">
          <div class="product-head">
            <div>
              <h2 [id]="'product-' + product.id" class="text-lg font-semibold text-(--ui-text)">{{ product.name }}</h2>
              <p class="muted"><span class="font-mono">{{ product.code }}</span>, <span class="badge" [class.on]="product.isActive" [class.off]="!product.isActive">{{ product.isActive ? 'Etkin' : 'Kapalı' }}</span></p>
            </div>
            <button type="button" class="secondary" (click)="startProduct(product)">Adı ve sırayı düzenle</button>
          </div>

          @if (settingsDrafts()[product.id]; as settings) {
            <form (ngSubmit)="saveSettings(product)" [attr.data-testid]="'settings-' + product.code">
              <div class="settings-row">
                <fieldset>
                  <legend>Abonelik</legend>
                  <label class="check"><input type="checkbox" role="switch" [(ngModel)]="settings.requireSubscription" [name]="'require-' + product.id" [attr.aria-checked]="settings.requireSubscription" /> Bu üründe abonelik iste</label>
                  <p class="muted mt-2">{{ settings.requireSubscription ? 'Açık: ürünü kullanmak için geçerli bir hak gerekir.' : 'Kapalı: hesabı olan herkes ürünü abonelik olmadan kullanır; satın alma sayfası gizlenir.' }}</p>
                </fieldset>
                <fieldset>
                  <legend>Havale/EFT onayı</legend>
                  <label class="check"><input type="radio" [(ngModel)]="settings.bankTransferApproval" [name]="'approval-' + product.id" value="Manual" /> Elle: her ödemeyi yönetici onaylar</label>
                  <label class="check mt-1"><input type="radio" [(ngModel)]="settings.bankTransferApproval" [name]="'approval-' + product.id" value="Automatic" /> Otomatik: banka hareketiyle tam eşleşen ödeme kendiliğinden onaylanır</label>
                  <p class="muted mt-2">Otomatik onay yalnız ödeme kodu ve tutar birebir tuttuğunda çalışır; eşleşmeyenler yine kuyruğa düşer.</p>
                </fieldset>
                <fieldset>
                  <legend>Satış türü</legend>
                  <label class="check"><input type="radio" [(ngModel)]="settings.businessSales" [name]="'sales-' + product.id" [value]="false" /> Tüketiciye satış: ön bilgilendirme formu, mesafeli satış sözleşmesi ve 14 gün cayma</label>
                  <label class="check mt-1"><input type="radio" [(ngModel)]="settings.businessSales" [name]="'sales-' + product.id" [value]="true" /> Kurumsal satış: kurumsal abonelik sözleşmesi, cayma ve iade yok</label>
                </fieldset>
              </div>
              <div class="form-actions">
                <span class="muted">Kayıtlı: {{ product.requireSubscription ? 'abonelik isteniyor' : 'abonelik istenmiyor' }}, {{ product.bankTransferApproval === 'Automatic' ? 'otomatik onay' : 'elle onay' }}, {{ product.businessSales ? 'kurumsal satış' : 'tüketiciye satış' }}</span>
                <button type="submit" class="primary" [disabled]="saving() || !settingsChanged(product)">Ayarları kaydet</button>
              </div>
            </form>
          }

          <div class="mt-4 flex items-center justify-between gap-3">
            <h3 class="font-semibold text-(--ui-text)">Haklar</h3>
            <button type="button" class="secondary" (click)="startEntitlement(product)">Hak ekle</button>
          </div>
          @if (entitlementDraft(); as draft) {
            @if (draft.productId === product.id) {
              <form (ngSubmit)="saveEntitlement()" class="form-card mt-3" data-testid="entitlement-form">
                <h3>{{ draft.id ? 'Hakkı düzenle' : 'Yeni hak' }}</h3>
                <div class="form-grid">
                  <label>Hak adı<input [(ngModel)]="draft.name" name="entitlementName" required maxlength="200" /></label>
                  <label>Anahtar<input [(ngModel)]="draft.key" name="entitlementKey" required maxlength="100" [disabled]="!!draft.id" />
                    <span class="muted">"{{ draft.productCode }}." ile başlar. Ürün bu anahtarı arar; sonradan değişmez.</span></label>
                  <label class="check wide"><input type="checkbox" [(ngModel)]="draft.hasLimit" name="entitlementHasLimit" /> Sayısal sınır taşır (örneğin etkin öğrenci sınırı); sayı planda yazılır</label>
                </div>
                <div class="form-actions"><button type="button" class="secondary" (click)="entitlementDraft.set(null)">Vazgeç</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
              </form>
            }
          }
          <div class="overflow-x-auto mt-3">
            <table class="data-table">
              <thead><tr><th>Hak</th><th>Anahtar</th><th>Sınır</th><th></th></tr></thead>
              <tbody>
                @for (entitlement of product.entitlements; track entitlement.id) {
                  <tr>
                    <td>{{ entitlement.name }}</td>
                    <td class="font-mono">{{ entitlement.key }}</td>
                    <td>{{ entitlement.hasLimit ? 'Var' : 'Yok' }}</td>
                    <td class="actions">
                      @if (removingId() === entitlement.id) {
                        <span class="muted">Silinsin mi?</span>
                        <button type="button" class="danger" (click)="removeEntitlement(entitlement)" [disabled]="saving()">Evet, sil</button>
                        <button type="button" (click)="removingId.set(null)">Vazgeç</button>
                      } @else {
                        <button type="button" (click)="startEntitlement(product, entitlement)">Düzenle</button>
                        <button type="button" class="danger" (click)="removingId.set(entitlement.id)">Sil</button>
                      }
                    </td>
                  </tr>
                } @empty { <tr><td colspan="4" class="empty">Bu ürünün hakkı yok. Plan tanımlamak için en az bir hak ekleyin.</td></tr> }
              </tbody>
            </table>
          </div>
        </section>
      } @empty {
        @if (!loading() && !error()) { <p class="empty">Ürün bulunamadı.</p> }
      }
    </main>
  `,
  styles: [BILLING_PAGE_STYLES]
})
export class BillingProductsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly products = signal<BillingProduct[]>([]);
  readonly settingsDrafts = signal<Record<string, SettingsDraft>>({});
  readonly productDraft = signal<ProductDraft | null>(null);
  readonly entitlementDraft = signal<EntitlementDraft | null>(null);
  readonly removingId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.billing.getProducts().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: products => this.apply(products),
      error: () => this.error.set('Ürünler yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
    });
  }

  settingsChanged(product: BillingProduct): boolean {
    const draft = this.settingsDrafts()[product.id];
    return !!draft && (draft.requireSubscription !== product.requireSubscription || draft.bankTransferApproval !== product.bankTransferApproval
      || draft.businessSales !== !!product.businessSales);
  }

  saveSettings(product: BillingProduct): void {
    const draft = this.settingsDrafts()[product.id];
    if (!draft) return;
    this.save(this.billing.updateProductSettings(product.id, { ...draft }), updated => {
      this.replace(updated);
      this.toaster.success(`${updated.name} ayarları kaydedildi.`);
    }, 'Ürün ayarları kaydedilemedi.');
  }

  startProduct(product?: BillingProduct): void {
    this.productDraft.set(product
      ? { id: product.id, code: product.code, name: product.name, isActive: product.isActive, sortOrder: product.sortOrder }
      : { id: null, code: '', name: '', isActive: true, sortOrder: this.products().length + 1 });
  }

  saveProduct(): void {
    const draft = this.productDraft();
    if (!draft) return;
    const request = { code: draft.code.trim(), name: draft.name.trim(), isActive: draft.isActive, sortOrder: Number(draft.sortOrder) || 0 };
    this.save(draft.id ? this.billing.updateProduct(draft.id, request) : this.billing.createProduct(request), () => {
      this.productDraft.set(null);
      this.toaster.success('Ürün kaydedildi.');
      this.load();
    }, 'Ürün kaydedilemedi.');
  }

  startEntitlement(product: BillingProduct, entitlement?: BillingEntitlement): void {
    this.removingId.set(null);
    this.entitlementDraft.set(entitlement
      ? { id: entitlement.id, productId: product.id, productCode: product.code, key: entitlement.key, name: entitlement.name, hasLimit: entitlement.hasLimit }
      : { id: null, productId: product.id, productCode: product.code, key: `${product.code}.`, name: '', hasLimit: false });
  }

  saveEntitlement(): void {
    const draft = this.entitlementDraft();
    if (!draft) return;
    const request = { key: draft.key.trim(), name: draft.name.trim(), hasLimit: draft.hasLimit };
    this.save(draft.id ? this.billing.updateEntitlement(draft.id, request) : this.billing.addEntitlement(draft.productId, request), updated => {
      this.entitlementDraft.set(null);
      this.replace(updated);
      this.toaster.success('Hak kaydedildi.');
    }, 'Hak kaydedilemedi.');
  }

  removeEntitlement(entitlement: BillingEntitlement): void {
    this.save(this.billing.removeEntitlement(entitlement.id), () => {
      this.removingId.set(null);
      this.toaster.success('Hak silindi.');
      this.load();
    }, 'Hak silinemedi.');
  }

  private save<T>(request: Observable<T>, done: (value: T) => void, fallback: string): void {
    this.saving.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: done,
      error: error => this.error.set(billingErrorMessage(error, fallback))
    });
  }

  private replace(product: BillingProduct): void {
    this.apply(this.products().map(item => item.id === product.id ? product : item));
  }

  private apply(products: BillingProduct[]): void {
    this.products.set(products);
    this.settingsDrafts.set(Object.fromEntries(products.map(product =>
      [product.id, { requireSubscription: product.requireSubscription, bankTransferApproval: product.bankTransferApproval, businessSales: !!product.businessSales }])));
  }
}
