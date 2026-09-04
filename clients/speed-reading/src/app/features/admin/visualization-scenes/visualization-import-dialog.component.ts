import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Subject } from 'rxjs';
import { takeUntil, finalize } from 'rxjs/operators';
import { VisualizationAdminService, ImportResult } from '../../../core/services/visualization-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
    selector: 'app-visualization-import-dialog',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatProgressBarModule
    ],
    template: `
        <h2 mat-dialog-title>CSV'den Sahne İçe Aktar</h2>
        
        <mat-dialog-content>
            <div class="import-container">
                <!-- File Upload Area -->
                <div class="upload-area" 
                     [class.drag-over]="isDragOver"
                     (dragover)="onDragOver($event)"
                     (dragleave)="isDragOver = false"
                     (drop)="onDrop($event)"
                     (click)="fileInput.click()">
                    <input #fileInput type="file" accept=".csv" hidden (change)="onFileSelect($event)">
                    
                    <mat-icon>cloud_upload</mat-icon>
                    <p>CSV dosyasını buraya sürükleyin veya tıklayın</p>
                    <span class="hint">Maksimum 10MB, UTF-8 kodlamalı CSV</span>
                </div>

                <!-- Selected File -->
                <div *ngIf="selectedFile" class="selected-file">
                    <mat-icon>description</mat-icon>
                    <span>{{ selectedFile.name }}</span>
                    <span class="file-size">({{ (selectedFile.size / 1024).toFixed(1) }} KB)</span>
                    <button mat-icon-button type="button" aria-label="Seçili dosyayı kaldır" (click)="clearFile()">
                        <mat-icon>close</mat-icon>
                    </button>
                </div>

                <!-- Loading -->
                <div *ngIf="loading" class="loading">
                    <mat-spinner diameter="40"></mat-spinner>
                    <p>İçe aktarılıyor...</p>
                </div>

                <!-- Result -->
                <div *ngIf="result" class="result" [class.success]="result.failedCount === 0">
                    <mat-icon>{{ result.failedCount === 0 ? 'check_circle' : 'warning' }}</mat-icon>
                    <div class="result-details">
                        <p class="result-title">{{ result.message }}</p>
                        <p *ngIf="result.failedCount > 0" class="result-errors">
                            {{ result.failedCount }} satır başarısız oldu
                        </p>
                        <ul *ngIf="result.errors.length > 0" class="error-list">
                            <li *ngFor="let error of result.errors.slice(0, 5)">{{ error }}</li>
                            <li *ngIf="result.errors.length > 5">... ve {{ result.errors.length - 5 }} hata daha</li>
                        </ul>
                    </div>
                </div>

                <!-- CSV Format Info -->
                <div class="format-info">
                    <h4>CSV Formatı</h4>
                    <p>CSV dosyanız aşağıdaki sütunları içermelidir:</p>
                    <table class="format-table">
                        <tr>
                            <td><code>ExerciseId</code></td>
                            <td>Bağlı egzersizin GUID değeri (zorunlu)</td>
                        </tr>
                        <tr>
                            <td><code>Description</code></td>
                            <td>Sahne açıklaması (zorunlu)</td>
                        </tr>
                        <tr>
                            <td><code>Duration</code></td>
                            <td>Süre (saniye)</td>
                        </tr>
                        <tr>
                            <td><code>DifficultyLevel</code></td>
                            <td>Zorluk (1-3)</td>
                        </tr>
                        <tr>
                            <td><code>DisplayOrder</code></td>
                            <td>Görüntüleme sırası</td>
                        </tr>
                        <tr>
                            <td><code>Q1, A1, O1</code></td>
                            <td>1. Soru, Cevap, Şıklar (| ile ayrılmış)</td>
                        </tr>
                        <tr>
                            <td><code>Q2, A2, O2 ... Q5</code></td>
                            <td>2-5. sorular (opsiyonel)</td>
                        </tr>
                    </table>
                </div>
            </div>
        </mat-dialog-content>

        <mat-dialog-actions align="end">
            <button mat-button mat-dialog-close>{{ result ? 'Kapat' : 'İptal' }}</button>
            <button mat-raised-button color="primary" 
                    [disabled]="!selectedFile || loading"
                    (click)="import()">
                <mat-icon>upload</mat-icon>
                İçe Aktar
            </button>
        </mat-dialog-actions>
    `,
    styles: [`
        mat-dialog-content {
            min-width: 500px;
        }
        .import-container {
            padding: 16px 0;
        }
        .upload-area {
            border: 2px dashed #ccc;
            border-radius: 12px;
            padding: 48px;
            text-align: center;
            cursor: pointer;
            transition: all 0.2s;
            background: #fafafa;
            
            &:hover, &.drag-over {
                border-color: #6a1b9a;
                background: #f3e5f5;
            }
            
            mat-icon {
                font-size: 48px;
                width: 48px;
                height: 48px;
                color: #9c27b0;
            }
            
            p {
                margin: 16px 0 8px;
                font-size: 16px;
                color: #333;
            }
            
            .hint {
                font-size: 12px;
                color: #999;
            }
        }
        .selected-file {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 12px 16px;
            background: #e8f5e9;
            border-radius: 8px;
            margin-top: 16px;
            
            mat-icon {
                color: #4caf50;
            }
            
            .file-size {
                color: #666;
                font-size: 12px;
            }
            
            button {
                margin-left: auto;
            }
        }
        .loading {
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 32px;
            gap: 16px;
        }
        .result {
            display: flex;
            gap: 16px;
            padding: 16px;
            border-radius: 8px;
            margin-top: 16px;
            background: #fff3e0;
            
            &.success {
                background: #e8f5e9;
                mat-icon { color: #4caf50; }
            }
            
            mat-icon {
                font-size: 32px;
                width: 32px;
                height: 32px;
                color: #ff9800;
            }
            
            .result-title {
                font-weight: 500;
                margin: 0 0 8px;
            }
            
            .result-errors {
                color: #f44336;
                margin: 0;
            }
            
            .error-list {
                margin: 8px 0 0;
                padding-left: 20px;
                font-size: 12px;
                color: #666;
            }
        }
        .format-info {
            margin-top: 24px;
            padding: 16px;
            background: #f5f5f5;
            border-radius: 8px;
            
            h4 {
                margin: 0 0 8px;
                font-size: 14px;
            }
            
            p {
                margin: 0 0 12px;
                font-size: 12px;
                color: #666;
            }
        }
        .format-table {
            width: 100%;
            font-size: 12px;
            
            td {
                padding: 4px 8px;
                border-bottom: 1px solid #eee;
                
                &:first-child {
                    width: 150px;
                }
            }
            
            code {
                background: #e0e0e0;
                padding: 2px 6px;
                border-radius: 4px;
            }
        }
        mat-dialog-actions button mat-icon {
            margin-right: 8px;
        }
    `]
})
export class VisualizationImportDialogComponent implements OnDestroy {
    private destroy$ = new Subject<void>();

