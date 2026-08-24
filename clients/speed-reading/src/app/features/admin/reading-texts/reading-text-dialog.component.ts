import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { takeUntil, finalize } from 'rxjs/operators';
import { ReadingTextsService } from '../../../core/services/reading-texts.service';
import { ReadingText } from '../../../core/models/reading-text.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-reading-text-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatAutocompleteModule
  ],
  templateUrl: './reading-text-dialog.component.html',
  styleUrls: ['./reading-text-dialog.component.scss']
})
export class ReadingTextDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ReadingTextsService);
  // toaster inherited from BaseComponent

  form: FormGroup;
  isEditMode = false;
  saving = false;
  categories: string[] = [];
  levels = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  calculatedWordCount = 0;
  estimatedMinutes = 0;

  constructor(
    public dialogRef: MatDialogRef<ReadingTextDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ReadingText | null
  ) {
    super();
    this.isEditMode = !!data;

    this.form = this.fb.group({
      title: [data?.title || '', Validators.required],
      content: [data?.content || '', Validators.required],
      category: [data?.category || '', Validators.required],
      newCategory: [''],
      difficultyLevel: [data?.difficultyLevel || 1, Validators.required],
      language: [data?.language || 'tr'],
      isActive: [data?.isActive ?? true]
    });

    if (data?.content) {
      this.calculateWordCount();
    }
  }

  ngOnInit() {
    this.loadCategories();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadCategories() {
    this.service.getCategories()
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

  calculateWordCount() {
    const content = this.form.get('content')?.value || '';
    const words = content.trim().split(/\s+/).filter((word: string) => word.length > 0);
    this.calculatedWordCount = words.length;
    // Assuming average reading speed of 200 WPM
    this.estimatedMinutes = Math.ceil(this.calculatedWordCount / 200);
  }

  onCancel() {
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.form.invalid) {
      this.toaster.warning('Lütfen tüm zorunlu alanları doldurun');
      return;
    }

    this.saving = true;
    const formValue = this.form.value;

    // Yeni kategori seçildiyse, newCategory değerini kullan
    const finalCategory = formValue.category === '__new__'
      ? formValue.newCategory
      : formValue.category;

    // Yeni kategori seçildi ama isim girilmediyse hata ver
    if (formValue.category === '__new__' && !formValue.newCategory) {
      this.toaster.warning('Lütfen yeni kategori adını girin');
      this.saving = false;
      return;
    }

    const dto = {
      title: formValue.title,
      content: formValue.content,
      category: finalCategory,
      difficultyLevel: formValue.difficultyLevel,
      language: formValue.language,
      isActive: formValue.isActive
    };

    const operation = this.isEditMode
      ? this.service.updateText(this.data!.id, dto)
      : this.service.createText(dto);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Metin güncellendi' : 'Metin oluşturuldu'
          );
          this.dialogRef.close(true);
        },
        error: (error) => {
          this.handleError(error, 'Kaydetme sırasında hata oluştu');
        }
      });
  }
}
