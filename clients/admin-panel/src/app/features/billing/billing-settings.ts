import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize, forkJoin } from 'rxjs';
import {
  BillingAdminService,
  BillingBankAccount,
  BillingSeller,
  BillingSellerRequest,
  billingErrorMessage
} from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

interface BankAccountDraft {
  id: string | null;
  bankName: string;
  accountHolder: string;
  iban: string;
  instructions: string;
  statementTemplate: string;
  isActive: boolean;
  sortOrder: number;
}

const EMPTY_SELLER: BillingSellerRequest = { title: '', address: '', taxOffice: '', taxNumber: '', mersisNumber: '', email: '', phone: '', alertEmail: '' };

/** "Abonelik ve Ödeme › Ayarlar": the seller named in the sales documents and the bank accounts buyers pay into. */
@Component({
  selector: 'app-billing-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-settings-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve Ödeme</p>
        <h1 id="billing-settings-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Ayarlar</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Satıcı bilgileri ve banka hesapları bütün ürünler için ortaktır. Satıcı bilgileri eksikken ya da etkin banka hesabı yokken hiçbir üründe satın alma açılmaz.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }
      @if (loading()) { <p class="muted" role="status">Yükleniyor…</p> }

      <form (ngSubmit)="saveSeller()" class="form-card" data-testid="seller-form" aria-labelledby="seller-title">
        <h3 id="seller-title">Satıcı bilgileri</h3>
        <p class="muted mb-3">Ön bilgilendirme formunun ve mesafeli satış sözleşmesinin başında yer alır. MERSİS dışındaki alanlar zorunludur.</p>
        <div class="form-grid">
          <label class="wide">Unvan<input [(ngModel)]="sellerDraft.title" name="sellerTitle" maxlength="200" autocomplete="organization" /></label>
          <label class="wide">Adres<input [(ngModel)]="sellerDraft.address" name="sellerAddress" maxlength="500" /></label>
          <label>Vergi dairesi<input [(ngModel)]="sellerDraft.taxOffice" name="sellerTaxOffice" maxlength="100" /></label>
          <label>Vergi numarası<input [(ngModel)]="sellerDraft.taxNumber" name="sellerTaxNumber" maxlength="11" inputmode="numeric" /></label>
          <label>MERSİS numarası <span class="muted">(isteğe bağlı)</span><input [(ngModel)]="sellerDraft.mersisNumber" name="sellerMersis" maxlength="16" inputmode="numeric" /></label>
          <label>E-posta<input type="email" [(ngModel)]="sellerDraft.email" name="sellerEmail" maxlength="320" /></label>
          <label>Telefon<input [(ngModel)]="sellerDraft.phone" name="sellerPhone" maxlength="40" /></label>
          <label>Uyarı e-postası<input type="email" [(ngModel)]="sellerDraft.alertEmail" name="sellerAlertEmail" maxlength="320" />
            <span class="muted">Yeni ödeme bildirimi ve cayma talebi bu adrese haber verilir. Boşsa e-posta gitmez.</span></label>
        </div>
        <div class="form-actions">
          <span class="badge" [class.on]="seller()?.isComplete" [class.off]="!seller()?.isComplete" data-testid="seller-state">{{ seller()?.isComplete ? 'Satış belgeleri hazır' : 'Eksik: satış belgeleri oluşturulamaz' }}</span>
          <button type="submit" class="primary" [disabled]="saving() || loading()">Satıcı bilgilerini kaydet</button>
        </div>
      </form>

      <section class="space-y-3" aria-labelledby="bank-accounts-title">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 id="bank-accounts-title" class="text-lg font-semibold text-(--ui-text)">Banka hesapları</h2>
            <p class="muted">Etkin hesapların hepsi ödeme sayfasında görünür. Kullanıcı hangisine gönderirse göndersin, ödeme kodu aynıdır.</p>
          </div>
          <button type="button" class="primary" (click)="startAccount()" data-testid="new-bank-account">Hesap ekle</button>
        </div>

        @if (accountDraft(); as draft) {
          <form (ngSubmit)="saveAccount()" class="form-card" data-testid="bank-account-form">
            <h3>{{ draft.id ? 'Hesabı düzenle' : 'Yeni banka hesabı' }}</h3>
            <div class="form-grid">
              <label>Banka adı<input [(ngModel)]="draft.bankName" name="accountBankName" required maxlength="200" /></label>
              <label>Hesap sahibi<input [(ngModel)]="draft.accountHolder" name="accountHolder" required maxlength="200" autocomplete="organization" /></label>
              <label class="wide">IBAN<input [(ngModel)]="draft.iban" name="accountIban" required maxlength="34" placeholder="TR00 0000 0000 0000 0000 0000 00" autocomplete="off" /></label>
              <label class="wide">Ödeme sayfasında gösterilecek açıklama<textarea [(ngModel)]="draft.instructions" name="accountInstructions" maxlength="2000"></textarea></label>
              <label>Ekstre şablonu
                <select [(ngModel)]="draft.statementTemplate" name="accountStatementTemplate">
                  <option value="">Sütunları elle eşleştir</option>
                  <option value="Halkbank">Halkbank</option>
                </select>
                <span class="muted">Banka ekstresi yüklerken kullanılır.</span>
              </label>
              <label>Sıra<input type="number" [(ngModel)]="draft.sortOrder" name="accountSortOrder" min="0" max="10000" /></label>
              <label class="check"><input type="checkbox" [(ngModel)]="draft.isActive" name="accountActive" /> Ödeme sayfasında göster</label>
            </div>
            <div class="form-actions"><button type="button" class="secondary" (click)="accountDraft.set(null)">Vazgeç</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
          </form>
        }

        <div class="data-card">
          <div class="overflow-x-auto">
            <table class="data-table">
              <thead><tr><th>Banka</th><th>Hesap sahibi</th><th>IBAN</th><th>Ekstre şablonu</th><th>Durum</th><th></th></tr></thead>
              <tbody>
                @for (account of accounts(); track account.id) {
                  <tr>
                    <td><strong>{{ account.bankName }}</strong></td>
                    <td>{{ account.accountHolder }}</td>
                    <td class="font-mono">{{ account.ibanFormatted }}</td>
                    <td>{{ account.statementTemplate || 'Elle eşleştirme' }}</td>
                    <td><span class="badge" [class.on]="account.isActive" [class.off]="!account.isActive">{{ account.isActive ? 'Yayında' : 'Kapalı' }}</span></td>
                    <td class="actions">
                      @if (removingId() === account.id) {
                        <span class="muted">Silinsin mi?</span>
                        <button type="button" class="danger" (click)="removeAccount(account)" [disabled]="saving()">Evet, sil</button>
                        <button type="button" (click)="removingId.set(null)">Vazgeç</button>
                      } @else {
                        <button type="button" (click)="startAccount(account)">Düzenle</button>
                        <button type="button" class="danger" (click)="removingId.set(account.id)">Sil</button>
                      }
                    </td>
                  </tr>
                } @empty { <tr><td colspan="6" class="empty">{{ loading() ? 'Yükleniyor…' : 'Banka hesabı yok. Havale/EFT ile satış için en az bir hesap ekleyin.' }}</td></tr> }
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES]
})
export class BillingSettingsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly seller = signal<BillingSeller | null>(null);
  readonly accounts = signal<BillingBankAccount[]>([]);
  readonly accountDraft = signal<BankAccountDraft | null>(null);
  readonly removingId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  sellerDraft: BillingSellerRequest = { ...EMPTY_SELLER };

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({ seller: this.billing.getSeller(), accounts: this.billing.getBankAccounts() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ seller, accounts }) => { this.applySeller(seller); this.accounts.set(accounts); },
        error: () => this.error.set('Ayarlar yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
      });
  }

  saveSeller(): void {
    const text = (value: string | null) => value?.trim() || null;
    const draft = this.sellerDraft;
    this.save(this.billing.updateSeller({
      title: text(draft.title), address: text(draft.address), taxOffice: text(draft.taxOffice), taxNumber: text(draft.taxNumber),
      mersisNumber: text(draft.mersisNumber), email: text(draft.email), phone: text(draft.phone), alertEmail: text(draft.alertEmail)
    }), seller => {
      this.applySeller(seller);
      this.toaster.success('Satıcı bilgileri kaydedildi.');
    }, 'Satıcı bilgileri kaydedilemedi.');
  }

  startAccount(account?: BillingBankAccount): void {
    this.removingId.set(null);
    this.accountDraft.set(account
      ? { id: account.id, bankName: account.bankName, accountHolder: account.accountHolder, iban: account.ibanFormatted,
          instructions: account.instructions ?? '', statementTemplate: account.statementTemplate ?? '', isActive: account.isActive, sortOrder: account.sortOrder }
      : { id: null, bankName: '', accountHolder: this.seller()?.title ?? '', iban: '', instructions: '', statementTemplate: '', isActive: true, sortOrder: this.accounts().length + 1 });
  }

  saveAccount(): void {
    const draft = this.accountDraft();
    if (!draft) return;
    const request = {
      bankName: draft.bankName.trim(), accountHolder: draft.accountHolder.trim(), iban: draft.iban.trim(),
      instructions: draft.instructions.trim() || null, statementTemplate: draft.statementTemplate || null,
      isActive: draft.isActive, sortOrder: Number(draft.sortOrder) || 0
    };
    this.save(draft.id ? this.billing.updateBankAccount(draft.id, request) : this.billing.createBankAccount(request), () => {
      this.accountDraft.set(null);
      this.toaster.success('Banka hesabı kaydedildi.');
      this.reloadAccounts();
    }, 'Banka hesabı kaydedilemedi.');
  }

  removeAccount(account: BillingBankAccount): void {
    this.save(this.billing.removeBankAccount(account.id), () => {
      this.removingId.set(null);
      this.toaster.success('Banka hesabı silindi.');
      this.reloadAccounts();
    }, 'Banka hesabı silinemedi.');
  }

  private reloadAccounts(): void {
    this.billing.getBankAccounts().subscribe({ next: accounts => this.accounts.set(accounts), error: () => this.error.set('Banka hesapları yüklenemedi. Sayfayı yenileyip yeniden deneyin.') });
  }

  private applySeller(seller: BillingSeller): void {
    this.seller.set(seller);
    this.sellerDraft = {
      title: seller.title ?? '', address: seller.address ?? '', taxOffice: seller.taxOffice ?? '', taxNumber: seller.taxNumber ?? '',
      mersisNumber: seller.mersisNumber ?? '', email: seller.email ?? '', phone: seller.phone ?? '', alertEmail: seller.alertEmail ?? ''
    };
  }

  private save<T>(request: Observable<T>, done: (value: T) => void, fallback: string): void {
    this.saving.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: done,
      error: error => this.error.set(billingErrorMessage(error, fallback))
    });
  }
}