    selectedFile: File | null = null;
    isDragOver = false;
    loading = false;
    result: ImportResult | null = null;

    constructor(
        private dialogRef: MatDialogRef<VisualizationImportDialogComponent>,
        private service: VisualizationAdminService,
        private toaster: ToasterService
    ) { }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    onDragOver(event: DragEvent): void {
        event.preventDefault();
        this.isDragOver = true;
    }

    onDrop(event: DragEvent): void {
        event.preventDefault();
        this.isDragOver = false;

        const files = event.dataTransfer?.files;
        if (files && files.length > 0) {
            this.selectFile(files[0]);
        }
    }

    onFileSelect(event: Event): void {
        const input = event.target as HTMLInputElement;
        if (input.files && input.files.length > 0) {
            this.selectFile(input.files[0]);
        }
    }

    selectFile(file: File): void {
        if (!file.name.endsWith('.csv')) {
            this.toaster.error('Sadece CSV dosyaları kabul edilir');
            return;
        }
        if (file.size > 10 * 1024 * 1024) {
            this.toaster.error('Dosya boyutu 10MB\'dan büyük olamaz');
            return;
        }
        this.selectedFile = file;
        this.result = null;
    }

    clearFile(): void {
        this.selectedFile = null;
        this.result = null;
    }

    import(): void {
        if (!this.selectedFile) return;

        this.loading = true;
        this.service.importFromCsv(this.selectedFile)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading = false)
            )
            .subscribe({
                next: (result) => {
                    this.result = result;
                    if (result.successCount > 0) {
                        this.toaster.success(`${result.successCount} sahne başarıyla içe aktarıldı`);
                    }
                },
                error: () => this.toaster.error('İçe aktarma başarısız oldu')
            });
    }
}
