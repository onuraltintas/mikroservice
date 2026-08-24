import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { SubscriptionService, Product } from '../../../core/services/subscription.service';

@Component({
  selector: 'app-product-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './product-dialog.component.html',
  styleUrls: ['./product-dialog.component.scss']
})
export class ProductDialogComponent extends BaseComponent implements OnInit {
  private fb      = inject(FormBuilder);
  private service  = inject(SubscriptionService);
  dialogRef        = inject(MatDialogRef<ProductDialogComponent>);
  data: Product | null = inject(MAT_DIALOG_DATA);

  isEditMode = false;
  /** Mevcut diğer ürünler — bundle seçimi için */
  otherProducts: Product[] = [];
  /** Seçili included product slug'ları */
  selectedSlugs: Set<string> = new Set();
  /** Template'de Array.from erişimi için */
  readonly Array = Array;

  form = this.fb.group({
    name:        ['', [Validators.required]],
    slug:        ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    description: [''],
    isActive:    [true],
    isPublic:    [true],
    sortOrder:   [0, [Validators.required, Validators.min(0)]],
  });

  ngOnInit(): void {
    this.isEditMode = !!this.data;

    // Tüm ürünleri yükle — bundle seçimi için (kendisi hariç)
    this.service.getAllProducts()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: products => {
          this.otherProducts = this.data
            ? products.filter(p => p.id !== this.data!.id)
            : products;
        }
      });

    if (this.data) {
      this.form.patchValue({
        name:        this.data.name,
        slug:        this.data.slug,
        description: this.data.description,
        isActive:    this.data.isActive,
        isPublic:    this.data.isPublic,
        sortOrder:   this.data.sortOrder,
      });
      this.selectedSlugs = new Set(this.data.includedProductSlugs);
      // Edit modda slug değiştirme
      this.form.get('slug')!.disable();
    }

    // Auto-slug sadece create modda
    if (!this.isEditMode) {
      this.form.get('name')!.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(name => {
          const slugCtrl = this.form.get('slug')!;
          if (!slugCtrl.dirty || !slugCtrl.value) {
            slugCtrl.setValue(
              (name || '').toLowerCase()
                .replace(/\s+/g, '-')
                .replace(/[^a-z0-9-]/g, '')
                .replace(/-+/g, '-')
                .replace(/^-|-$/g, ''),
              { emitEvent: false }
            );
          }
        });
    }
  }

  toggleIncluded(slug: string): void {
    if (this.selectedSlugs.has(slug)) this.selectedSlugs.delete(slug);
    else this.selectedSlugs.add(slug);
  }

  isIncluded(slug: string): boolean {
    return this.selectedSlugs.has(slug);
  }

  getProductIcon(slug: string): string {
    const icons: Record<string, string> = { hizliokuma: 'speed', kocluk: 'psychology' };
    return icons[slug] || 'inventory_2';
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const val = this.form.getRawValue();
    const includedProductSlugs = Array.from(this.selectedSlugs);

    this.loading.set(true);

    const op = this.isEditMode
      ? this.service.updateProduct(this.data!.id, {
          name:                 val.name ?? undefined,
          description:          val.description ?? undefined,
          includedProductSlugs,
          isActive:             val.isActive ?? undefined,
          isPublic:             val.isPublic ?? undefined,
          sortOrder:            val.sortOrder ?? undefined,
        })
      : this.service.createProduct({
          slug:                 val.slug!,
          name:                 val.name!,
          description:          val.description || '',
          includedProductSlugs,
          isActive:             val.isActive ?? true,
          isPublic:             val.isPublic ?? true,
          sortOrder:            val.sortOrder || 0,
        });

    op.pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next:  () => {
          this.toaster.success(this.isEditMode ? 'Platform güncellendi' : 'Platform oluşturuldu');
          this.dialogRef.close(true);
        },
        error: (err) => {
          const msg = err?.error?.message || 'İşlem sırasında hata oluştu';
          this.toaster.error(msg);
        }
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
