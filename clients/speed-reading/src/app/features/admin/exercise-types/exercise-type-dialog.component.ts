import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { takeUntil, finalize } from 'rxjs/operators';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { ExerciseType, ExerciseTypeCategory } from '../../../core/models/exercise.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-exercise-type-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatCheckboxModule
  ],
  templateUrl: './exercise-type-dialog.component.html',
  styleUrls: ['./exercise-type-dialog.component.scss']
})
export class ExerciseTypeDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private exerciseTypeService = inject(ExerciseTypeService);
  // toaster inherited from BaseComponent

  typeForm: FormGroup;
  isEditMode = false;
  saving = false;
  categories: ExerciseTypeCategory[] = [];

  constructor(
    public dialogRef: MatDialogRef<ExerciseTypeDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ExerciseType | null
  ) {
    super();
    this.isEditMode = !!data;
    this.typeForm = this.fb.group({
      name: [data?.name || '', Validators.required],
      displayName: [data?.displayName || '', Validators.required],
      description: [data?.description || ''],
      iconName: [data?.iconName || ''],
      colorCode: [data?.colorCode || '#4CAF50'],
      categoryId: [data?.categoryId || null],
      sortOrder: [data?.sortOrder || 0, Validators.required],
      isActive: [data?.isActive !== undefined ? data.isActive : true]
    });
  }

  ngOnInit() {
    this.loadCategories();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadCategories() {
    this.exerciseTypeService.getCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (categories) => {
          this.categories = categories;
        },
        error: (error) => {
          this.handleError(error, 'Kategoriler yüklenirken hata oluştu');
        }
      });
  }

  onCancel() {
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.typeForm.invalid) {
      return;
    }

    this.saving = true;
    const typeData = {
      ...this.typeForm.value
    };

    const operation = this.isEditMode
      ? this.exerciseTypeService.updateExerciseType(this.data!.id, typeData)
      : this.exerciseTypeService.createExerciseType(typeData);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Egzersiz tipi güncellendi' : 'Egzersiz tipi oluşturuldu'
          );
          this.dialogRef.close(true);
        },
        error: (error) => {
          this.handleError(error, 'Hata oluştu');
        }
      });
  }
}
