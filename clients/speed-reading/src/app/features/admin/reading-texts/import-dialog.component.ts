import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { ReadingTextsService } from '../../../core/services/reading-texts.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

interface ParsedText {
  title: string;
  content: string;
  category: string;
  level: number;
  questionCount: number;
  status: 'pending' | 'success' | 'error';
  error?: string;
}

@Component({
  selector: 'app-import-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTabsModule
  ],
  templateUrl: './import-dialog.component.html',
  styleUrls: ['./import-dialog.component.scss']
})
export class ImportDialogComponent extends BaseComponent {
  private service = inject(ReadingTextsService);
  // toaster inherited from BaseComponent

  selectedFile: File | null = null;
  fileType: 'csv' | 'excel' = 'csv';
  parsedTexts: ParsedText[] = [];
  previewColumns = ['status', 'title', 'category', 'level', 'questions', 'message'];
  importComplete = false;
  successCount = 0;
  errorCount = 0;

  constructor(public dialogRef: MatDialogRef<ImportDialogComponent>) {
    super();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  onFileSelected(event: any, type: 'csv' | 'excel') {
    const file = event.target.files[0];
    if (file) {
      this.selectedFile = file;
      this.fileType = type;
    }
  }

  clearFile() {
    this.selectedFile = null;
    this.parsedTexts = [];
    this.importComplete = false;
    this.successCount = 0;
    this.errorCount = 0;
  }

  importTexts() {
    if (!this.selectedFile) return;

    this.loading.set(true);

    const operation = this.fileType === 'csv'
      ? this.service.importFromCsv(this.selectedFile)
      : this.service.importFromExcel(this.selectedFile);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (result) => {
          this.successCount = result.successCount || this.parsedTexts.length;
          this.errorCount = result.errorCount || 0;
          this.importComplete = true;

          // Update statuses
          this.parsedTexts.forEach((text, i) => {
            text.status = i < this.successCount ? 'success' : 'error';
            if (text.status === 'error') {
              text.error = result.errors?.[i] || 'Bilinmeyen hata';
            }
          });

          this.handleSuccess(`${this.successCount} metin başarıyla içe aktarıldı`);
        },
        error: (error) => {
          this.handleError(error, 'İçe aktarma sırasında hata oluştu');
        }
      });
  }

  onClose() {
    this.dialogRef.close(this.importComplete);
  }
}
