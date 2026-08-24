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
import { SubscriptionService, Product } from '../../../core/services/subscription.service';
import { ProductDialogComponent } from './product-dialog.component';

@Component({
  selector: 'app-products-list',
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
    MatTooltipModule,
  ],
  templateUrl: './products-list.component.html',
  styleUrls: ['./products-list.component.scss']
})
export class ProductsListComponent extends BaseComponent implements OnInit {
  private service = inject(SubscriptionService);
  private dialog  = inject(MatDialog);

  products: Product[] = [];
  displayedColumns = ['name', 'slug', 'bundle', 'isActive', 'isPublic', 'sortOrder', 'actions'];

  ngOnInit(): void { this.loadProducts(); }

  loadProducts(): void {
    this.loading.set(true);
    this.service.getAllProducts()
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next:  products => { this.products = products; },
        error: () => this.toaster.error('Platformlar yüklenirken bir hata oluştu')
      });
  }

  openDialog(product?: Product): void {
    const dialogRef = this.dialog.open(ProductDialogComponent, {
      width: '520px',
      maxWidth: '96vw',
      maxHeight: '92vh',
      panelClass: 'product-dialog-panel',
      data: product || null,
      disableClose: false
    });
    dialogRef.afterClosed().subscribe(result => { if (result) this.loadProducts(); });
  }

  async toggleActive(product: Product): Promise<void> {
    const action = product.isActive ? 'devre dışı bırakılsın' : 'aktif edilsin';
    const confirmed = await this.toaster.confirm(`"${product.name}" platformu ${action} mı?`);
    if (!confirmed) return;

    this.service.updateProduct(product.id, { isActive: !product.isActive })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next:  () => {
          this.toaster.success(product.isActive ? 'Platform devre dışı bırakıldı' : 'Platform aktif edildi');
          this.loadProducts();
        },
        error: () => this.toaster.error('İşlem sırasında hata oluştu')
      });
  }

  async deactivateProduct(product: Product): Promise<void> {
    const confirmed = await this.toaster.confirm(`"${product.name}" platformunu devre dışı bırakmak istediğinizden emin misiniz?`);
    if (!confirmed) return;

    this.service.deactivateProduct(product.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next:  () => { this.toaster.success('Platform devre dışı bırakıldı'); this.loadProducts(); },
        error: (err) => this.toaster.error(err?.error?.message || 'Platform devre dışı bırakılırken hata oluştu')
      });
  }

  getProductIcon(slug: string): string {
    const icons: Record<string, string> = { hizliokuma: 'speed', kocluk: 'psychology', combo: 'all_inclusive' };
    return icons[slug] || 'inventory_2';
  }

  getProductColor(slug: string): string {
    const colors: Record<string, string> = { hizliokuma: '#1d4ed8', kocluk: '#7c3aed', combo: '#0369a1' };
    return colors[slug] || '#374151';
  }
}
