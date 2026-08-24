import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { takeUntil, finalize } from 'rxjs/operators';
import { VocabularyService, VocabularyItem } from '../../../core/services/vocabulary.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-vocabulary-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatAutocompleteModule
  ],
  templateUrl: './vocabulary-dialog.component.html',
  styleUrls: ['./vocabulary-dialog.component.scss']
})
export class VocabularyDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private vocabularyService = inject(VocabularyService);
  public ageGroupService = inject(AgeGroupConfigurationService);
  // toaster inherited from BaseComponent

  form: FormGroup;
  isEditMode = false;
  saving = false;
  categories: string[] = [];
  ageGroups: AgeGroupConfiguration[] = [];

  constructor(
    public dialogRef: MatDialogRef<VocabularyDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: VocabularyItem | null
  ) {
    super();
    this.isEditMode = !!data;

    this.form = this.fb.group({
      word: [data?.word || '', Validators.required],
      definition: [data?.definition || '', Validators.required],
      exampleSentence: [data?.exampleSentence || ''],
      synonyms: [data?.synonyms || ''],
      antonyms: [data?.antonyms || ''],
      category: [data?.category || '', Validators.required],
      newCategory: [''],
      difficultyLevel: [data?.difficultyLevel || 1, Validators.required],
      targetAgeGroupId: [data?.targetAgeGroupId || null, Validators.required]
    });
  }

  ngOnInit() {
    this.loadCategories();
    this.loadAgeGroups();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadCategories() {
    this.vocabularyService.getCategories()
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

  loadAgeGroups() {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (ageGroups) => {
          this.ageGroups = ageGroups;
        },
        error: (error) => {
          this.handleError(error, 'Yaş grupları yüklenirken hata oluştu');
        }
      });
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
      word: formValue.word,
      definition: formValue.definition,
      exampleSentence: formValue.exampleSentence || undefined,
      synonyms: formValue.synonyms || undefined,
      antonyms: formValue.antonyms || undefined,
      category: finalCategory,
      difficultyLevel: formValue.difficultyLevel,
      targetAgeGroupId: formValue.targetAgeGroupId
    };

    const operation = this.isEditMode
      ? this.vocabularyService.updateVocabularyItem(this.data!.id, dto)
      : this.vocabularyService.createVocabularyItem(dto);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Kelime güncellendi' : 'Kelime eklendi'
          );
          this.dialogRef.close(true);
        },
        error: (error) => {
          this.handleError(error, 'Kaydetme sırasında hata oluştu');
        }
      });
  }
}
