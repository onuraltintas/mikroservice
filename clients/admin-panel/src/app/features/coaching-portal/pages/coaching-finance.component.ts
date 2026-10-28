import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, OnChanges, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, Subscription, finalize } from 'rxjs';
import { CoachingCashStatement, CoachingFee, CoachingFeeAccount, CoachingFeeCancelInput, CoachingFeeCreateInput, CoachingFeePayment, CoachingFeePaymentInput, CoachingFeeReverseInput, CoachingFinanceMode, CoachingFinanceService, CoachingPaymentMethod, CoachingPaymentPage, CoachingReceivables } from '../../../core/services/coaching-finance.service';

type FinanceForm = 'create' | 'payment' | 'reverse' | 'cancel';
type FinanceInput = CoachingFeeCreateInput | CoachingFeePaymentInput | CoachingFeeReverseInput | CoachingFeeCancelInput;

@Component({
  selector: 'app-coaching-finance', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './coaching-finance.component.html', styleUrl: './coaching-finance.component.scss'
})
export class CoachingFinanceComponent implements OnChanges, OnInit, OnDestroy {
  private readonly toaster = coachingToaster();
  private readonly api = inject(CoachingFinanceService);
  @Input() mode: CoachingFinanceMode = 'student';
  @Input() studentId = '';
  @Input() cashView = false;
  readonly account = signal<CoachingFeeAccount | null>(null);
  readonly cash = signal<CoachingCashStatement | null>(null);
  readonly receivables = signal<CoachingReceivables | null>(null);
  readonly receivablesLoading = signal(false);
  readonly receivablesError = signal<string | null>(null);
  private receivablesRead?: Subscription;
  readonly history = signal<CoachingPaymentPage | null>(null);
  readonly historyFee = signal<CoachingFee | null>(null);
  readonly loading = signal(false);
  readonly historyLoading = signal(false);
  readonly pending = signal(false);
  readonly error = signal<string | null>(null);
  readonly historyError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly form = signal<FinanceForm | null>(null);
  readonly conflict = signal(false);
  readonly attempted = signal(false);
  readonly canManage = computed(() => this.mode === 'staff' && !!this.account()?.canManage);
  readonly canCreate = computed(() => this.mode === 'staff' && !!this.account()?.canCreate);
  private reads = new Subscription();
  private historyRead?: Subscription;
  private write?: Subscription;
  private generation = 0;
  private initialized = false;
  private fee?: CoachingFee;
  private paymentId = '';
  private requestId = '';
  private retryInput: FinanceInput | null = null;
  private cashStudentId = '';
  title = '';
  amount: number | null = null;
  method: CoachingPaymentMethod = 'Cash';
  receivedOn = this.today();
  reference = '';
  reason = '';
  confirmed = false;
  installments: { dueOn: string; amount: number | null }[] = [];
  fromDate = `${this.today().slice(0, 7)}-01`;
  toDate = this.today();

