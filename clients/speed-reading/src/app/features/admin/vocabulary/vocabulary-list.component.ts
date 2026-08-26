import { Component, OnInit, inject, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { takeUntil, finalize } from 'rxjs/operators';
import { VocabularyService, VocabularyItem, ImportVocabularyResult } from '../../../core/services/vocabulary.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { VocabularyDialogComponent } from './vocabulary-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-vocabulary-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './vocabulary-list.component.html',
  styleUrls: ['./vocabulary-list.component.scss']
})
export class VocabularyListComponent extends BaseComponent implements OnInit {
  private vocabularyService = inject(VocabularyService);
  public ageGroupService = inject(AgeGroupConfigurationService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  vocabularyItems: VocabularyItem[] = [];
  displayedColumns = ['word', 'definition', 'category', 'difficulty', 'ageGroup', 'actions'];
  categories: string[] = [];
  categoriesWithCount: { name: string; count: number }[] = [];
  ageGroups: AgeGroupConfiguration[] = [];
  // loading inherited from BaseComponent
  importing = false;

  // Pagination
  pageNumber = 1;
  pageSize = 10;
  totalCount = 0;

  filters = {
    search: '',
    category: '',
    difficulty: undefined as number | undefined,
    ageGroupId: ''
  };

  ngOnInit() {
    this.loadCategories();
    this.loadAgeGroups();
    this.loadVocabulary();
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
          this.updateCategoriesWithCount();
        },
        error: (error) => {
          console.error('Error loading categories:', error);
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
          console.error('Error loading age groups:', error);
        }
      });
  }

  updateCategoriesWithCount() {
    this.categoriesWithCount = this.categories.map(name => ({
      name,
      count: this.vocabularyItems.filter(item => item.category === name).length
    }));
  }

  loadVocabulary() {
    this.loading.set(true);
    this.vocabularyService.getAllVocabularyItems(
      this.filters.search || undefined,
      this.filters.category || undefined,
      this.filters.difficulty,
      this.filters.ageGroupId || undefined,
      this.pageNumber,
      this.pageSize
    )
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (result) => {
          this.vocabularyItems = result.items;
          this.totalCount = result.totalCount;
          this.updateCategoriesWithCount();
        },
        error: (error) => {
          this.handleError(error, 'Kelimeler yüklenirken hata oluştu');
        }
      });
  }

  onFilterChange() {
    this.pageNumber = 1; // Reset to first page on filter change
    this.loadVocabulary();
  }

  onPageChange(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadVocabulary();
  }

  clearFilters() {
    this.filters = {
      search: '',
      category: '',
      difficulty: undefined,
      ageGroupId: ''
    };
    this.pageNumber = 1;
    this.loadVocabulary();
  }

  hasActiveFilters(): boolean {
    return !!(
      this.filters.search ||
      this.filters.category ||
      this.filters.difficulty !== undefined ||
      this.filters.ageGroupId
    );
  }

  openVocabularyDialog(item?: VocabularyItem) {
    const dialogRef = this.dialog.open(VocabularyDialogComponent, {
      width: '1200px',
      maxHeight: '95vh',
      data: item || null
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadCategories();
        this.loadVocabulary();
      }
    });
  }

  async deleteVocabulary(item: VocabularyItem) {
    const confirmed = await this.confirm(`"${item.word}" kelimesini silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`);
    if (confirmed) {
      this.vocabularyService.deleteVocabularyItem(item.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Kelime silindi');
            this.loadCategories();
            this.loadVocabulary();
          },
          error: (error) => {
            this.handleError(error, 'Kelime silinirken hata oluştu');
          }
        });
    }
  }

  getDifficultyLabel(level: number): string {
    switch (level) {
      case 1: return 'Başlangıç';
      case 2: return 'Temel';
      case 3: return 'Orta';
      case 4: return 'İleri';
      case 5: return 'Uzman';
      default: return `Seviye ${level}`;
    }
  }

  getDifficultyColor(level: number): string {
    switch (level) {
      case 1: return '#4caf50'; // Green
      case 2: return '#8bc34a'; // Light Green
      case 3: return '#ff9800'; // Orange
      case 4: return '#ff5722'; // Deep Orange
      case 5: return '#f44336'; // Red
      default: return '#9e9e9e';
    }
  }

  getAgeGroupLabel(ageGroupId?: string): string {
    if (!ageGroupId) return 'Bilinmeyen';
    const group = this.ageGroups.find(g => g.id === ageGroupId);
    return group ? group.displayName : 'Bilinmeyen';
  }

  // Import/Export methods
  onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (!file) return;

    // Validate file type
    const fileName = file.name.toLowerCase();
    if (!fileName.endsWith('.csv')) {
      this.handleError(new Error('Invalid file type'), 'Sadece CSV dosyaları desteklenmektedir');
      return;
    }

    this.importing = true;
    this.vocabularyService.importVocabulary(file)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.importing = false)
      )
      .subscribe({
        next: (result: ImportVocabularyResult) => {
          this.showImportResult(result);
          if (result.successCount > 0) {
            this.loadCategories();
            this.loadVocabulary();
          }
          // Reset file input
          if (this.fileInput) {
            this.fileInput.nativeElement.value = '';
          }
        },
        error: (error) => {
          this.handleError(error, 'Dosya içe aktarılırken hata oluştu');
          // Reset file input
          if (this.fileInput) {
            this.fileInput.nativeElement.value = '';
          }
        }
      });
  }

  showImportResult(result: ImportVocabularyResult) {
    const totalProcessed = result.successCount + result.failureCount;
    let message = `Toplam ${totalProcessed} satır işlendi.\n`;
    message += `Başarılı: ${result.successCount}\n`;
    message += `Başarısız: ${result.failureCount}`;

    if (result.errors.length > 0) {
      message += '\n\nHatalar:\n';
      message += result.errors.slice(0, 10).join('\n');
      if (result.errors.length > 10) {
        message += `\n... ve ${result.errors.length - 10} hata daha`;
      }
    }

    if (result.successCount > 0 && result.failureCount === 0) {
      this.handleSuccess(`Tüm kelimeler başarıyla içe aktarıldı! (${result.successCount} kelime)`);
    } else if (result.successCount > 0 && result.failureCount > 0) {
      this.handleError(new Error(message), message); // Using handleError for warning/mixed result
    } else {
      this.handleError(new Error(message), message);
    }
  }

  exportVocabulary() {
    this.vocabularyService.exportVocabulary()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          this.downloadFile(blob, `vocabulary_export_${this.getDateString()}.csv`);
          this.handleSuccess('Kelimeler başarıyla dışa aktarıldı');
        },
        error: (error) => {
          this.handleError(error, 'Kelimeler dışa aktarılırken hata oluştu');
        }
      });
  }

  exportFiltered() {
    const category = this.filters.category || undefined;
    const difficultyLevel = this.filters.difficulty;
    const ageGroupId = this.filters.ageGroupId || undefined;

    this.vocabularyService.exportVocabulary(category, difficultyLevel, ageGroupId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          this.downloadFile(blob, `vocabulary_filtered_export_${this.getDateString()}.csv`);
          this.handleSuccess('Filtrelenmiş kelimeler başarıyla dışa aktarıldı');
        },
        error: (error) => {
          this.handleError(error, 'Kelimeler dışa aktarılırken hata oluştu');
        }
      });
  }

  downloadTemplate() {
    this.vocabularyService.downloadTemplate()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          this.downloadFile(blob, 'vocabulary_import_template.csv');
          this.handleSuccess('Şablon dosyası indirildi');
        },
        error: (error) => {
          this.handleError(error, 'Şablon indirilirken hata oluştu');
        }
      });
  }

  private downloadFile(blob: Blob, filename: string) {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  }

  private getDateString(): string {
    const now = new Date();
    return now.toISOString().split('T')[0].replace(/-/g, '');
  }
}
