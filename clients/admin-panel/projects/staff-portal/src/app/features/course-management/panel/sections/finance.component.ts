import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseInstallment, CourseManagementService, CourseFinanceSummary } from '../../course-management.service';
import { CoursePanelApi, PanelInstallmentRow, PanelPaymentRow, PanelStudentRow } from '../panel-api.service';
import { CoursePanelState, formatDate, formatDateTime, formatMoney, problemText, readHashQuery, writeHashQuery } from '../panel-state';

type Tab = 'debts' | 'payments';
const MODELS = [
  { value: 1, label: 'Dönemlik, taksitli', hint: 'Toplam ücret, eşit taksitlere bölünür.' },
  { value: 2, label: 'Aylık sabit', hint: 'Her ay aynı tutar; seçtiğiniz ay sayısı kadar taksit açılır.' },
  { value: 3, label: 'Ders ya da saat başı', hint: 'Ders tamamlandıkça borç yazılır; yoklamada "gelmedi" olsa da yazılır.' },
  { value: 4, label: 'Ders paketi', hint: 'Belirli sayıda ders hakkı; her tamamlanan ders bir hak düşer.' }
] as const;

/** Tahsilat: debts and payments with filters and pages, taking a payment and setting up a student's fee. Owner only. */
@Component({
  selector: 'course-finance-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head">
      <div><h1>Tahsilat</h1><p>Taksitler, ödemeler ve ücret anlaşmaları</p></div>
      @if (!state.readOnly()) {
        <div class="actions">
          <button type="button" class="btn-secondary" (click)="openAgreement()">Ücret anlaşması ekle</button>
          <button type="button" class="btn" (click)="openPayment(null)">Ödeme al</button>
        </div>
      }
    </header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
    @if (summary(); as totals) {
      <section class="kpis" aria-label="Özet" style="margin-bottom: 20px">
        <div class="card kpi"><span class="kpi-label">Geciken</span><span class="kpi-value num">{{ money(totals.overdueAmount) }}</span></div>
        <div class="card kpi"><span class="kpi-label">Önümüzdeki 30 gün</span><span class="kpi-value num">{{ money(totals.dueInNext30Days) }}</span></div>
        <div class="card kpi"><span class="kpi-label">Toplam bakiye</span><span class="kpi-value num">{{ money(totals.balance) }}</span></div>
      </section>
    }
    <div class="tabs" role="tablist" aria-label="Görünüm" style="margin-bottom: 16px">
      <button type="button" role="tab" [attr.aria-selected]="tab() === 'debts'" (click)="setTab('debts')">Taksit ve borçlar</button>
      <button type="button" role="tab" [attr.aria-selected]="tab() === 'payments'" (click)="setTab('payments')">Alınan ödemeler</button>
    </div>

    @if (tab() === 'debts') {
      <section class="card">
        <form class="filters" (ngSubmit)="loadDebts(0)" role="search">
          <label class="field">Durum<select name="status" [(ngModel)]="debt.status" (ngModelChange)="loadDebts(0)">
            <option value="open">Ödenmemiş</option><option value="overdue">Gecikmiş</option><option value="upcoming">Vadesi gelmemiş</option><option value="paid">Ödenmiş</option></select></label>
          <label class="field">Kurs<select name="course" [(ngModel)]="debt.courseId" (ngModelChange)="loadDebts(0)"><option value="">Tümü</option>
            @for (course of state.courses(); track course.id) { <option [value]="course.id">{{ course.name }}</option> }</select></label>
          <label class="field">Ara<input name="q" [(ngModel)]="debt.q" placeholder="Öğrenci ya da taksit adı"></label>
          <button type="submit" class="btn-secondary">Ara</button>
        </form>
        @if (debts(); as page) {
          @if (page.items.length) {
            <div class="table-wrap"><table>
              <thead><tr><th scope="col">Öğrenci</th><th scope="col">Kurs ve taksit</th><th scope="col">Son gün</th>
                <th scope="col" class="right">{{ debt.status === 'paid' ? 'Tutar' : 'Kalan' }}</th><th scope="col">Durum</th><th scope="col"><span class="sr-only">İşlem</span></th></tr></thead>
              <tbody>
                @for (row of page.items; track row.id) {
                  <tr>
                    <th scope="row">{{ row.studentName }}</th>
                    <td>{{ row.courseName }}, {{ row.label }}</td>
                    <td class="num">{{ day(row.dueOn) }}</td>
                    <td class="right num"><strong>{{ money(debt.status === 'paid' ? row.amount : row.remaining) }}</strong></td>
                    <td>@if (debt.status === 'paid') { <span class="badge ok">Ödendi</span> }
                      @else if (row.overdueDays) { <span class="badge bad">{{ row.overdueDays }} gün gecikti</span> }
                      @else { <span class="badge">{{ daysLeft(row) }}</span> }</td>
                    <td class="right">@if (debt.status !== 'paid' && !state.readOnly()) {
                      <button type="button" class="btn-secondary btn-small" (click)="openPayment(row)">Ödeme al</button> }</td>
                  </tr>
                }
              </tbody>
            </table></div>
            <nav class="pager" aria-label="Taksit sayfaları">
              <span class="num">{{ page.skip + 1 }}–{{ page.skip + page.items.length }} / {{ page.total }}@if (debt.status !== 'paid') {, toplam {{ money(page.outstanding) }} }</span>
              <div class="actions">
                <button type="button" class="btn-secondary btn-small" (click)="loadDebts(page.skip - page.take)" [disabled]="page.skip === 0">Önceki</button>
                <button type="button" class="btn-secondary btn-small" (click)="loadDebts(page.skip + page.take)" [disabled]="page.skip + page.take >= page.total">Sonraki</button>
              </div>
            </nav>
          } @else { <p class="empty">Bu filtrelere uyan kayıt yok.</p> }
        }
      </section>
    } @else {
      <section class="card">
        <form class="filters" (ngSubmit)="loadPayments(0)" role="search">
          <label class="field">Başlangıç<input name="from" type="date" [(ngModel)]="pay.from" (ngModelChange)="loadPayments(0)"></label>
          <label class="field">Bitiş<input name="to" type="date" [(ngModel)]="pay.to" (ngModelChange)="loadPayments(0)"></label>
          <label class="field">Yöntem<select name="method" [(ngModel)]="pay.method" (ngModelChange)="loadPayments(0)">
            <option value="">Tümü</option><option value="1">Nakit</option><option value="2">Havale / EFT</option></select></label>
          <label class="field">Öğrenci<input name="q" [(ngModel)]="pay.q"></label>
          <button type="submit" class="btn-secondary">Ara</button>
        </form>
        @if (paymentsPage(); as page) {
          @if (page.items.length) {
            <div class="table-wrap"><table>
              <thead><tr><th scope="col">Tarih</th><th scope="col">Öğrenci</th><th scope="col">Yöntem</th><th scope="col" class="right">Tutar</th><th scope="col">Not</th></tr></thead>
              <tbody>
                @for (row of page.items; track row.id) {
                  <tr>
                    <td class="num">{{ dateTime(row.receivedAt) }}</td>
                    <th scope="row">{{ row.studentName }}</th>
                    <td>{{ row.method === 'Cash' ? 'Nakit' : 'Havale / EFT' }}</td>
                    <td class="right num">{{ money(row.amount) }}</td>
                    <td>@if (row.isReversal) { <span class="badge warn">Düzeltme</span> } @else if (row.reversed) { <span class="badge">Geri alındı</span> } {{ row.note ?? '' }}</td>
                  </tr>
                }
              </tbody>
            </table></div>
            <nav class="pager" aria-label="Ödeme sayfaları">
              <span class="num">{{ page.skip + 1 }}–{{ page.skip + page.items.length }} / {{ page.total }}, toplam {{ money(page.sum) }}</span>
              <div class="actions">
                <button type="button" class="btn-secondary btn-small" (click)="loadPayments(page.skip - page.take)" [disabled]="page.skip === 0">Önceki</button>
                <button type="button" class="btn-secondary btn-small" (click)="loadPayments(page.skip + page.take)" [disabled]="page.skip + page.take >= page.total">Sonraki</button>
              </div>
            </nav>
          } @else { <p class="empty">Bu filtrelere uyan ödeme yok.</p> }
        }
      </section>
    }

    @if (payment(); as form) {
      <div class="dialog-backdrop" (click)="payment.set(null)">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="payment-title" (click)="$event.stopPropagation()" (keydown.escape)="payment.set(null)">
          <h2 id="payment-title">Ödeme al</h2>
          @if (dialogError()) { <p class="alert" role="alert">{{ dialogError() }}</p> }
          <div class="form-grid">
            <label class="field">Öğrenci<input [(ngModel)]="form.studentName" (ngModelChange)="pickStudent($event, 'payment')" list="pay-students" autocomplete="off">
              <datalist id="pay-students">@for (student of studentOptions(); track student.id) { <option [value]="student.displayName"></option> }</datalist></label>
            <label class="field">Tutar (TL)<input type="number" min="0.01" step="0.01" [(ngModel)]="form.amount"></label>
            <label class="field">Yöntem<select [(ngModel)]="form.method"><option [ngValue]="1">Nakit</option><option [ngValue]="2">Havale / EFT</option></select></label>
            <label class="field">Tarih<input type="date" [(ngModel)]="form.day"></label>
          </div>
          <label class="field">Not<input [(ngModel)]="form.note" maxlength="500" placeholder="İsteğe bağlı"></label>
          @if (openInstallments().length) {
            <p class="muted" style="margin: 0">Ödeme en eski borçtan başlayarak kapatılır:
              {{ allocationLine(form.amount) }}</p>
          } @else if (form.studentId) { <p class="muted" style="margin: 0">Açık borç yok; ödeme bakiyeye yazılır.</p> }
          <div class="dialog-actions">
            <button type="button" class="btn-secondary" (click)="payment.set(null)">Vazgeç</button>
            <button type="button" class="btn" (click)="savePayment()" [disabled]="busy() || !form.studentId || !(form.amount > 0) || !form.day">Ödemeyi kaydet</button>
          </div>
        </section>
      </div>
    }

    @if (agreement(); as form) {
      <div class="dialog-backdrop" (click)="agreement.set(null)">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="agreement-title" (click)="$event.stopPropagation()" (keydown.escape)="agreement.set(null)">
          <h2 id="agreement-title">Ücret anlaşması ekle</h2>
          @if (dialogError()) { <p class="alert" role="alert">{{ dialogError() }}</p> }
          <div class="form-grid">
            <label class="field">Öğrenci<input [(ngModel)]="form.studentName" (ngModelChange)="pickStudent($event, 'agreement')" list="fee-students" autocomplete="off">
              <datalist id="fee-students">@for (student of studentOptions(); track student.id) { <option [value]="student.displayName"></option> }</datalist></label>
            <label class="field">Kurs<select [(ngModel)]="form.courseId"><option value="">Kurs seçin</option>
              @for (course of state.courses(); track course.id) { <option [value]="course.id">{{ course.name }}</option> }</select></label>
            <label class="field">Ücret türü<select [(ngModel)]="form.model">@for (model of models; track model.value) { <option [ngValue]="model.value">{{ model.label }}</option> }</select>
              <span class="hint">{{ modelHint(form.model) }}</span></label>
            <label class="field">{{ form.model === 1 ? 'Toplam tutar (TL)' : form.model === 2 ? 'Aylık tutar (TL)' : form.model === 3 ? 'Birim ücret (TL)' : 'Paket tutarı (TL)' }}
              <input type="number" min="0.01" step="0.01" [(ngModel)]="form.amount"></label>
            @if (form.model === 1 || form.model === 2) {
              <label class="field">{{ form.model === 1 ? 'Taksit sayısı' : 'Kaç ay' }}<input type="number" min="1" max="24" [(ngModel)]="form.count"></label>
              <label class="field">İlk ödeme günü<input type="date" [(ngModel)]="form.firstDue"></label>
            }
            @if (form.model === 3) {
              <label class="field">Birim<select [(ngModel)]="form.unit"><option [ngValue]="1">Ders başı</option><option [ngValue]="2">Saat başı</option></select></label>
            }
            @if (form.model === 4) {
              <label class="field">Ders sayısı<input type="number" min="1" max="500" [(ngModel)]="form.sessions"></label>
              <label class="field">Son kullanım<input type="date" [(ngModel)]="form.expiresOn"></label>
              <label class="field">Ödeme günü<input type="date" [(ngModel)]="form.firstDue"></label>
            }
          </div>
          @if (form.model === 1 && form.amount > 0 && form.count > 0) {
            <p class="muted" style="margin: 0">{{ form.count }} taksit, her biri yaklaşık {{ money(form.amount / form.count) }}; ayın aynı gününde.</p>
          }
          <div class="dialog-actions">
            <button type="button" class="btn-secondary" (click)="agreement.set(null)">Vazgeç</button>
            <button type="button" class="btn" (click)="saveAgreement()" [disabled]="busy() || !form.studentId || !form.courseId || !(form.amount > 0)">Kaydet</button>
          </div>
        </section>
      </div>
    }
  `
})
export class FinanceSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly models = MODELS;
  readonly tab = signal<Tab>('debts');
  readonly summary = signal<CourseFinanceSummary | null>(null);
  readonly debts = signal<{ total: number; outstanding: number; skip: number; take: number; items: PanelInstallmentRow[] } | null>(null);
  readonly paymentsPage = signal<{ total: number; sum: number; skip: number; take: number; items: PanelPaymentRow[] } | null>(null);
  readonly payment = signal<{ studentId: string; studentName: string; amount: number; method: 1 | 2; day: string; note: string } | null>(null);
  readonly agreement = signal<{ studentId: string; studentName: string; courseId: string; model: 1 | 2 | 3 | 4; amount: number; count: number;
    firstDue: string; unit: 1 | 2; sessions: number; expiresOn: string } | null>(null);
  readonly openInstallments = signal<CourseInstallment[]>([]);
  readonly studentOptions = signal<PanelStudentRow[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly notice = signal('');
  readonly money = formatMoney;
  readonly dateTime = formatDateTime;
  debt = { status: 'open', courseId: '', q: '' };
  pay = { from: '', to: '', method: '', q: '' };

  constructor() {
    const query = readHashQuery();
    if (query.get('gorunum') === 'odemeler') this.tab.set('payments');
    this.debt.status = query.get('durum') ?? 'open';
    effect(() => { this.state.id(); this.state.version(); untracked(() => void this.reload()); });
  }

  private today(): string { return new Date(Date.now() + 3 * 3_600_000).toISOString().slice(0, 10); }
  day(value: string): string { return formatDate(`${value}T12:00:00+03:00`); }
  daysLeft(row: PanelInstallmentRow): string {
    const days = Math.round((new Date(`${row.dueOn}T00:00:00+03:00`).getTime() - new Date(`${this.today()}T00:00:00+03:00`).getTime()) / 86_400_000);
    return days === 0 ? 'Bugün' : `${days} gün kaldı`;
  }
  modelHint(model: number): string { return MODELS.find(item => item.value === model)?.hint ?? ''; }

  async reload(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    try { this.summary.set(await this.service.getFinanceSummary(id)); } catch { /* the lists still load */ }
    await (this.tab() === 'debts' ? this.loadDebts(0) : this.loadPayments(0));
  }
  setTab(tab: Tab): void { this.tab.set(tab); void (tab === 'debts' ? this.loadDebts(0) : this.loadPayments(0)); }

  async loadDebts(skip: number): Promise<void> {
    writeHashQuery('tahsilat', { durum: this.debt.status === 'open' ? null : this.debt.status });
    try { this.debts.set(await this.api.installments(this.state.id(), { ...this.debt, skip: Math.max(0, skip), take: 25 })); this.error.set(''); }
    catch (error) { this.error.set(problemText(error, 'Taksitler yüklenemedi.')); }
  }
  async loadPayments(skip: number): Promise<void> {
    writeHashQuery('tahsilat', { gorunum: 'odemeler' });
    try { this.paymentsPage.set(await this.api.payments(this.state.id(), { ...this.pay, skip: Math.max(0, skip), take: 25 })); this.error.set(''); }
    catch (error) { this.error.set(problemText(error, 'Ödemeler yüklenemedi.')); }
  }

  async pickStudent(text: string, target: 'payment' | 'agreement'): Promise<void> {
    const match = this.studentOptions().find(student => student.displayName === text);
    if (target === 'payment') { this.payment.update(form => form && { ...form, studentId: match?.id ?? '', studentName: text }); if (match) await this.loadOpen(match.id); }
    else this.agreement.update(form => form && { ...form, studentId: match?.id ?? '', studentName: text });
    if (match || text.trim().length < 2) return;
    try { this.studentOptions.set((await this.api.searchStudents(this.state.id(), { q: text.trim(), take: 20 })).items); } catch { /* keep typing */ }
  }

  private async loadOpen(studentId: string): Promise<void> {
    try {
      const account = await this.service.getStudentAccount(this.state.id(), studentId);
      this.openInstallments.set(account.installments.filter(item => item.remainingAmount > 0 && !item.reversalOfInstallmentId)
        .sort((a, b) => a.dueOn.localeCompare(b.dueOn)));
    } catch { this.openInstallments.set([]); }
  }

  openPayment(row: PanelInstallmentRow | null): void {
    this.dialogError.set('');
    this.openInstallments.set([]);
    this.payment.set({ studentId: row?.studentId ?? '', studentName: row?.studentName ?? '', amount: row?.remaining ?? 0, method: 1, day: this.today(), note: '' });
    if (row) void this.loadOpen(row.studentId);
  }

  private allocate(amount: number): { installmentId: string; amount: number }[] {
    let left = Math.round(amount * 100) / 100;
    const result: { installmentId: string; amount: number }[] = [];
    for (const item of this.openInstallments()) {
      if (left <= 0) break;
      const part = Math.min(left, item.remainingAmount);
      result.push({ installmentId: item.id, amount: Math.round(part * 100) / 100 });
      left = Math.round((left - part) * 100) / 100;
    }
    return result;
  }
  allocationLine(amount: number): string {
    const parts = this.allocate(amount || 0).map(part => {
      const item = this.openInstallments().find(installment => installment.id === part.installmentId)!;
      return `${item.label} ${formatMoney(part.amount)}`;
    });
    return parts.length ? parts.join(', ') + '.' : 'tutar girin.';
  }

  async savePayment(): Promise<void> {
    const form = this.payment();
    if (!form) return;
    this.busy.set(true); this.dialogError.set('');
    try {
      await this.service.recordPayment(this.state.id(), { id: crypto.randomUUID(), studentId: form.studentId, method: form.method,
        amount: Math.round(form.amount * 100) / 100, receivedAt: new Date(`${form.day}T12:00:00+03:00`).toISOString(),
        note: form.note.trim() || null, allocations: this.allocate(form.amount) });
      this.payment.set(null);
      this.notice.set(`${form.studentName} için ${formatMoney(form.amount)} ödeme kaydedildi.`);
      this.state.changed();
    } catch (error) { this.dialogError.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  openAgreement(): void {
    this.dialogError.set('');
    this.agreement.set({ studentId: '', studentName: '', courseId: this.state.courses()[0]?.id ?? '', model: 1, amount: 0, count: 8,
      firstDue: this.today(), unit: 1, sessions: 8, expiresOn: '' });
  }

  async saveAgreement(): Promise<void> {
    const form = this.agreement();
    if (!form) return;
    this.busy.set(true); this.dialogError.set('');
    const id = this.state.id();
    try {
      const amount = Math.round(form.amount * 100) / 100;
      const created = await this.service.createFeeAgreement(id, { id: crypto.randomUUID(), studentId: form.studentId, courseId: form.courseId,
        model: form.model, amount, billingUnit: form.model === 3 ? form.unit : null,
        packageSessions: form.model === 4 ? form.sessions : null,
        packageExpiresAt: form.model === 4 && form.expiresOn ? new Date(`${form.expiresOn}T23:59:00+03:00`).toISOString() : null });
      const installments: { label: string; amount: number; dueOn: string }[] = [];
      if (form.model === 1 || form.model === 2) {
        const count = Math.max(1, Math.min(24, Math.floor(form.count)));
        const each = form.model === 1 ? Math.floor(amount / count * 100) / 100 : amount;
        for (let index = 0; index < count; index++) {
          const due = new Date(`${form.firstDue}T12:00:00Z`);
          due.setUTCMonth(due.getUTCMonth() + index);
          const value = form.model === 1 && index === count - 1 ? Math.round((amount - each * (count - 1)) * 100) / 100 : each;
          installments.push({ label: form.model === 1 ? `${index + 1}. taksit` : this.monthName(due), amount: value, dueOn: due.toISOString().slice(0, 10) });
        }
      } else if (form.model === 4) {
        installments.push({ label: `${form.sessions} derslik paket`, amount, dueOn: form.firstDue || this.today() });
      }
      for (const item of installments) await this.service.createInstallment(id, created.id, { id: crypto.randomUUID(), ...item });
      this.agreement.set(null);
      this.notice.set(installments.length ? `Anlaşma kaydedildi; ${installments.length} taksit açıldı.` : 'Anlaşma kaydedildi; borç ders tamamlandıkça yazılır.');
      this.state.changed();
    } catch (error) {
      this.dialogError.set((error as { error?: { code?: string } })?.error?.code === 'FeeAgreement.ActiveAgreementExists'
        ? 'Bu öğrencinin bu kurs için açık bir anlaşması var. Önce onu kapatın.' : problemText(error));
    } finally { this.busy.set(false); }
  }

  private monthName(date: Date): string {
    return new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date);
  }
}