  ngOnInit(): void { if (!this.initialized) this.ngOnChanges(); }
  ngOnChanges(): void {
    this.initialized = true;
    ++this.generation; this.reads.unsubscribe(); this.reads = new Subscription(); this.historyRead?.unsubscribe(); this.receivablesRead?.unsubscribe(); this.write?.unsubscribe();
    this.receivablesLoading.set(false);
    this.pending.set(false); this.loading.set(false); this.historyLoading.set(false);
    this.account.set(null); this.cash.set(null); this.receivables.set(null); this.receivablesError.set(null); this.history.set(null); this.historyFee.set(null); this.cashStudentId = '';
    this.resetForm(); this.error.set(null); this.notice.set(null); this.historyError.set(null);
    if (this.cashView && this.mode !== 'student') { void this.loadCash(); this.loadReceivables(); }
    else if (this.mode === 'student' || this.studentId) void this.loadAccount();
  }
  ngOnDestroy(): void { ++this.generation; this.reads.unsubscribe(); this.historyRead?.unsubscribe(); this.receivablesRead?.unsubscribe(); this.write?.unsubscribe(); }
  private subjectId(): string { return this.cashStudentId || this.studentId; }
  async canLeavePage(): Promise<boolean> {
    if (this.pending()) { this.error.set('İşlem sürüyor. Tamamlanmasını bekle.'); return false; }
    return !this.form() || await this.toaster.confirm('Kaydedilmemiş ücret işleminden ayrılmak istiyor musun?');
  }
  async loadAccount(page = 1, preserveForm = false): Promise<void> {
    const scope = this.generation;
    if (this.pending() || (!preserveForm && !await this.canLeavePage())) return;
    if (scope !== this.generation || this.pending()) return;
    if (!preserveForm) this.resetForm();
    const generation = this.generation; this.loading.set(true); this.error.set(null);
    this.reads.add(this.api.account(this.mode, this.subjectId(), page).pipe(finalize(() => {
      if (generation === this.generation) this.loading.set(false);
    })).subscribe({ next: account => {
      if (generation !== this.generation) return;
      this.account.set(account);
      if (preserveForm && this.fee) {
        const current = account.agreements.find(item => item.id === this.fee?.id);
        if (!current) { this.error.set('Ücret bu sayfada bulunamadı. İşlemden çıkıp ilgili kaydı yeniden aç.'); return; }
        this.fee = current; this.retryInput = null; this.attempted.set(false); this.conflict.set(false);
        if (this.historyFee()?.id === current.id) this.loadHistory(current);
      }
    }, error: error => { if (generation === this.generation) this.fail(error); } }));
  }
  async loadCash(page = 1): Promise<void> {
    const scope = this.generation;
    if (this.mode === 'student' || this.pending() || !await this.canLeavePage()) return;
    if (scope !== this.generation || this.pending()) return;
    const from = Date.parse(this.fromDate), to = Date.parse(this.toDate);
    if (!this.validDate(this.fromDate) || !this.validDate(this.toDate) || to < from || (to - from) / 86400000 > 366) {
      this.error.set('Geçerli bir tarih aralığı seç (en fazla 367 gün).'); return;
    }
    const generation = this.generation; this.loading.set(true); this.error.set(null);
    this.reads.add(this.api.cash(this.mode, this.fromDate, this.toDate, page).pipe(finalize(() => {
      if (generation === this.generation) this.loading.set(false);
    })).subscribe({ next: value => { if (generation === this.generation) this.cash.set(value); },
      error: error => { if (generation === this.generation) { this.cash.set(null); this.fail(error); } } }));
  }
  async openCashAccount(payment: CoachingFeePayment): Promise<void> {
    await this.openAccount(payment.studentId);
  }
  async openAccount(studentId: string): Promise<void> {
    if (!await this.canLeavePage()) return;
    this.reads.unsubscribe(); this.reads = new Subscription(); this.historyRead?.unsubscribe();
    this.loading.set(false); this.historyLoading.set(false); this.resetForm(); this.account.set(null);
    this.history.set(null); this.historyFee.set(null); this.historyError.set(null); this.notice.set(null);
    this.cashStudentId = studentId; await this.loadAccount();
  }
  loadReceivables(page = 1): void {
    if (this.mode === 'student' || this.pending()) return;
    this.receivablesRead?.unsubscribe(); this.receivablesLoading.set(true); this.receivablesError.set(null);
    const generation = this.generation;
    this.receivablesRead = this.api.receivables(this.mode, page).pipe(finalize(() => {
      if (generation === this.generation) this.receivablesLoading.set(false);
    })).subscribe({ next: value => { if (generation === this.generation) this.receivables.set(value); },
      error: error => { if (generation === this.generation) { this.receivables.set(null); this.receivablesError.set('Açık ücretler yüklenemedi. Yeniden dene.'); if (error instanceof HttpErrorResponse && error.status === 403) this.fail(error); } } });
  }
  loadHistory(fee: CoachingFee, page = 1): void {
    if (this.pending()) return;
    this.historyRead?.unsubscribe(); this.historyFee.set(fee); this.history.set(null); this.historyError.set(null); this.historyLoading.set(true);
    const generation = this.generation;
    this.historyRead = this.api.payments(this.mode, this.subjectId(), fee.id, page).pipe(finalize(() => {
      if (generation === this.generation) this.historyLoading.set(false);
    })).subscribe({ next: value => { if (generation === this.generation) this.history.set(value); },
      error: error => { if (generation === this.generation) { this.historyError.set('Tahsilat geçmişi yüklenemedi. Yeniden dene.'); if (error instanceof HttpErrorResponse && error.status === 403) this.fail(error); } } });
  }
  async openCreate(): Promise<void> { if (this.canCreate() && await this.startForm('create')) this.installments = [{ dueOn: this.today(), amount: null }]; }
  async openPayment(fee: CoachingFee): Promise<void> { if (this.canManage() && !fee.isCancelled && fee.balance > 0 && await this.startForm('payment', fee)) this.amount = fee.balance; }
  async openReverse(fee: CoachingFee, payment: CoachingFeePayment): Promise<void> {
    if (this.canManage() && !fee.isCancelled && payment.amount > 0 && !payment.reversalOfPaymentId && await this.startForm('reverse', fee)) this.paymentId = payment.id;
  }
  async openCancel(fee: CoachingFee): Promise<void> { if (this.canManage() && !fee.isCancelled && fee.paidAmount === 0) await this.startForm('cancel', fee); }
  private async startForm(kind: FinanceForm, fee?: CoachingFee): Promise<boolean> {
    if (this.pending() || !await this.canLeavePage()) return false;
    this.resetForm(); this.fee = fee; this.requestId = crypto.randomUUID(); this.form.set(kind); this.error.set(null); this.notice.set(null); return true;
  }
  async closeForm(): Promise<void> { if (await this.canLeavePage()) { this.resetForm(); this.error.set(null); } }
  private resetForm(): void {
    this.form.set(null); this.conflict.set(false); this.attempted.set(false); this.retryInput = null; this.fee = undefined;
    this.title = ''; this.amount = null; this.reference = ''; this.reason = ''; this.method = 'Cash'; this.receivedOn = this.today();
    this.confirmed = false; this.installments = []; this.paymentId = '';
  }
  addInstallment(): void { if (!this.attempted() && this.installments.length < 24) this.installments.push({ dueOn: this.today(), amount: null }); }
  removeInstallment(index: number): void { if (!this.attempted() && this.installments.length > 1) this.installments.splice(index, 1); }
  async reloadConflict(): Promise<void> { if (this.conflict() && !this.pending()) await this.loadAccount(this.account()?.pageNumber ?? 1, true); }
  submit(): void {
    const kind = this.form(); if (!kind || this.pending() || this.conflict() || this.loading() || !this.canManage() || (kind === 'create' && !this.canCreate())) return;
    const input = this.retryInput ?? this.input(kind); if (!input) return;
    this.retryInput = input; this.attempted.set(true); this.pending.set(true); this.error.set(null);
    const studentId = this.subjectId(), generation = this.generation, selectedFee = this.fee;
    let operation: Observable<CoachingFee>;
    if (kind === 'create') operation = this.api.create(studentId, input as CoachingFeeCreateInput);
    else if (kind === 'payment') operation = this.api.pay(studentId, selectedFee!.id, input as CoachingFeePaymentInput);
    else if (kind === 'reverse') operation = this.api.reverse(studentId, selectedFee!.id, this.paymentId, input as CoachingFeeReverseInput);
    else operation = this.api.cancel(studentId, selectedFee!.id, input as CoachingFeeCancelInput);
    this.write = operation.pipe(finalize(() => { if (generation === this.generation) this.pending.set(false); })).subscribe({
      next: async result => {
        if (generation !== this.generation) return;
        this.pending.set(false); this.resetForm(); this.notice.set('Manuel ücret işlemi kaydedildi.');
        await this.loadAccount(this.account()?.pageNumber ?? 1);
        if (this.historyFee()?.id === result.id) this.loadHistory(result);
        if (this.cashView) { await this.loadCash(this.cash()?.pageNumber ?? 1); this.loadReceivables(this.receivables()?.pageNumber ?? 1); }
      }, error: error => { if (generation === this.generation) this.fail(error); }
    });
  }
  private input(kind: FinanceForm): FinanceInput | null {
    const version = this.fee?.version ?? 0;
    if (kind === 'create') {
      const parts = this.installments.map(part => ({ dueOn: part.dueOn, amount: part.amount ?? NaN }));
      if (!this.title.trim() || this.title.trim().length > 200 || !this.money(this.amount) || parts.length < 1 || parts.length > 24 ||
        parts.some(part => !this.validDate(part.dueOn) || !this.money(part.amount)) ||
        parts.reduce((sum, part) => sum + Math.round(part.amount * 100), 0) !== Math.round(this.amount! * 100)) {
        this.error.set('Başlık, toplam ücret ve 1–24 taksidi kontrol et. Taksit toplamı ücrete eşit olmalıdır.'); return null;
      }
      return { requestId: this.requestId, title: this.title.trim(), totalAmount: this.amount!, installments: parts };
    }
    if (kind === 'payment' || kind === 'reverse') {
      if (!this.validDate(this.receivedOn) || this.receivedOn > this.today()) { this.error.set('Tahsilat tarihi geçerli olmalı ve gelecekte olmamalıdır.'); return null; }
    }
    if (kind === 'payment') {
      if (!this.money(this.amount) || this.amount! > this.fee!.balance || !['Cash', 'BankTransfer'].includes(this.method) || this.reference.trim().length > 300) {
        this.error.set('Tahsilat pozitif, en fazla iki ondalık haneli ve kalan borcu aşmayacak bir tutar olmalıdır.'); return null;
      }
      return { requestId: this.requestId, expectedVersion: version, amount: this.amount!, method: this.method, receivedOn: this.receivedOn, reference: this.reference.trim() || null };
    }
    if (!this.reason.trim() || this.reason.trim().length > 300 || !this.confirmed) { this.error.set('Gerekçe gir ve işlemi onayla.'); return null; }
    return kind === 'reverse' ? { requestId: this.requestId, expectedVersion: version, receivedOn: this.receivedOn, reason: this.reason.trim() } : { expectedVersion: version, reason: this.reason.trim() };
  }
  private fail(error: unknown): void {
    const status = error instanceof HttpErrorResponse ? error.status : 0;
    if (status === 403) {
      ++this.generation; this.reads.unsubscribe(); this.reads = new Subscription(); this.historyRead?.unsubscribe(); this.receivablesRead?.unsubscribe();
      this.pending.set(false); this.loading.set(false); this.historyLoading.set(false); this.receivablesLoading.set(false);
      this.account.set(null); this.cash.set(null); this.receivables.set(null); this.history.set(null); this.historyFee.set(null); this.resetForm();
      this.error.set('Bu ücret kayıtlarına erişim yetkin doğrulanamadı. Oturumunu yenile.');
    }
    else if (status === 409) { this.conflict.set(true); this.error.set('Ücret kaydı değişti. Güncel kaydı yükleyip tutarı kontrol ederek yeniden dene.'); }
    else this.error.set(this.attempted() ? 'İşlem doğrulanamadı. Yeniden dene aynı işlemi güvenli biçimde kontrol eder. Alanları değiştirmek için önce işlemden çık.' : 'Ücret kayıtları yüklenemedi. Yeniden dene.');
  }
  private money(value: number | null): boolean { return value !== null && Number.isFinite(value) && value >= 0.01 && value <= 999999999.99 && /^\d+(?:\.\d{1,2})?$/.test(String(value)); }
  private validDate(value: string): boolean { const date = new Date(`${value}T00:00:00Z`); return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value && value.slice(0, 4) !== '0000'; }
  today(): string { return new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()); }
  currency(value: number): string { return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(value); }
  paymentMethod(value: CoachingPaymentMethod): string { return value === 'Cash' ? 'Nakit' : 'Banka havalesi'; }
  private readonly feedback = coachingFeedback(this.notice, this.error);
}
