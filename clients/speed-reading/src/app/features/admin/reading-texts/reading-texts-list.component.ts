import { Component, OnInit, inject } from '@angular/core';
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
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { takeUntil, finalize } from 'rxjs/operators';
import { ReadingTextsService } from '../../../core/services/reading-texts.service';
import { ReadingText } from '../../../core/models/reading-text.model';
import { ReadingTextDialogComponent } from './reading-text-dialog.component';
import { QuestionsDialogComponent } from './questions-dialog.component';
import { ImportDialogComponent } from './import-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-reading-texts-list',
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
    MatCheckboxModule,
    MatSlideToggleModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatPaginatorModule,
    MatMenuModule
  ],
  templateUrl: './reading-texts-list.component.html',
  styleUrls: ['./reading-texts-list.component.scss']
})
export class ReadingTextsListComponent extends BaseComponent implements OnInit {
  private service = inject(ReadingTextsService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  readingTexts: ReadingText[] = [];
  paginatedTexts: ReadingText[] = [];
  displayedColumns = ['index', 'title', 'category', 'level', 'questions', 'status', 'actions'];
  categories: string[] = [];
  categoriesWithCount: { name: string; count: number }[] = [];

  // New filter data sources
  levels: number[] = [];
  ageGroups: { id: string, name: string, displayName: string }[] = [];

  pageNumber = 1;
  pageSize = 10;

  filters = {
    search: '',
    category: '',
    level: undefined as number | undefined,
    ageGroupId: '',
    activeOnly: false
  };

  ngOnInit() {
    this.loadFilterData();
    this.loadTexts();
  }

  loadFilterData() {
    // Load existing categories
    this.service.getCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (categories) => {
          this.categories = categories;
          this.updateCategoriesWithCount();
        },
        error: (error) => console.error('Error loading categories:', error)
      });

