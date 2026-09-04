import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { SubscriptionService, UserSubscription, UpdateSubscriptionRequest } from '../../../core/services/subscription.service';
import { AssignSubscriptionDialogComponent } from './assign-subscription-dialog.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';

@Component({
  selector: 'app-user-subscriptions-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatPaginatorModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './user-subscriptions-list.component.html',
  styleUrls: ['./user-subscriptions-list.component.scss']
})
export class UserSubscriptionsListComponent extends BaseComponent implements OnInit {
  private service = inject(SubscriptionService);
  private dialog = inject(MatDialog);

  records: UserSubscription[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 20;

  searchControl = new FormControl('');
  statusControl = new FormControl('');

  displayedColumns = ['user', 'plan', 'modules', 'status', 'startDate', 'endDate', 'isActive', 'actions'];

  statusOptions = [
    { value: '', label: 'Tümü' },
    { value: 'Active', label: 'Aktif' },
    { value: 'Trial', label: 'Deneme' },
    { value: 'Expired', label: 'Süresi Dolmuş' },
    { value: 'Cancelled', label: 'İptal Edildi' }
  ];

  ngOnInit(): void {
    this.loadSubscriptions();

    this.searchControl.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => { this.page = 1; this.loadSubscriptions(); });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => { this.page = 1; this.loadSubscriptions(); });
  }

  loadSubscriptions(): void {
    this.loading.set(true);
    this.service.getSubscriptions({
      search: this.searchControl.value || undefined,
      status: this.statusControl.value || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.records = result.items;
          this.totalCount = result.totalCount;
        },
        error: () => this.toaster.error('Abonelikler yüklenirken bir hata oluştu')
      });
  }

  onPage(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadSubscriptions();
  }

  openAssignDialog(): void {
    const dialogRef = this.dialog.open(AssignSubscriptionDialogComponent, {
      width: '540px',
      maxWidth: '96vw',
      maxHeight: '92vh',
      panelClass: 'assign-dialog-panel',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadSubscriptions();
      }
    });
  }

  async deleteSubscription(sub: UserSubscription): Promise<void> {
    const confirmed = await this.toaster.confirm(
      `"${sub.plan.name}" aboneliğini silmek istediğinizden emin misiniz?`
    );
    if (!confirmed) return;

    this.service.deleteSubscription(sub.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toaster.success('Abonelik silindi');
          this.loadSubscriptions();
        },
        error: () => this.toaster.error('Abonelik silinirken hata oluştu')
      });
  }

  getStatusLabel(status: string): string {
    return this.statusOptions.find(s => s.value === status)?.label ?? status;
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Active: 'status-active',
      Trial: 'status-trial',
      Expired: 'status-expired',
      Cancelled: 'status-cancelled'
    };
    return map[status] || '';
  }

  getModuleClass(module: string): string {
    return module === 'SpeedReading' ? 'module-speed' : 'module-coaching';
  }

  getModuleLabel(module: string): string {
    const labels: Record<string, string> = {
      SpeedReading: 'Hızlı Okuma',
      Coaching: 'Koçluk'
    };
    return labels[module] || module;
  }

  async extendSubscription(sub: UserSubscription): Promise<void> {
    const days = await this.toaster.prompt(`"${sub.plan.name}" aboneliğini kaç gün uzatmak istiyorsunuz?`, '30', { title: 'Aboneliği uzat', placeholder: 'Gün sayısı' });
    if (!days || isNaN(Number(days))) return;

    const currentEnd = sub.endDate ? new Date(sub.endDate) : new Date();
    currentEnd.setDate(currentEnd.getDate() + Number(days));
    const newEndDate = currentEnd.toISOString();

    this.service.updateSubscription(sub.id, { status: 'Active', endDate: newEndDate, notes: sub.notes })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toaster.success(`Abonelik ${days} gün uzatıldı`); this.loadSubscriptions(); },
        error: () => this.toaster.error('Uzatma işlemi başarısız')
      });
  }

  async cancelSubscription(sub: UserSubscription): Promise<void> {
    const confirmed = await this.toaster.confirm(`"${sub.plan.name}" aboneliğini iptal etmek istediğinizden emin misiniz?`);
    if (!confirmed) return;

    this.service.updateSubscription(sub.id, { status: 'Cancelled', endDate: sub.endDate, notes: sub.notes })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toaster.success('Abonelik iptal edildi'); this.loadSubscriptions(); },
        error: () => this.toaster.error('İptal işlemi başarısız')
      });
  }

  getUserDisplay(sub: UserSubscription): string {
    return sub.userName || sub.userEmail || sub.userId.substring(0, 8) + '...';
  }
}
