import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuditLogsService } from '../../../core/services/audit-logs.service';
import { AuditLog, AuditLogFilters } from '../../../core/models/audit-log.model';
import { AuditLogDetailDialogComponent } from './audit-log-detail-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';
import { catchError, takeUntil, finalize } from 'rxjs/operators';
import { switchMap } from 'rxjs/operators';
import { EMPTY, Subject } from 'rxjs';

@Component({
  selector: 'app-audit-logs-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatDialogModule,
    MatTooltipModule
  ],
  templateUrl: './audit-logs-list.component.html',
  styleUrls: ['./audit-logs-list.component.scss']
})
export class AuditLogsListComponent extends BaseComponent implements OnInit {
  private auditLogsService = inject(AuditLogsService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  logs: AuditLog[] = [];
  actions: string[] = [];
  entityTypes: string[] = [];
  displayedColumns = ['timestamp', 'userName', 'action', 'entity', 'ipAddress', 'actions'];

  totalCount = 0;
  pageSize = 50;
  pageNumber = 1;
  loadError: string | null = null;
  loadWarning: string | null = null;
  failedServices: string[] = [];
  filterOptionsError: string | null = null;
  private readonly reloadLogs$ = new Subject<void>();

  filters: AuditLogFilters = {
    pageNumber: 1,
    pageSize: 50
  };

  // Action translations
  private actionLabels: { [key: string]: string } = {
    'create': 'Oluştur',
    'update': 'Güncelle',
    'delete': 'Sil',
    'login': 'Giriş',
    'logout': 'Çıkış',
    'view': 'Görüntüle',
    'export': 'Dışa Aktar',
    'import': 'İçe Aktar'
  };

  ngOnInit(): void {
    this.loadFilterOptions();
    this.reloadLogs$
      .pipe(
        takeUntil(this.destroy$),
        switchMap(() => {
          this.loading.set(true);
          this.loadError = null;
          this.loadWarning = null;
          this.failedServices = [];
          return this.auditLogsService.getAuditLogs({ ...this.filters }).pipe(
            takeUntil(this.destroy$),
            finalize(() => this.loading.set(false)),
            catchError(error => {
              this.logs = [];
              this.totalCount = 0;
              this.loadError = 'Aktivite logları yüklenemedi';
              this.loadWarning = null;
              this.failedServices = [];
              this.handleError(error, this.loadError);
              return EMPTY;
            })
          );
        })
      )
      .subscribe({
        next: response => {
          this.logs = response.logs;
          this.totalCount = response.totalCount;
          this.pageNumber = response.pageNumber;
          this.pageSize = response.pageSize;
          this.loadWarning = response.warning;
          this.failedServices = response.failedServices;
        }
      });
    this.loadLogs();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  getActionLabel(action: string): string {
    return this.actionLabels[action] || this.actionLabels[action.toLowerCase()] || action;
  }

  getActionClass(action: string): string {
    const actionLower = action.toLowerCase();
    return `action-${actionLower}`;
  }

  getActionIcon(action: string): string {
    const icons: { [key: string]: string } = {
      'create': 'add_circle',
      'update': 'edit',
      'delete': 'delete',
      'login': 'login',
      'logout': 'logout',
      'view': 'visibility',
      'export': 'file_download',
      'import': 'file_upload'
    };
    return icons[action.toLowerCase()] || 'info';
  }

  loadLogs(): void {
    this.reloadLogs$.next();
  }

  loadFilterOptions(): void {
    this.filterOptionsError = null;
    this.auditLogsService.getAuditFilterOptions()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: options => {
          this.actions = options.actions;
          this.entityTypes = options.entityTypes;
        },
        error: err => {
          this.filterOptionsError = 'Audit filtre seçenekleri yüklenemedi';
          this.handleError(err, this.filterOptionsError);
        }
      });
  }

  applyFilters(): void {
    this.filters.pageNumber = 1;
    this.loadLogs();
  }

  clearFilters(): void {
    this.filters = {
      pageNumber: 1,
      pageSize: this.pageSize
    };
    this.loadLogs();
  }

  onPageChange(event: PageEvent): void {
    this.filters.pageNumber = event.pageIndex + 1;
    this.filters.pageSize = event.pageSize;
    this.loadLogs();
  }

  viewDetail(log: AuditLog): void {
    this.dialog.open(AuditLogDetailDialogComponent, {
      width: '900px',
      data: { log }
    });
  }

  exportLogs(): void {
    this.auditLogsService.exportAuditLogs(this.filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `aktivite-loglari-${new Date().toISOString()}.csv`;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);
          this.handleSuccess('Aktivite logları başarıyla dışa aktarıldı');
        },
        error: (err) => {
          console.error('Error exporting logs:', err);
          this.handleError(err, this.getExportErrorMessage(err));
        }
      });
  }

  private getExportErrorMessage(error: any): string {
    const message = [
      typeof error === 'string' ? error : '',
      typeof error?.message === 'string' ? error.message : '',
      typeof error?.error === 'string' ? error.error : '',
      typeof error?.error?.message === 'string' ? error.error.message : ''
    ].join(' ').toLowerCase();

    if (
      message.includes('100.000')
      || message.includes('100,000')
      || message.includes('100000')
      || message.includes('güvenli dışa aktarma sınırını aşıyor')
    ) {
      return 'Dışa aktarma 100.000 kayıtla sınırlıdır. Lütfen filtreleri daraltıp tekrar deneyin.';
    }

    if (
      message.includes('audit servislerinden veri alınamadı')
      || message.includes('audit services unavailable')
      || (message.includes('audit') && message.includes('service unavailable'))
    ) {
      return 'Audit servisleri şu anda kullanılamıyor. Lütfen daha sonra tekrar deneyin.';
    }

    return 'Dışa aktarma işlemi sırasında hata oluştu';
  }
}