    // Load dynamic levels
    this.service.getLevels()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (levels) => this.levels = levels,
        error: (error) => console.error('Error loading levels:', error)
      });

    // Load active age groups
    this.service.getAgeGroups()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => this.ageGroups = groups,
        error: (error) => console.error('Error loading age groups:', error)
      });
  }

  updateCategoriesWithCount() {
    this.categoriesWithCount = this.categories.map(name => ({
      name,
      count: this.readingTexts.filter(t => t.category === name).length
    }));
  }

  loadTexts() {
    this.loading.set(true);
    this.service.getAllTexts(
      this.filters.category || undefined,
      this.filters.level,
      this.filters.ageGroupId || undefined,
      this.filters.activeOnly ? true : undefined,
      this.filters.search || undefined
    )
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (texts) => {
          this.readingTexts = texts;
          this.updateCategoriesWithCount();
          this.updatePagination();
        },
        error: (error) => {
          this.toaster.error('Metinler yüklenirken bir hata oluştu');
        }
      });
  }

  updatePagination() {
    const startIndex = (this.pageNumber - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedTexts = this.readingTexts.slice(startIndex, endIndex);
  }

  onPageChange(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.updatePagination();
  }

  onFilterChange() {
    this.loadTexts();
  }

  clearFilters() {
    this.filters = {
      search: '',
      category: '',
      level: undefined,
      ageGroupId: '',
      activeOnly: false
    };
    this.loadTexts();
  }

  hasActiveFilters(): boolean {
    return !!(
      this.filters.search ||
      this.filters.category ||
      this.filters.level !== undefined ||
      this.filters.ageGroupId ||
      this.filters.activeOnly
    );
  }

  openTextDialog(text?: ReadingText) {
    // If editing, fetch full text data first
    if (text) {
      this.service.getTextById(text.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (fullText) => {
            this.openDialogWithData(fullText);
          },
          error: (error) => {
            this.handleError(error, 'Metin yüklenirken hata oluştu');
          }
        });
    } else {
      // Creating new text
      this.openDialogWithData(undefined);
    }
  }

  private openDialogWithData(data?: ReadingText) {
    const dialogRef = this.dialog.open(ReadingTextDialogComponent, {
      width: '95vw',
      maxWidth: '1200px',
      maxHeight: '95vh',
      data
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTexts();
      }
    });
  }

  openQuestionsDialog(text: ReadingText) {
    // Fetch full text data with questions
    this.service.getTextWithQuestions(text.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (fullTextWithQuestions) => {
          const dialogRef = this.dialog.open(QuestionsDialogComponent, {
            width: '98vw',
            maxWidth: '1600px',
            maxHeight: '95vh',
            data: fullTextWithQuestions
          });

          dialogRef.afterClosed().subscribe(result => {
            if (result) {
              this.loadTexts();
            }
          });
        },
        error: (error) => {
          this.handleError(error, 'Metin yüklenirken hata oluştu');
        }
      });
  }

  toggleActive(text: ReadingText) {
    const previousState = text.isActive;
    text.isActive = !text.isActive; // Optimistic update

    this.service.updateStatus(text.id, text.isActive)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: () => {
          this.handleSuccess(`Metin ${text.isActive ? 'aktif' : 'pasif'} edildi`);
        },
        error: (error) => {
          text.isActive = previousState; // Rollback on error
          this.toaster.error('Durum değiştirilirken bir hata oluştu. Lütfen tekrar deneyin.');
        }
      });
  }

  openImportDialog() {
    const dialogRef = this.dialog.open(ImportDialogComponent, {
      width: '95vw',
      maxWidth: '1000px',
      maxHeight: '95vh'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadTexts();
      }
    });
  }

  async deleteText(text: ReadingText) {
    const confirmed = await this.confirm(`"${text.title}" metnini silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.`);
    if (confirmed) {
      this.service.deleteText(text.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Metin silindi');
            this.loadTexts();
          },
          error: (error) => {
            this.toaster.error('Metin silinirken bir hata oluştu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  getLevelColor(level: number): string {
    if (level <= 3) return '#4caf50';
    if (level <= 6) return '#ff9800';
    return '#f44336';
  }

  // Export Methods
  exportToPdf(text: ReadingText) {
    this.loading.set(true);
    this.service.exportToPdf(text.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          this.service.downloadFile(blob, `${text.title}.pdf`);
          this.handleSuccess('PDF başarıyla indirildi');
        },
        error: (error) => {
          this.handleError(error, 'PDF oluşturulurken hata oluştu');
        }
      });
  }

  exportToDocx(text: ReadingText) {
    this.loading.set(true);
    this.service.exportToDocx(text.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          this.service.downloadFile(blob, `${text.title}.docx`);
          this.handleSuccess('DOCX başarıyla indirildi');
        },
        error: (error) => {
          this.handleError(error, 'DOCX oluşturulurken hata oluştu');
        }
      });
  }

  exportAllToPdf() {
    if (this.readingTexts.length === 0) {
      this.toaster.error('İndirilecek metin bulunamadı.');
      return;
    }

    this.loading.set(true);
    const ids = this.readingTexts.map(t => t.id);
    this.service.exportMultipleToPdf(ids)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          const fileName = `OkumaMetinleri_${new Date().toISOString().slice(0, 10)}.pdf`;
          this.service.downloadFile(blob, fileName);
          this.handleSuccess(`${this.readingTexts.length} metin PDF olarak indirildi`);
        },
        error: (error) => {
          this.handleError(error, 'PDF oluşturulurken hata oluştu');
        }
      });
  }

  exportAllToDocx() {
    if (this.readingTexts.length === 0) {
      this.toaster.error('İndirilecek metin bulunamadı.');
      return;
    }

    this.loading.set(true);
    const ids = this.readingTexts.map(t => t.id);
    this.service.exportMultipleToDocx(ids)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (blob) => {
          const fileName = `OkumaMetinleri_${new Date().toISOString().slice(0, 10)}.docx`;
          this.service.downloadFile(blob, fileName);
          this.handleSuccess(`${this.readingTexts.length} metin DOCX olarak indirildi`);
        },
        error: (error) => {
          this.handleError(error, 'DOCX oluşturulurken hata oluştu');
        }
      });
  }
}
