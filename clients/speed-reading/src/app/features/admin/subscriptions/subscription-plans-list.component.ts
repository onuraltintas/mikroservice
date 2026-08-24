import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { SubscriptionService, SubscriptionPlan } from '../../../core/services/subscription.service';
import { SubscriptionPlanDialogComponent } from './subscription-plan-dialog.component';

@Component({
  selector: 'app-subscription-plans-list',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './subscription-plans-list.component.html',
  styleUrls: ['./subscription-plans-list.component.scss']
})
export class SubscriptionPlansListComponent extends BaseComponent implements OnInit {
  private service = inject(SubscriptionService);
  private dialog  = inject(MatDialog);

  plans: SubscriptionPlan[] = [];
  displayedColumns = ['name', 'slug', 'product', 'price', 'billingPeriod', 'durationDays', 'isActive', 'isPublic', 'sortOrder', 'actions'];

  ngOnInit(): void { this.loadPlans(); }

  loadPlans(): void {
    this.loading.set(true);
    this.service.getAllPlans()
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: plans => { this.plans = plans; },
        error: () => this.toaster.error('Planlar yüklenirken bir hata oluştu')
      });
  }

  openDialog(plan?: SubscriptionPlan): void {
    const dialogRef = this.dialog.open(SubscriptionPlanDialogComponent, {
      width: '560px',
      maxWidth: '96vw',
      maxHeight: '92vh',
      panelClass: 'plan-dialog-panel',
      data: plan || null,
      disableClose: false
    });
    dialogRef.afterClosed().subscribe(result => { if (result) this.loadPlans(); });
  }

  async toggleActive(plan: SubscriptionPlan): Promise<void> {
    const action = plan.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.toaster.confirm(`"${plan.name}" planı ${action} mı?`);
    if (!confirmed) return;

    this.service.updatePlan(plan.id, { isActive: !plan.isActive })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next:  () => { this.toaster.success(plan.isActive ? 'Plan devre dışı bırakıldı' : 'Plan aktif edildi'); this.loadPlans(); },
        error: () => this.toaster.error('İşlem sırasında hata oluştu')
      });
  }

  async deactivatePlan(plan: SubscriptionPlan): Promise<void> {
    const confirmed = await this.toaster.confirm(`"${plan.name}" planını devre dışı bırakmak istediğinizden emin misiniz?`);
    if (!confirmed) return;

    this.service.deactivatePlan(plan.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next:  () => { this.toaster.success('Plan devre dışı bırakıldı'); this.loadPlans(); },
        error: () => this.toaster.error('Plan devre dışı bırakılırken hata oluştu')
      });
  }

  getBillingPeriodLabel(period: string): string {
    const labels: Record<string, string> = { Monthly: 'Aylık', Quarterly: 'Üç Aylık', Annual: 'Yıllık', Lifetime: 'Ömür Boyu' };
    return labels[period] || period;
  }

  getProductIcon(slug: string): string {
    const icons: Record<string, string> = { hizliokuma: 'speed', kocluk: 'psychology', combo: 'all_inclusive' };
    return icons[slug] || 'inventory_2';
  }

  getProductClass(slug: string): string {
    const classes: Record<string, string> = { hizliokuma: 'product-speed', kocluk: 'product-coaching', combo: 'product-combo' };
    return classes[slug] || 'product-default';
  }
}
