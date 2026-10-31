import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CoachingFee, CoachingFinanceService, CoachingPaymentPage } from '../../../core/services/coaching-finance.service';
import { CoachingDatePipe } from '../coaching-labels';

/** Roadmap O12: one coaching fee with its instalments and payment history, at its own address. */
@Component({
  selector: 'app-student-fee-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, CoachingDatePipe],
  template: `
    <section class="space-y-4" aria-labelledby="fee-detail-title">
      <a routerLink="/coaching-portal/account/payments/fees" class="text-sm font-medium text-(--ui-brand-text) ">← Koçluk ücretlerim</a>
      @if (error()) { <p role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">{{ error() }}</p> }
      @if (loading()) { <p role="status">Ücret yükleniyor…</p> }
      @if (fee(); as item) {
        <header>
          <h1 id="fee-detail-title" class="text-2xl font-bold tracking-tight">{{ item.title }}</h1>
          <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">{{ item.isCancelled ? 'İptal edildi' : item.balance === 0 ? 'Borç kapandı' : 'Açık ücret' }} · {{ item.institutionId ? 'Kurum koçluğu' : 'Bağımsız koçluk' }}</p>
        </header>
        <dl class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div class="rounded-xl border border-slate-200 p-3 dark:border-slate-800"><dt class="text-xs text-slate-500">Toplam</dt><dd class="font-semibold">{{ money(item.totalAmount) }}</dd></div>
          <div class="rounded-xl border border-slate-200 p-3 dark:border-slate-800"><dt class="text-xs text-slate-500">Ödenen</dt><dd class="font-semibold">{{ money(item.paidAmount) }}</dd></div>
          <div class="rounded-xl border border-slate-200 p-3 dark:border-slate-800"><dt class="text-xs text-slate-500">Kalan</dt><dd class="font-semibold">{{ money(item.balance) }}</dd></div>
          <div class="rounded-xl border border-slate-200 p-3 dark:border-slate-800"><dt class="text-xs text-slate-500">Vadesi geçen</dt><dd class="font-semibold">{{ money(item.overdueAmount) }}</dd></div>
        </dl>
        @if (item.isCancelled) { <p class="text-sm">İptal gerekçesi: {{ item.cancellationReason }}</p> }
        <section aria-labelledby="installments-heading">
          <h2 id="installments-heading" class="font-semibold">Taksitler</h2>
          <ol class="mt-2 space-y-2">
            @for (part of item.installments; track part.id) {
              <li class="flex flex-wrap justify-between gap-2 rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-800" [class.border-red-300]="part.isOverdue">
                <span>{{ part.sequence }}. taksit · {{ part.dueOn | coachingDate }}</span>
                <span>{{ money(part.amount) }} · Kalan {{ money(part.balance) }}{{ part.isOverdue ? ' · Vadesi geçti' : '' }}</span>
              </li>
            }
          </ol>
        </section>
        <section aria-labelledby="payments-heading">
          <h2 id="payments-heading" class="font-semibold">Ödemeler</h2>
          @if (payments(); as page) {
            @for (payment of page.items; track payment.id) {
              <p class="mt-2 rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-800">
                <strong>{{ money(payment.amount) }}</strong> · {{ payment.receivedOn | coachingDate }} · {{ payment.method === 'BankTransfer' ? 'Havale' : 'Nakit' }}{{ payment.reversalOfPaymentId ? ' · Düzeltme' : '' }}
                @if (payment.reference) { <br><span class="break-all text-slate-500">{{ payment.reference }}</span> }
              </p>
            } @empty { <p class="mt-2 text-sm">Henüz ödeme kaydı yok.</p> }
            @if (page.totalCount > page.pageSize) {
              <div class="mt-3 flex items-center gap-3 text-sm">
                <button type="button" [disabled]="page.pageNumber <= 1" (click)="loadPayments(page.pageNumber - 1)">Önceki</button>
                <span>{{ page.pageNumber }}. sayfa · {{ page.totalCount }} ödeme</span>
                <button type="button" [disabled]="page.pageNumber * page.pageSize >= page.totalCount" (click)="loadPayments(page.pageNumber + 1)">Sonraki</button>
              </div>
            }
          }
        </section>
      }
    </section>
  `
})
export class StudentFeeDetailComponent implements OnInit {
  private readonly api = inject(CoachingFinanceService);
  private readonly feeId = inject(ActivatedRoute).snapshot.paramMap.get('feeId') ?? '';
  readonly fee = signal<CoachingFee | null>(null);
  readonly payments = signal<CoachingPaymentPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private readonly currency = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try { this.fee.set(await firstValueFrom(this.api.fee('student', '', this.feeId))); await this.loadPayments(1); }
    catch { this.error.set('Ücret bulunamadı ya da yüklenemedi.'); }
    finally { this.loading.set(false); }
  }

  async loadPayments(page: number): Promise<void> {
    try { this.payments.set(await firstValueFrom(this.api.payments('student', '', this.feeId, page))); }
    catch { this.error.set('Ödemeler yüklenemedi. Yeniden dene.'); }
  }

  money(value: number): string { return this.currency.format(value); }
}
