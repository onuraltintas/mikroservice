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
import { takeUntil, finalize } from 'rxjs/operators';

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

  filters: AuditLogFilters = {
    pageNumber: 1,
    pageSize: 50
  };

  // Action translations
  private actionLabels: { [key: string]: string } = {
    'Create': 'Oluştur',
    'Update': 'Güncelle',
    'Delete': 'Sil',
    'Login': 'Giriş',
    'Logout': 'Çıkış',
    'View': 'Görüntüle',
    'Export': 'Dışa Aktar',
    'Import': 'İçe Aktar'
  };

  ngOnInit(): void {
    this.loadLogs();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  getActionLabel(action: string): string {
    return this.actionLabels[action] || action;
  }

  getActionClass(action: string): string {
    const actionLower = action.toLowerCase();
    return `action-${actionLower}`;
  }

  getActionIcon(action: string): string {
    const icons: { [key: string]: string } = {
      'Create': 'add_circle',
      'Update': 'edit',
      'Delete': 'delete',
      'Login': 'login',
      'Logout': 'logout',
      'View': 'visibility',
      'Export': 'file_download',
      'Import': 'file_upload'
    };
    return icons[action] || 'info';
  }

  loadLogs(): void {
    this.loading.set(true);
    this.auditLogsService.getAuditLogs(this.filters)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (response) => {
          this.logs = response.logs;
          this.totalCount = response.totalCount;
          this.pageNumber = response.pageNumber;
          this.pageSize = response.pageSize;
          this.actions = [...new Set(this.logs.map(log => log.action))].sort();
          this.entityTypes = [...new Set(this.logs.map(log => log.entityType).filter((type): type is string => !!type))].sort();
        },
        error: (err) => {
          console.error('Error loading audit logs:', err);
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
          this.handleError(err, 'Dışa aktarma işlemi sırasında hata oluştu');
        }
      });
  }
}
