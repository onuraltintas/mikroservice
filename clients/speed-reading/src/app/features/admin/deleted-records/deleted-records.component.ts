import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { SelectionModel } from '@angular/cdk/collections';
import { forkJoin } from 'rxjs';
import { DeletedRecordsService } from '../../../services/deleted-records.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { DeletedRecord } from '../../../models/deleted-record.model';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
    selector: 'app-deleted-records',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatCardModule,
        MatTableModule,
        MatCheckboxModule,
        MatButtonModule,
        MatSelectModule,
        MatFormFieldModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatPaginatorModule,
        MatDialogModule
    ],
    templateUrl: './deleted-records.component.html',
    styleUrls: ['./deleted-records.component.scss']
})
export class DeletedRecordsComponent implements OnInit {
    @ViewChild(MatPaginator) paginator!: MatPaginator;

    records: DeletedRecord[] = [];
    loading = true;
    bulkDeleting = false;
    selectedEntityType = 'all';
    selection = new SelectionModel<DeletedRecord>(true, []);

    totalCount = 0;
    pageSize = 20;
    pageIndex = 0;
    pageSizeOptions = [10, 20, 50, 100];

    entityTypes: Array<{ value: string; label: string }> = [
        { value: 'all', label: 'Tümü' }
    ];
    private knownEntityTypes = new Set<string>();

    displayedColumns = ['select', 'entityType', 'displayName', 'deletedAt', 'deletedBy', 'actions'];

    constructor(
        private deletedRecordsService: DeletedRecordsService,
        private toaster: ToasterService,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadRecords();
    }

    loadRecords(): void {
        this.loading = true;
        this.selection.clear();

        const entityType = this.selectedEntityType === 'all' ? undefined : this.selectedEntityType;

        this.deletedRecordsService.getDeletedRecords({
            page: this.pageIndex + 1,
            pageSize: this.pageSize,
            entityType
        }).subscribe({
            next: (page) => {
                this.records = page.items;
                this.totalCount = page.totalCount;
                this.accumulateEntityTypes(page.items);
                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading deleted records:', err);
                this.toaster.error('Silinen kayıtlar yüklenirken hata oluştu');
                this.loading = false;
            }
        });
    }

    private accumulateEntityTypes(items: DeletedRecord[]): void {
        let changed = false;
        items.forEach(r => {
            if (r.entityType && !this.knownEntityTypes.has(r.entityType)) {
                this.knownEntityTypes.add(r.entityType);
                changed = true;
            }
        });
        if (changed) {
            this.entityTypes = [
                { value: 'all', label: 'Tümü' },
                ...[...this.knownEntityTypes].map(t => ({ value: t, label: t }))
            ];
        }
    }

    handlePageEvent(event: PageEvent): void {
        this.pageIndex = event.pageIndex;
        this.pageSize = event.pageSize;
        this.loadRecords();
    }

    onFilterChange(): void {
        this.pageIndex = 0;
        if (this.paginator) {
            this.paginator.firstPage();
        }
        this.loadRecords();
    }

    isAllSelected(): boolean {
        return this.records.length > 0 && this.selection.selected.length === this.records.length;
    }

    isIndeterminate(): boolean {
        return this.selection.selected.length > 0 && !this.isAllSelected();
    }

    toggleAllRows(): void {
        if (this.isAllSelected()) {
            this.selection.clear();
        } else {
            this.records.forEach(r => this.selection.select(r));
        }
    }

    bulkHardDelete(): void {
        const selected = this.selection.selected;
        if (selected.length === 0) return;

        const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
            data: {
                title: 'Toplu Kalıcı Silme',
                message: `UYARI: Seçili ${selected.length} kayıt kalıcı olarak silinecek ve geri alınamayacak. Devam etmek istediğinizden emin misiniz?`,
                confirmText: 'Kalıcı Sil',
                cancelText: 'İptal'
            }
        });

        dialogRef.afterClosed().subscribe(confirmed => {
            if (!confirmed) return;

            this.bulkDeleting = true;
            const requests = selected.map(record =>
                this.deletedRecordsService.hardDeleteRecord({ entityType: record.entityType, id: record.id })
            );

            forkJoin(requests).subscribe({
                next: () => {
                    this.toaster.success(`${selected.length} kayıt kalıcı olarak silindi`);
                    this.bulkDeleting = false;
                    this.loadRecords();
                },
                error: (err) => {
                    console.error('Bulk delete error:', err);
                    this.toaster.error('Toplu silme sırasında hata oluştu');
                    this.bulkDeleting = false;
                    this.loadRecords();
                }
            });
        });
    }

    restoreRecord(record: DeletedRecord): void {
        const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
            data: {
                title: 'Kaydı Geri Yükle',
                message: `"${record.displayName}" kaydını geri yüklemek istediğinizden emin misiniz?`,
                confirmText: 'Geri Yükle',
                cancelText: 'İptal'
            }
        });

        dialogRef.afterClosed().subscribe(confirmed => {
            if (confirmed) {
                this.deletedRecordsService.restoreRecord({
                    entityType: record.entityType,
                    id: record.id
                }).subscribe({
                    next: () => {
                        this.toaster.success('Kayıt başarıyla geri yüklendi');
                        this.loadRecords();
                    },
                    error: (err) => {
                        console.error('Error restoring record:', err);
                        this.toaster.error('Kayıt geri yüklenirken hata oluştu');
                    }
                });
            }
        });
    }

    hardDeleteRecord(record: DeletedRecord): void {
        const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
            data: {
                title: 'Kalıcı Olarak Sil',
                message: `UYARI: "${record.displayName}" kaydı kalıcı olarak silinecek ve geri alınamayacak. Devam etmek istediğinizden emin misiniz?`,
                confirmText: 'Kalıcı Sil',
                cancelText: 'İptal'
            }
        });

        dialogRef.afterClosed().subscribe(confirmed => {
            if (confirmed) {
                this.deletedRecordsService.hardDeleteRecord({
                    entityType: record.entityType,
                    id: record.id
                }).subscribe({
                    next: () => {
                        this.toaster.success('Kayıt kalıcı olarak silindi');
                        this.loadRecords();
                    },
                    error: (err) => {
                        console.error('Error hard deleting record:', err);
                        this.toaster.error('Kayıt silinirken hata oluştu');
                    }
                });
            }
        });
    }

    getEntityTypeLabel(type: string): string {
        const entity = this.entityTypes.find(e => e.value === type);
        return entity ? entity.label : type;
    }
}
