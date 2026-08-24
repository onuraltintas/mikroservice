import { Component, OnInit, OnDestroy, signal } from '@angular/core';
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
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntil, finalize } from 'rxjs/operators';
import { VisualizationAdminService, VisualizationScene } from '../../../core/services/visualization-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { VisualizationSceneDialogComponent } from './visualization-scene-dialog.component';
import { VisualizationImportDialogComponent } from './visualization-import-dialog.component';

@Component({
    selector: 'app-visualization-scenes-list',
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
        MatTooltipModule,
        MatProgressSpinnerModule,
        MatPaginatorModule,
        MatMenuModule
    ],
    templateUrl: './visualization-scenes-list.component.html',
    styleUrls: ['./visualization-scenes-list.component.scss']
})
export class VisualizationScenesListComponent implements OnInit, OnDestroy {
    private destroy$ = new Subject<void>();
    private searchSubject = new Subject<string>();

    scenes: VisualizationScene[] = [];
    displayedColumns = ['description', 'difficultyLevel', 'duration', 'questionCount', 'createdAt', 'actions'];
    loading = signal(false);

    // Filters
    searchTerm = '';
    filterDifficulty?: number;

    // Pagination
    totalCount = 0;
    pageNumber = 1;
    pageSize = 10;

    difficultyLevels = [
        { value: 1, label: 'Seviye 1 - Temel' },
        { value: 2, label: 'Seviye 2 - Orta' },
        { value: 3, label: 'Seviye 3 - İleri' }
    ];

    constructor(
        private service: VisualizationAdminService,
        private dialog: MatDialog,
        private toaster: ToasterService
    ) {
        this.searchSubject.pipe(
            debounceTime(400),
            distinctUntilChanged(),
            takeUntil(this.destroy$)
        ).subscribe(() => {
            this.pageNumber = 1;
            this.loadScenes();
        });
    }

    ngOnInit(): void {
        this.loadScenes();
    }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    loadScenes(): void {
        this.loading.set(true);
        this.service.getScenes(this.pageNumber, this.pageSize, this.filterDifficulty, this.searchTerm)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (response) => {
                    this.scenes = response.items;
                    this.totalCount = response.totalCount;
                },
                error: (err) => {
                    console.error('Error loading scenes', err);
                    this.toaster.error('Sahneler yüklenirken hata oluştu');
                }
            });
    }

    onSearch(event: Event): void {
        const input = event.target as HTMLInputElement;
        this.searchTerm = input.value;
        this.searchSubject.next(this.searchTerm);
    }

    onFilterChange(): void {
        this.pageNumber = 1;
        this.loadScenes();
    }

    clearFilters(): void {
        this.searchTerm = '';
        this.filterDifficulty = undefined;
        this.pageNumber = 1;
        this.loadScenes();
    }

    onPageChange(event: PageEvent): void {
        this.pageNumber = event.pageIndex + 1;
        this.pageSize = event.pageSize;
        this.loadScenes();
    }

    openSceneDialog(scene?: VisualizationScene): void {
        const dialogRef = this.dialog.open(VisualizationSceneDialogComponent, {
            width: '1200px',
            maxWidth: '98vw',
            height: '90vh',
            data: { scene }
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.loadScenes();
            }
        });
    }

    openImportDialog(): void {
        const dialogRef = this.dialog.open(VisualizationImportDialogComponent, {
            width: '500px'
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.loadScenes();
            }
        });
    }

    async deleteScene(scene: VisualizationScene): Promise<void> {
        const confirmed = await this.toaster.confirm(
            `"${scene.description.substring(0, 50)}..." sahnesini silmek istediğinize emin misiniz?`,
            'Sahne Silinecek',
            'Sil',
            'İptal'
        );

        if (confirmed) {
            this.loading.set(true);
            this.service.deleteScene(scene.id)
                .pipe(
                    takeUntil(this.destroy$),
                    finalize(() => this.loading.set(false))
                )
                .subscribe({
                    next: () => {
                        this.toaster.success('Sahne başarıyla silindi');
                        this.loadScenes();
                    },
                    error: (err) => {
                        console.error('Error deleting scene', err);
                        this.toaster.error('Sahne silinirken hata oluştu');
                    }
                });
        }
    }

    // Export Methods
    exportToPdf(scene: VisualizationScene): void {
        this.loading.set(true);
        this.service.exportToPdf(scene.id)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (blob) => {
                    this.service.downloadFile(blob, `Sahne_${scene.difficultyLevel}_${scene.displayOrder}.pdf`);
                    this.toaster.success('PDF başarıyla indirildi');
                },
                error: () => this.toaster.error('PDF oluşturulurken hata oluştu')
            });
    }

    exportToDocx(scene: VisualizationScene): void {
        this.loading.set(true);
        this.service.exportToDocx(scene.id)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (blob) => {
                    this.service.downloadFile(blob, `Sahne_${scene.difficultyLevel}_${scene.displayOrder}.docx`);
                    this.toaster.success('DOCX başarıyla indirildi');
                },
                error: () => this.toaster.error('DOCX oluşturulurken hata oluştu')
            });
    }

    exportAllToPdf(): void {
        if (this.scenes.length === 0) return;

        this.loading.set(true);
        const ids = this.scenes.map(s => s.id);
        this.service.exportMultipleToPdf(ids)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (blob) => {
                    this.service.downloadFile(blob, `GorselSahneler_${new Date().toISOString().slice(0, 10)}.pdf`);
                    this.toaster.success(`${this.scenes.length} sahne PDF olarak indirildi`);
                },
                error: () => this.toaster.error('PDF oluşturulurken hata oluştu')
            });
    }

    exportAllToDocx(): void {
        if (this.scenes.length === 0) return;

        this.loading.set(true);
        const ids = this.scenes.map(s => s.id);
        this.service.exportMultipleToDocx(ids)
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (blob) => {
                    this.service.downloadFile(blob, `GorselSahneler_${new Date().toISOString().slice(0, 10)}.docx`);
                    this.toaster.success(`${this.scenes.length} sahne DOCX olarak indirildi`);
                },
                error: () => this.toaster.error('DOCX oluşturulurken hata oluştu')
            });
    }

    getDifficultyLabel(level: number): string {
        return this.difficultyLevels.find(d => d.value === level)?.label || `Seviye ${level}`;
    }

    getDifficultyColor(level: number): string {
        switch (level) {
            case 1: return '#4caf50';
            case 2: return '#ff9800';
            case 3: return '#f44336';
            default: return '#9e9e9e';
        }
    }

    getCountByDifficulty(level: number): number {
        return this.scenes.filter(s => s.difficultyLevel === level).length;
    }
}
