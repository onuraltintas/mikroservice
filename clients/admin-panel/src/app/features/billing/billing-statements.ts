import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize, forkJoin } from 'rxjs';
import {
  BANK_TRANSACTION_STATUS_LABELS,
  BankTransaction,
  BankTransactionStatus,
  BillingAdminOrder,
  BillingAdminService,
  BillingBankAccount,
  StatementImport,
  StatementImportResult,
  StatementMapping,
  StatementPreview,
  billingErrorMessage
} from '../../core/services/billing-admin.service';
import { ToasterService } from '../../core/services/toaster.service';
import { BILLING_PAGE_STYLES } from './billing-page.styles';

/**
 * "Abonelik ve ödeme › Banka ekstresi": the admin uploads the bank's export for one account. Lines are stored once;
 * a line whose payment code and amount fit a waiting order is approved by itself where the product is set to automatic
 * approval, and waits for the admin otherwise. Everything else is listed with the reason.
 */
@Component({
  selector: 'app-billing-statements',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6" aria-labelledby="billing-statements-title">
      <header>
        <p class="text-sm font-medium text-(--ui-brand-text)">Abonelik ve ödeme</p>
        <h1 id="billing-statements-title" class="mt-1 text-2xl font-bold text-(--ui-text)">Banka ekstresi</h1>
        <p class="mt-2 text-sm text-(--ui-text-muted)">Bankadan indirdiğiniz hesap ekstresini (CSV ya da .xlsx) yükleyin. Açıklamasında ödeme kodu olan ve tutarı siparişle birebir tutan ödemeler eşleşir; ürünü "otomatik onay"da olanlar kendiliğinden onaylanır. Aynı ekstreyi yeniden yüklemek hiçbir satırı iki kez eklemez.</p>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-(--ui-danger-soft) bg-(--ui-danger-soft) p-4 text-sm text-(--ui-danger)">{{ error() }}</div> }

      <section class="form-card" aria-labelledby="upload-title">
        <h3 id="upload-title">Ekstre yükle</h3>
        <div class="form-grid">
          <label>Banka hesabı
            <select [ngModel]="bankAccountId()" (ngModelChange)="bankAccountId.set($event)" name="statementAccount" data-testid="statement-account">
              <option value="">Hesap seçin</option>
              @for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.bankName }}, {{ account.ibanFormatted }}</option> }
            </select>
          </label>
          <label>Ekstre dosyası
            <input type="file" accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" (change)="choose($event)" data-testid="statement-file" />
            <span class="muted">En çok 5 MB. Eski Excel (.xls) okunmaz; CSV olarak indirin.</span>
          </label>
        </div>

        @if (preview(); as shown) {
          @if (mapping(); as columns) {
            <p class="mt-4 text-sm" [class.recognised]="shown.suggested" data-testid="mapping-state">
              {{ shown.suggested ? 'Sütunlar tanındı. Aşağıdaki önizlemeyi denetleyip yükleyin.' : 'Sütunlar tanınamadı. Başlık satırını ve sütunları seçin.' }}
            </p>
            <div class="form-grid mt-3">
              <label>Başlık satırı
                <select [(ngModel)]="columns.headerRow" name="mapHeaderRow">
                  @for (row of shown.rows; track $index) { <option [ngValue]="$index">{{ $index + 1 }}. satır: {{ rowLabel(row) }}</option> }
                </select>
              </label>
              <label>Tarih sütunu<select [(ngModel)]="columns.dateColumn" name="mapDate">@for (name of headers(); track $index) { <option [ngValue]="$index">{{ name }}</option> }</select></label>
              <label>Açıklama sütunu<select [(ngModel)]="columns.descriptionColumn" name="mapDescription">@for (name of headers(); track $index) { <option [ngValue]="$index">{{ name }}</option> }</select></label>
              <label>Tutar sütunu<select [(ngModel)]="columns.amountColumn" name="mapAmount">@for (name of headers(); track $index) { <option [ngValue]="$index">{{ name }}</option> }</select></label>
              <label>Borç/alacak sütunu
                <select [(ngModel)]="columns.directionColumn" name="mapDirection">
                  <option [ngValue]="null">Yok (tutar artı ise gelen ödemedir)</option>
                  @for (name of headers(); track $index) { <option [ngValue]="$index">{{ name }}</option> }
                </select>
              </label>
              <label>Gelen ödemeyi gösteren değer<input [(ngModel)]="columns.incomingValue" name="mapIncoming" maxlength="20" [disabled]="columns.directionColumn === null" placeholder="A" />
                <span class="muted">Halkbank'ta "A" (alacak).</span></label>
            </div>
            <div class="overflow-x-auto mt-3">
              <table class="data-table preview">
                <thead><tr>@for (name of headers(); track $index) { <th [class.picked]="isPicked($index)">{{ name }}</th> }</tr></thead>
                <tbody>
                  @for (row of sampleRows(); track $index) {
                    <tr>@for (name of headers(); track $index) { <td [class.picked]="isPicked($index)">{{ row[$index] }}</td> }</tr>
                  } @empty { <tr><td class="empty" [attr.colspan]="headers().length || 1">Başlık satırının altında satır yok.</td></tr> }
                </tbody>
              </table>
            </div>
            <div class="form-actions">
              <span class="muted">{{ shown.totalRows }} satır okundu</span>
              <button type="button" class="primary" (click)="upload()" [disabled]="saving() || !bankAccountId()" data-testid="statement-upload">Yükle ve eşleştir</button>
            </div>
          }
        }
      </section>

      @if (result(); as done) {
        <section class="data-card" role="status" data-testid="import-result">
          <h3 class="font-semibold text-(--ui-text)">{{ done.import.fileName }} yüklendi</h3>
          <p class="mt-1 text-sm text-(--ui-text)">
            {{ done.import.rowCount }} gelen ödeme satırı: <strong>{{ done.import.addedCount }} yeni</strong>, {{ done.import.duplicateCount }} daha önce yüklenmiş.
            Yeni satırlardan <strong>{{ done.import.autoApprovedCount }}</strong> kendiliğinden onaylandı, <strong>{{ done.import.matchedCount }}</strong> onayınızı bekliyor,
            <strong>{{ unmatchedCount(done) }}</strong> eşleşmedi.
          </p>
        </section>
      }

      <section class="data-card" aria-labelledby="lines-title">
        <div class="mb-3 flex flex-wrap items-end justify-between gap-3">
          <h2 id="lines-title" class="text-lg font-semibold text-(--ui-text)">Banka hareketleri</h2>
          <label class="filter">Durum
            <select [ngModel]="status()" (ngModelChange)="status.set($event); loadLines()" name="lineStatus">
              <option value="">Tümü</option>
              @for (option of statuses; track option) { <option [value]="option">{{ statusLabel(option) }}</option> }
            </select>
          </label>
        </div>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Tarih</th><th>Banka</th><th>Tutar</th><th>Açıklama</th><th>Durum</th><th></th></tr></thead>
            <tbody>
              @for (line of lines(); track line.id) {
                <tr [attr.data-testid]="'line-' + line.id">
                  <td class="nowrap">{{ line.bookedAt | date:'dd.MM.yyyy' }}</td>
                  <td>{{ line.bankName }}</td>
                  <td class="nowrap"><strong>{{ line.amount | number:'1.2-2' }}</strong></td>
                  <td class="description">{{ line.description }}@if (line.paymentCode) { <div class="muted">Kod: <span class="font-mono">{{ line.paymentCode }}</span></div> }</td>
                  <td><span class="badge" [class.on]="line.status === 'Approved'" [class.off]="line.status === 'Ignored'">{{ statusLabel(line.status) }}</span>
                    @if (line.orderSummary) { <div class="muted">{{ line.orderSummary }}</div> }
                    @if (line.note) { <div class="muted">{{ line.note }}</div> }</td>
                  <td class="actions">
                    @if (line.status === 'Matched' && line.orderId) {
                      <button type="button" class="approve" (click)="approve(line, line.orderId)" [disabled]="saving()">Ödemeyi onayla</button>
                    }
                    @if (line.status === 'Unmatched') {
                      @if (candidates(line).length) {
                        <select [ngModel]="picked()[line.id] || ''" (ngModelChange)="pick(line.id, $event)" [name]="'order-' + line.id" [attr.aria-label]="'Sipariş seç: ' + line.description">
                          <option value="">Aynı tutarda bekleyen sipariş…</option>
                          @for (order of candidates(line); track order.order.id) { <option [value]="order.order.id">{{ order.accountName }}, {{ order.order.planName }}, {{ order.order.paymentCode }}</option> }
                        </select>
                        <button type="button" class="approve" (click)="approve(line, picked()[line.id])" [disabled]="saving() || !picked()[line.id]">Eşleştir ve onayla</button>
                      }
                      <button type="button" (click)="ignore(line)" [disabled]="saving()">Yok say</button>
                    }
                    @if (line.status === 'Ignored') { <button type="button" (click)="restore(line)" [disabled]="saving()">Geri al</button> }
                  </td>
                </tr>
              } @empty { <tr><td colspan="6" class="empty">{{ loading() ? 'Yükleniyor…' : (status() === 'Unmatched' ? 'İncelenecek banka hareketi yok.' : 'Banka hareketi yok.') }}</td></tr> }
            </tbody>
          </table>
        </div>
      </section>

      <section class="data-card" aria-labelledby="imports-title">
        <h2 id="imports-title" class="mb-3 text-lg font-semibold text-(--ui-text)">Son yüklemeler</h2>
        <div class="overflow-x-auto">
          <table class="data-table">
            <thead><tr><th>Yükleme</th><th>Banka</th><th>Dosya</th><th>Gelen satır</th><th>Yeni</th><th>Otomatik onay</th><th>Onay bekleyen</th></tr></thead>
            <tbody>
              @for (item of imports(); track item.id) {
                <tr><td class="nowrap">{{ item.uploadedAt | date:'dd.MM.yyyy HH:mm' }}</td><td>{{ item.bankName }}</td><td>{{ item.fileName }}</td>
                  <td>{{ item.rowCount }}</td><td>{{ item.addedCount }}</td><td>{{ item.autoApprovedCount }}</td><td>{{ item.matchedCount }}</td></tr>
              } @empty { <tr><td colspan="7" class="empty">Henüz ekstre yüklenmedi.</td></tr> }
            </tbody>
          </table>
        </div>
      </section>
    </main>
  `,
  styles: [BILLING_PAGE_STYLES, `
    .filter { display: flex; flex-direction: column; gap: .25rem; font-size: .8rem; color: var(--ui-text); }
    .recognised { color: var(--ds-success); }
    .nowrap { white-space: nowrap; }
    .description { max-width: 28rem; overflow-wrap: anywhere; }
    .preview td, .preview th { white-space: nowrap; max-width: 18rem; overflow: hidden; text-overflow: ellipsis; }
    .preview .picked { background: color-mix(in srgb, var(--ui-brand) 8%, transparent); }
    .actions select { max-width: 16rem; margin-right: .35rem; font-size: .8rem; }
    .actions .approve { border-color: var(--ds-success); color: var(--ds-success); }
    @media (max-width: 640px) { .actions select { max-width: 100%; margin: 0 0 .35rem; display: block; } }
  `]
})
export class BillingStatementsComponent implements OnInit {
  private readonly billing = inject(BillingAdminService);
  private readonly toaster = inject(ToasterService);

  readonly statuses: BankTransactionStatus[] = ['Unmatched', 'Matched', 'Approved', 'Ignored'];
  readonly accounts = signal<BillingBankAccount[]>([]);
  readonly bankAccountId = signal('');
  readonly preview = signal<StatementPreview | null>(null);
  readonly mapping = signal<StatementMapping | null>(null);
  readonly result = signal<StatementImportResult | null>(null);
  readonly lines = signal<BankTransaction[]>([]);
  readonly imports = signal<StatementImport[]>([]);
  readonly pendingOrders = signal<BillingAdminOrder[]>([]);
  readonly picked = signal<Record<string, string>>({});
  readonly status = signal<BankTransactionStatus | ''>('Unmatched');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  private file: File | null = null;

  readonly headers = computed(() => {
    const shown = this.preview();
    const columns = this.mapping();
    if (!shown || !columns) return [];
    const width = Math.max(0, ...shown.rows.map(row => row.length));
    const header = shown.rows[columns.headerRow] ?? [];
    return Array.from({ length: width }, (_, index) => header[index]?.trim() || `${index + 1}. sütun`);
  });
  readonly sampleRows = computed(() => {
    const shown = this.preview();
    const columns = this.mapping();
    return shown && columns ? shown.rows.slice(columns.headerRow + 1, columns.headerRow + 9) : [];
  });

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({ accounts: this.billing.getBankAccounts(), lines: this.billing.getBankTransactions(this.status()), imports: this.billing.getStatementImports(),
      orders: this.billing.getOrders('Pending', '') })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ accounts, lines, imports, orders }) => {
          this.accounts.set(accounts);
          if (accounts.length === 1) this.bankAccountId.set(accounts[0].id);
          this.lines.set(lines);
          this.imports.set(imports);
          this.pendingOrders.set(orders);
        },
        error: () => this.error.set('Banka hareketleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.')
      });
  }

  statusLabel(status: BankTransactionStatus): string { return BANK_TRANSACTION_STATUS_LABELS[status] ?? status; }
  rowLabel(row: string[]): string { return row.filter(cell => cell.trim()).slice(0, 3).map(cell => cell.trim()).join(' · ').slice(0, 60) || '(boş)'; }

  isPicked(column: number): boolean {
    const columns = this.mapping();
    return !!columns && [columns.dateColumn, columns.descriptionColumn, columns.amountColumn, columns.directionColumn].includes(column);
  }

  unmatchedCount(done: StatementImportResult): number {
    return done.transactions.filter(line => line.status === 'Unmatched').length;
  }

  /** Waiting orders a line could pay: the same amount, nothing else. */
  candidates(line: BankTransaction): BillingAdminOrder[] {
    return this.pendingOrders().filter(order => order.order.amount === line.amount);
  }

  pick(lineId: string, orderId: string): void { this.picked.update(current => ({ ...current, [lineId]: orderId })); }

  choose(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
    this.preview.set(null);
    this.mapping.set(null);
    this.result.set(null);
    this.error.set('');
    if (!this.file) return;
    this.billing.previewStatement(this.file).subscribe({
      next: preview => {
        this.preview.set(preview);
        this.mapping.set(preview.suggested ? { ...preview.suggested }
          : { headerRow: 0, dateColumn: 0, descriptionColumn: 1, amountColumn: 2, senderColumn: null, referenceColumn: null, directionColumn: null, incomingValue: null });
      },
      error: error => this.error.set(billingErrorMessage(error, 'Dosya okunamadı.'))
    });
  }

  upload(): void {
    const columns = this.mapping();
    if (!this.file || !columns || !this.bankAccountId()) return;
    const mapping: StatementMapping = { ...columns, incomingValue: columns.directionColumn === null ? null : (columns.incomingValue ?? '').trim() || null };
    this.run(this.billing.importStatement(this.file, this.bankAccountId(), mapping), result => {
      this.result.set(result);
      this.preview.set(null);
      this.mapping.set(null);
      this.file = null;
      this.toaster.success('Ekstre yüklendi.');
      this.refresh();
    }, 'Ekstre yüklenemedi. Sayfayı yenileyip yeniden deneyin.');
  }

  approve(line: BankTransaction, orderId: string | null | undefined): void {
    if (!orderId) return;
    this.run(this.billing.approveBankTransaction(line.id, orderId), () => {
      this.toaster.success('Ödeme onaylandı; abonelik açıldı.');
      this.refresh();
    }, 'Ödeme onaylanamadı.');
  }

  ignore(line: BankTransaction): void {
    this.run(this.billing.ignoreBankTransaction(line.id), () => this.loadLines(), 'İşlem tamamlanamadı.');
  }

  restore(line: BankTransaction): void {
    this.run(this.billing.restoreBankTransaction(line.id), () => this.loadLines(), 'İşlem tamamlanamadı.');
  }

  loadLines(): void {
    this.billing.getBankTransactions(this.status()).subscribe({ next: lines => this.lines.set(lines), error: () => this.error.set('Banka hareketleri yüklenemedi. Sayfayı yenileyip yeniden deneyin.') });
  }

  private refresh(): void {
    this.loadLines();
    this.billing.getStatementImports().subscribe({ next: imports => this.imports.set(imports) });
    this.billing.getOrders('Pending', '').subscribe({ next: orders => this.pendingOrders.set(orders) });
  }

  private run<T>(request: Observable<T>, done: (value: T) => void, fallback: string): void {
    this.saving.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: done,
      error: error => { this.error.set(billingErrorMessage(error, fallback)); this.refresh(); }
    });
  }
}
