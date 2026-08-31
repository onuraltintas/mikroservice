import { Component, OnInit, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuditLog, AuditLogDetail } from '../../../core/models/audit-log.model';
import { BaseComponent } from '../../../core/components/base.component';

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
  log: AuditLogDetail | null = null;
  // loading inherited from BaseComponent
  error: string | null = null;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { log: AuditLog }) {
    super();
  }

  ngOnInit(): void {
    const source = this.data.log;
    this.log = {
      ...source,
      oldValues: null,
      newValues: source.changedFieldsJson ?? null,
      userAgent: source.userAgent ?? '',
      additionalInfo: [
        source.serviceName ? `Servis: ${source.serviceName}` : '',
        source.requestPath && source.httpMethod ? `İstek: ${source.httpMethod} ${source.requestPath}` : '',
        source.statusCode ? `HTTP durumu: ${source.statusCode}` : '',
        source.correlationId ? `Korelasyon: ${source.correlationId}` : ''
      ].filter(Boolean).join('\n') || null
    };
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
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
