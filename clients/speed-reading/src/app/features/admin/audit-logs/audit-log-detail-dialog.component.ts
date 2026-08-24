import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuditLogsService } from '../../../core/services/audit-logs.service';
import { AuditLogDetail } from '../../../core/models/audit-log.model';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-audit-log-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './audit-log-detail-dialog.component.html',
  styleUrls: ['./audit-log-detail-dialog.component.scss']
})
export class AuditLogDetailDialogComponent extends BaseComponent implements OnInit {
  private auditLogsService = inject(AuditLogsService);
  private dialogRef = inject(MatDialogRef<AuditLogDetailDialogComponent>);

  log: AuditLogDetail | null = null;
  // loading inherited from BaseComponent
  error: string | null = null;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { logId: string }) {
    super();
  }

  ngOnInit(): void {
    this.loadLogDetail();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadLogDetail(): void {
    this.loading.set(true);
    this.auditLogsService.getAuditLogDetail(this.data.logId)
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: (log) => {
          this.log = log;
        },
        error: (err) => {
          console.error('Error loading log detail:', err);
          this.error = 'Log detayı yüklenirken hata oluştu';
        }
      });
  }

  formatJson(jsonString: string): string {
    try {
      const obj = JSON.parse(jsonString);
      return JSON.stringify(obj, null, 2);
    } catch (e) {
      return jsonString;
    }
  }
}
