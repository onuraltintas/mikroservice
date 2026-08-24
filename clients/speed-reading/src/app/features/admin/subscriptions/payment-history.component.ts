import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { PaymentService, PaymentSummary } from '../../../core/services/payment.service';

@Component({
  selector: 'app-payment-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatPaginatorModule,
    MatTooltipModule,
    MatChipsModule,
  ],
  templateUrl: './payment-history.component.html',
  styleUrls: ['./payment-history.component.scss']
})
export class PaymentHistoryComponent extends BaseComponent implements OnInit {
  private service = inject(PaymentService);

  records: PaymentSummary[] = [];
  totalCount = 0;
  page      = 1;
  pageSize  = 20;

  searchControl = new FormControl('');
  statusControl = new FormControl('');

  displayedColumns = ['user', 'plan', 'amount', 'status', 'provider', 'subscriptionId', 'createdAt'];

  statusOptions = [
    { value: '',          label: 'Tümü'         },
    { value: 'Success',   label: 'Başarılı'      },
    { value: 'Pending',   label: 'Bekliyor'      },
    { value: 'Failed',    label: 'Başarısız'     },
    { value: 'Cancelled', label: 'İptal Edildi'  },
  ];

  ngOnInit(): void {
    this.load();

    this.searchControl.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.page = 1; this.load(); });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.page = 1; this.load(); });
  }

  load(): void {
    this.loading.set(true);
    this.service.getPayments(
      this.page,
      this.pageSize,
      this.statusControl.value || undefined,
      this.searchControl.value || undefined
    )
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next:  (res) => { this.records = res.items; this.totalCount = res.total; },
        error: ()    => this.toaster.error('Ödeme geçmişi yüklenirken hata oluştu')
      });
  }

  onPage(event: PageEvent): void {
    this.page     = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Success:   'status-success',
      Pending:   'status-pending',
      Failed:    'status-failed',
      Cancelled: 'status-cancelled',
    };
    return map[status] ?? '';
  }

  getStatusLabel(status: string): string {
    return this.statusOptions.find(s => s.value === status)?.label ?? status;
  }

  getStatusIcon(status: string): string {
    const map: Record<string, string> = {
      Success:   'check_circle',
      Pending:   'schedule',
      Failed:    'error',
      Cancelled: 'cancel',
    };
    return map[status] ?? 'help';
  }

  formatAmount(payment: PaymentSummary): string {
    return `${payment.amount.toLocaleString('tr-TR')} ${payment.currency}`;
  }

  getUserDisplay(p: PaymentSummary): string {
    return p.userName || p.userEmail || p.userId.substring(0, 8) + '…';
  }
}
