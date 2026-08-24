import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatDividerModule } from '@angular/material/divider';
import { takeUntil, finalize } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-age-group-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatDividerModule
  ],
  templateUrl: './age-group-dialog.component.html',
  styleUrls: ['./age-group-dialog.component.scss']
})
export class AgeGroupDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private ageGroupService = inject(AgeGroupConfigurationService);
  // toaster inherited from BaseComponent

  ageGroupForm: FormGroup;
  isEditMode = false;
  saving = false;
  existingGroups: AgeGroupConfiguration[] = [];

  constructor(
    public dialogRef: MatDialogRef<AgeGroupDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AgeGroupConfiguration | null
  ) {
    super();
    this.isEditMode = !!data;
    // Note: Form creation moved to ngOnInit to wait for data fetching if possible, 
    // or we fetch data then update validators. 
    // For better UX, we'll initialize form first, then fetch data and update validators.
    this.ageGroupForm = this.createForm();

    // Configure dialog to be wider
    this.dialogRef.updateSize('800px');
  }

  ngOnInit() {
    this.loadExistingGroups();
  }

  loadExistingGroups() {
    this.loading.set(true);
    // Fetch all groups to check against
    this.ageGroupService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.existingGroups = groups;
          this.updateValidators();
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Failed to load existing groups for validation', err);
          this.loading.set(false);
        }
      });
  }

  updateValidators() {
    // Update Name Validator
    const nameControl = this.ageGroupForm.get('name');
    if (nameControl) {
      nameControl.addValidators(this.uniqueNameValidator());
      nameControl.updateValueAndValidity();
    }

    // Update Age Validators
    // We attach the validator to the form group or individual controls?
    // Overlap depends on both min and max, so it's often better on the group or we allow standard cross-field validation.
    // Here we'll add a validator to minAge and maxAge that checks the whole form state or use a group validator.
    // Let's use individual validators that check the other field's value.
    const minAgeControl = this.ageGroupForm.get('minAge');
    const maxAgeControl = this.ageGroupForm.get('maxAge');

    if (minAgeControl) {
      minAgeControl.addValidators(this.ageOverlapValidator());
      minAgeControl.updateValueAndValidity();
    }
    if (maxAgeControl) {
      maxAgeControl.addValidators(this.ageOverlapValidator());
      maxAgeControl.updateValueAndValidity();
    }

    // Re-validate when either changes
    minAgeControl?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => maxAgeControl?.updateValueAndValidity({ emitEvent: false }));
    maxAgeControl?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => minAgeControl?.updateValueAndValidity({ emitEvent: false }));
  }

  uniqueNameValidator(): any {
    return (control: any) => {
      if (!control.value || !this.existingGroups.length) return null;

      const value = control.value.toLowerCase();
      // Check if any *other* group has this name
      const exists = this.existingGroups.some(g =>
        g.name.toLowerCase() === value &&
        (!this.isEditMode || g.id !== this.data?.id)
      );

      return exists ? { nameConflict: true } : null;
    };
  }

  ageOverlapValidator(): any {
    return (control: any) => {
      // We need both min and max to check overlap.
      // Since this validator is on a control, we can access the parent or just use this.ageGroupForm if available.
      if (!this.ageGroupForm || !this.existingGroups.length) return null;

      const minVal = this.ageGroupForm.get('minAge')?.value;
      const maxVal = this.ageGroupForm.get('maxAge')?.value;

      // Treat null maxAge as Infinity (150 in our logic or actual infinity)
      // Note: Logic must match backend: (request.MinAge <= existing.MaxAge) && (request.MaxAge >= existing.MinAge)
      const currentMin = minVal !== null && minVal !== undefined ? minVal : 0;
      const currentMax = maxVal !== null && maxVal !== undefined ? maxVal : Number.MAX_VALUE;

      // Don't validate if basic requirements aren't met
      if (currentMin > currentMax && maxVal !== null) return null;

      // Check overlap with other ACTIVE groups
      const overlapGroup = this.existingGroups.find(g => {
        // Skip self
        if (this.isEditMode && g.id === this.data?.id) return false;
        // Skip inactive groups (backend rule: "Where(x => x.IsActive ...)")
        if (!g.isActive) return false;

        const otherMin = g.minAge;
        const otherMax = g.maxAge ?? Number.MAX_VALUE;

        // Overlap formula: StartA <= EndB && EndA >= StartB
        const overlaps = (currentMin <= otherMax) && (currentMax >= otherMin);
        return overlaps;
      });

      if (overlapGroup) {
        return { ageOverlap: { groupName: overlapGroup.displayName } };
      }

      return null;
    };
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  createForm(): FormGroup {
    const formConfig: any = {
      name: [
        this.data?.name || '',
        [
          Validators.required,
          Validators.minLength(2),
          Validators.maxLength(50),
          Validators.pattern(/^[a-z0-9_-]+$/)
        ]
      ],
      displayName: [
        this.data?.displayName || '',
        [Validators.required, Validators.minLength(2), Validators.maxLength(100)]
      ],
      description: [
        this.data?.description || '',
        [Validators.maxLength(500)]
      ],
      minAge: [
        this.data?.minAge ?? null,
        [Validators.required, Validators.min(1), Validators.max(150)]
      ],
      maxAge: [
        this.data?.maxAge ?? null,
        [Validators.min(1), Validators.max(150)]
      ],
      recommendedWPM: [
        this.data?.recommendedWPM ?? 200,
        [Validators.required, Validators.min(50), Validators.max(2000)]
      ],
      minWPM: [
        this.data?.minWPM ?? 100,
        [Validators.required, Validators.min(50), Validators.max(2000)]
      ],
      maxWPM: [
        this.data?.maxWPM ?? 400,
        [Validators.required, Validators.min(50), Validators.max(2000)]
      ],
      recommendedComprehension: [
        this.data?.recommendedComprehension ?? 70,
        [Validators.required, Validators.min(0), Validators.max(100)]
      ],
      recommendedDailyMinutes: [
        this.data?.recommendedDailyMinutes ?? 20,
        [Validators.required, Validators.min(1), Validators.max(300)]
      ],
      defaultDifficultyLevel: [
        this.data?.defaultDifficultyLevel ?? 1,
        [Validators.required, Validators.min(1), Validators.max(5)]
      ],
      orderIndex: [
        this.data?.orderIndex ?? 0,
        [Validators.required, Validators.min(1), Validators.max(100)]
      ]
    };

    // Add isActive field only for edit mode
    if (this.isEditMode) {
      formConfig.isActive = [this.data?.isActive || false];
    }

    return this.fb.group(formConfig);
  }

  async onCancel() {
    if (this.ageGroupForm.dirty) {
      const confirmed = await this.confirm(
        'Değişiklikler kaydedilmedi. Çıkmak istediğinizden emin misiniz?'
      );
      if (!confirmed) {
        return;
      }
    }
    this.dialogRef.close(false);
  }

  private getFieldNameTurkish(field: string): string {
    const fieldNames: { [key: string]: string } = {
      'Name': 'Kod Adı',
      'DisplayName': 'Görünen Ad',
      'Description': 'Açıklama',
      'MinAge': 'Minimum Yaş',
      'MaxAge': 'Maximum Yaş',
      'RecommendedWPM': 'Önerilen Hız',
      'RecommendedComprehension': 'Önerilen Kavrama',
      'RecommendedDailyMinutes': 'Günlük Süre',
      'DefaultDifficultyLevel': 'Zorluk Seviyesi',
      'OrderIndex': 'Sıralama',
      'IsActive': 'Durum'
    };
    return fieldNames[field] || field;
  }

  onSave() {
    if (this.ageGroupForm.invalid) {
      Object.keys(this.ageGroupForm.controls).forEach(key => {
        this.ageGroupForm.get(key)?.markAsTouched();
      });
      this.toaster.error('Lütfen formdaki tüm zorunlu alanları doldurun ve hataları düzeltin.', 4000);
      return;
    }

    this.saving = true;
    const formValue = this.ageGroupForm.value;

    const request$: Observable<any> = this.isEditMode
      ? this.ageGroupService.update(this.data!.id, {
        id: this.data!.id,
        name: formValue.name,
        displayName: formValue.displayName,
        description: formValue.description || null,
        minAge: formValue.minAge,
        maxAge: formValue.maxAge || null,
        recommendedWPM: formValue.recommendedWPM,
        minWPM: formValue.minWPM,
        maxWPM: formValue.maxWPM,
        recommendedComprehension: formValue.recommendedComprehension,
        recommendedDailyMinutes: formValue.recommendedDailyMinutes,
        defaultDifficultyLevel: formValue.defaultDifficultyLevel,
        orderIndex: formValue.orderIndex,
        isActive: formValue.isActive
      })
      : this.ageGroupService.create({
        name: formValue.name,
        displayName: formValue.displayName,
        description: formValue.description || null,
        minAge: formValue.minAge,
        maxAge: formValue.maxAge || null,
        recommendedWPM: formValue.recommendedWPM,
        minWPM: formValue.minWPM,
        maxWPM: formValue.maxWPM,
        recommendedComprehension: formValue.recommendedComprehension,
        recommendedDailyMinutes: formValue.recommendedDailyMinutes,
        defaultDifficultyLevel: formValue.defaultDifficultyLevel,
        orderIndex: formValue.orderIndex,
        isActive: true
      });

    request$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(this.isEditMode ? 'Yaş grubu başarıyla güncellendi' : 'Yaş grubu başarıyla oluşturuldu');
          this.dialogRef.close(true);
        },
        error: (error: any) => {
          if (!this.handleValidationErrors(error)) {
            this.handleError(error, this.isEditMode ? 'Yaş grubu güncellenirken bir hata oluştu.' : 'Yaş grubu oluşturulurken bir hata oluştu.');
          }
        }
      });
  }

  private handleValidationErrors(error: any): boolean {
    // Case 1: Field-specific validation errors (FluentValidation style)
    if (error.status === 400 && error.error && error.error.errors) {
      const validationErrors = error.error.errors;
      let hasFieldErrors = false;

      Object.keys(validationErrors).forEach(key => {
        // Backend keys are usually PascalCase (e.g. RecommendedWPM), form is camelCase (e.g. recommendedWPM)
        // We try to match by lowercasing the first letter
        const formControlName = key.charAt(0).toLowerCase() + key.slice(1);
        const control = this.ageGroupForm.get(formControlName);

        if (control) {
          const errorMessage = validationErrors[key][0]; // Take the first error
          control.setErrors({ serverError: errorMessage });
          control.markAsTouched();
          hasFieldErrors = true;
        }
      });

      if (hasFieldErrors) {
        this.toaster.error('Lütfen formdaki hatalı alanları düzeltin.', 4000);
        return true;
      }
    }

    // Case 2: General validation exception (e.g. "Age overlap") sent as message
    if (error.status === 400 && error.error && !error.error.errors && error.error.message) {
      this.toaster.error(error.error.message, 5000);
      return true;
    }

    return false;
  }
}
