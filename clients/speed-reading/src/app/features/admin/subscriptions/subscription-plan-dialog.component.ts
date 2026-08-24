import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { MatChipInputEvent } from '@angular/material/chips';
import { finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { SubscriptionService, SubscriptionPlan, Product } from '../../../core/services/subscription.service';

@Component({
  selector: 'app-subscription-plan-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './subscription-plan-dialog.component.html',
  styleUrls: ['./subscription-plan-dialog.component.scss']
})
export class SubscriptionPlanDialogComponent extends BaseComponent implements OnInit {
  private fb       = inject(FormBuilder);
  private service  = inject(SubscriptionService);
  dialogRef        = inject(MatDialogRef<SubscriptionPlanDialogComponent>);
  data: SubscriptionPlan | null = inject(MAT_DIALOG_DATA);

  readonly separatorKeysCodes = [ENTER, COMMA] as const;
  features: string[] = [];
  products: Product[] = [];
  isEditMode = false;
  step = 1;

  form = this.fb.group({
    name:          ['', [Validators.required]],
    description:   ['', [Validators.required]],
    slug:          ['', [Validators.required]],
    productId:     ['', [Validators.required]],
    price:         [0, [Validators.required, Validators.min(0)]],
    billingPeriod: ['Monthly', [Validators.required]],
    durationDays:  [null as number | null],
    isActive:      [true],
    isPublic:      [true],
    sortOrder:     [0, [Validators.required]],
  });

  billingPeriods = [
    { value: 'Monthly',   label: 'Aylık'     },
    { value: 'Quarterly', label: 'Üç Aylık'  },
    { value: 'Annual',    label: 'Yıllık'    },
    { value: 'Lifetime',  label: 'Ömür Boyu' },
  ];

  ngOnInit(): void {
    this.isEditMode = !!this.data;

    // Ürünleri yükle
    this.service.getAllProducts()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: products => this.products = products });

    if (this.data) {
      this.form.patchValue({
        name:          this.data.name,
        description:   this.data.description,
        slug:          this.data.slug,
        productId:     this.data.productId,
        price:         this.data.price,
        billingPeriod: this.data.billingPeriod,
        durationDays:  this.data.durationDays,
        isActive:      this.data.isActive,
        isPublic:      this.data.isPublic,
        sortOrder:     this.data.sortOrder,
      });
      this.features = this.data.features ? [...this.data.features] : [];
    }

    // Lifetime → durationDays devre dışı
    this.form.get('billingPeriod')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(period => {
        const ctrl = this.form.get('durationDays')!;
        if (period === 'Lifetime') { ctrl.setValue(null); ctrl.disable(); }
        else ctrl.enable();
      });

    if (this.data?.billingPeriod === 'Lifetime')
      this.form.get('durationDays')!.disable();

    // Auto-slug (sadece create mode)
    if (!this.isEditMode) {
      this.form.get('name')!.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(name => {
          const slugCtrl = this.form.get('slug')!;
          if (!slugCtrl.dirty || !slugCtrl.value) {
            slugCtrl.setValue(
              (name || '').toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, ''),
              { emitEvent: false }
            );
          }
        });
    }
  }

  // ── Step navigation ───────────────────────────────────────────────
  canGoToStep2(): boolean {
    const { name, description, slug, productId } = this.form.controls;
    return name.valid && description.valid && slug.valid && productId.valid;
  }

  goToStep2(): void {
    if (this.canGoToStep2()) this.step = 2;
  }

  // ── Features ─────────────────────────────────────────────────────
  addFeature(event: MatChipInputEvent): void {
    const value = (event.value || '').trim();
    if (value) this.features.push(value);
    event.chipInput!.clear();
  }

  removeFeature(feature: string): void {
    const idx = this.features.indexOf(feature);
    if (idx >= 0) this.features.splice(idx, 1);
  }

  getProductIcon(slug: string): string {
    const icons: Record<string, string> = { hizliokuma: 'speed', kocluk: 'psychology', combo: 'all_inclusive' };
    return icons[slug] || 'inventory_2';
  }

  // ── Save ──────────────────────────────────────────────────────────
  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const val = this.form.getRawValue();
    const request = {
      name:          val.name!,
      description:   val.description!,
      slug:          val.slug!,
      productId:     val.productId!,
      price:         val.price || 0,
      billingPeriod: val.billingPeriod!,
      durationDays:  val.billingPeriod === 'Lifetime' ? null : val.durationDays,
      isActive:      val.isActive ?? true,
      isPublic:      val.isPublic ?? true,
      sortOrder:     val.sortOrder || 0,
      features:      this.features.length > 0 ? this.features : null,
    };

    this.loading.set(true);
    const op = this.isEditMode
      ? this.service.updatePlan(this.data!.id, request)
      : this.service.createPlan(request);

    op.pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next:  () => {
          this.toaster.success(this.isEditMode ? 'Plan güncellendi' : 'Plan oluşturuldu');
          this.dialogRef.close(true);
        },
        error: () => this.toaster.error('İşlem sırasında hata oluştu')
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
