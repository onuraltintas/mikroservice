import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ReportTemplate,
  ScheduledReport,
  ReportSnapshot
} from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportTemplatesService {
  private http = inject(HttpClient);
  private reportsApiUrl = `${environment.speedReadingApiUrl}/reports`;

  // ==================== TEMPLATES ====================

  getTemplates(category?: 'student' | 'teacher' | 'admin'): Observable<ReportTemplate[]> {
    let params = new HttpParams();
    if (category) {
      params = params.set('type', this.capitalize(category));
    }
    return this.http.get<CentralReportTemplate[]>(`${this.reportsApiUrl}/templates`, { params })
      .pipe(map(items => items.map(item => this.toTemplate(item))));
  }

  getTemplateById(templateId: string): Observable<ReportTemplate> {
    return this.http.get<CentralReportTemplate>(`${this.reportsApiUrl}/templates/${templateId}`)
      .pipe(map(item => this.toTemplate(item)));
  }

  createTemplate(template: Omit<ReportTemplate, 'id' | 'createdAt' | 'updatedAt'>): Observable<ReportTemplate> {
    return this.http.post<CentralReportTemplate>(
      `${this.reportsApiUrl}/templates`,
      template,
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toTemplate(item)));
  }

  updateTemplate(templateId: string, template: Partial<ReportTemplate>): Observable<ReportTemplate> {
    return this.http.put<CentralReportTemplate>(
      `${this.reportsApiUrl}/templates/${templateId}`,
      {
        name: template.name ?? '',
        description: template.description ?? '',
        configurationJson: (template as any).configurationJson ?? '{}',
        isActive: template.isActive ?? true
      },
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toTemplate(item)));
  }

  deleteTemplate(templateId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.reportsApiUrl}/templates/${templateId}`,
      { headers: this.idempotencyHeaders() });
  }

  // ==================== SCHEDULED REPORTS ====================

  getScheduledReports(): Observable<ScheduledReport[]> {
    return this.http.get<CentralScheduledReport[]>(`${this.reportsApiUrl}/scheduled`, {
      params: new HttpParams().set('limit', 100)
    }).pipe(map(items => items.map(item => this.toScheduledReport(item))));
  }

  getScheduledReportById(scheduleId: string): Observable<ScheduledReport> {
    return this.http.get<CentralScheduledReport>(`${this.reportsApiUrl}/scheduled/${scheduleId}`)
      .pipe(map(item => this.toScheduledReport(item)));
  }

  createScheduledReport(schedule: Omit<ScheduledReport, 'id' | 'lastRun' | 'nextRun'>): Observable<ScheduledReport> {
    return this.http.post<CentralScheduledReport>(
      `${this.reportsApiUrl}/scheduled`,
      this.toScheduledRequest(schedule),
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toScheduledReport(item)));
  }

  updateScheduledReport(scheduleId: string, schedule: Partial<ScheduledReport>): Observable<ScheduledReport> {
    return this.http.put<CentralScheduledReport>(
      `${this.reportsApiUrl}/scheduled/${scheduleId}`,
      this.toScheduledRequest(schedule),
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toScheduledReport(item)));
  }

  deleteScheduledReport(scheduleId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.reportsApiUrl}/scheduled/${scheduleId}`,
      { headers: this.idempotencyHeaders() });
  }

  toggleScheduledReport(scheduleId: string, isActive: boolean): Observable<ScheduledReport> {
    return this.http.patch<CentralScheduledReport>(
      `${this.reportsApiUrl}/scheduled/${scheduleId}/status`,
      { isActive },
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toScheduledReport(item)));
  }

  // ==================== SNAPSHOTS ====================

  getSnapshots(reportType?: string): Observable<ReportSnapshot[]> {
    // Snapshot reads are centralized; generation remains a compatibility bridge
    // until the legacy report/export pipeline is migrated.
    return this.http.get<CentralReportSnapshot[]>(`${this.reportsApiUrl}/snapshots`, {
      params: new HttpParams().set('limit', 100)
    }).pipe(
      map(items => items
        .map(item => this.toSnapshot(item))
        .filter(item => !reportType || item.reportType === reportType))
    );
  }

  getSnapshotById(snapshotId: string): Observable<ReportSnapshot> {
    return this.http.get<CentralReportSnapshotDetail>(`${this.reportsApiUrl}/snapshots/${snapshotId}`)
      .pipe(map(item => this.toSnapshot(item)));
  }

  createSnapshot(snapshot: Omit<ReportSnapshot, 'id' | 'generatedAt'>): Observable<ReportSnapshot> {
    return this.http.post<CentralReportSnapshotDetail>(
      `${this.reportsApiUrl}/snapshots`,
      {
        reportTemplateId: snapshot.templateId,
        data: snapshot.data,
        reportStartDate: null,
        reportEndDate: null
      },
      { headers: this.idempotencyHeaders() })
      .pipe(map(item => this.toSnapshot(item)));
  }

  deleteSnapshot(snapshotId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.reportsApiUrl}/snapshots/${snapshotId}`,
      { headers: this.idempotencyHeaders() });
  }

  private idempotencyHeaders(): HttpHeaders {
    const key = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
    return new HttpHeaders({ 'Idempotency-Key': key });
  }

  private capitalize(value: string): string {
    return value.charAt(0).toUpperCase() + value.slice(1);
  }

  private toTemplate(item: CentralReportTemplate): ReportTemplate {
    let config: { metrics?: string[]; filters?: Record<string, unknown> } = {};
    try {
      config = JSON.parse(item.configurationJson || '{}');
    } catch {
      config = {};
    }

    return {
      id: item.id,
      name: item.name,
      description: item.description,
      reportType: item.type,
      category: item.category as ReportTemplate['category'],
      metrics: config.metrics ?? [],
      filters: config.filters ?? {},
      createdBy: item.createdByUserId ?? '',
      createdAt: new Date(item.createdAt),
      updatedAt: new Date(item.createdAt),
      isActive: item.isActive,
      configurationJson: item.configurationJson,
      type: item.type
    } as ReportTemplate;
  }

  private toScheduledRequest(schedule: ScheduleInput): Record<string, unknown> {
    const frequency = typeof schedule.frequency === 'number'
      ? schedule.frequency
      : ({ daily: 0, weekly: 1, monthly: 2 } as Record<string, number>)[String(schedule.frequency ?? 'daily').toLowerCase()] ?? 0;
    const deliveryTime = schedule.time ?? schedule.deliveryTime ?? '09:00:00';
    const recipients = Array.isArray(schedule.recipients)
      ? schedule.recipients.join(',')
      : (schedule.emailRecipients ?? null);
    const options = schedule.deliveryOptions ?? {};

    return {
      reportTemplateId: schedule.templateId ?? schedule.reportTemplateId,
      frequency,
      dayOfWeek: schedule.dayOfWeek ?? null,
      dayOfMonth: schedule.dayOfMonth ?? null,
      deliveryTime: String(deliveryTime).length === 5 ? `${deliveryTime}:00` : deliveryTime,
      isActive: schedule.isActive ?? true,
      sendEmail: schedule.sendEmail ?? options.sendEmail ?? true,
      saveToDashboard: schedule.saveToDashboard ?? options.saveToDashboard ?? true,
      emailRecipients: recipients
    };
  }

  private toScheduledReport(item: CentralScheduledReport): ScheduledReport {
    const frequency = typeof item.frequency === 'number'
      ? ({ 0: 'daily', 1: 'weekly', 2: 'monthly' } as Record<number, ScheduledReport['frequency']>)[item.frequency] ?? 'daily'
      : String(item.frequency).toLowerCase() as ScheduledReport['frequency'];
    const dayOfWeek = typeof item.dayOfWeek === 'number'
      ? item.dayOfWeek
      : item.dayOfWeek == null ? undefined : ({ sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6 } as Record<string, number>)[String(item.dayOfWeek).toLowerCase()];

    return {
      id: item.id,
      templateId: item.reportTemplateId,
      name: item.reportTemplateName,
      frequency,
      dayOfWeek,
      dayOfMonth: item.dayOfMonth ?? undefined,
      time: String(item.deliveryTime).slice(0, 5),
      recipients: item.emailRecipients ? item.emailRecipients.split(',').map(value => value.trim()).filter(Boolean) : [],
      isActive: item.isActive,
      lastRun: item.lastRunAt ? new Date(item.lastRunAt) : undefined,
      nextRun: item.nextRunAt ? new Date(item.nextRunAt) : new Date(0),
      deliveryOptions: {
        sendEmail: item.sendEmail,
        saveToDashboard: item.saveToDashboard
      }
    };
  }

  private toSnapshot(item: CentralReportSnapshot | CentralReportSnapshotDetail): ReportSnapshot {
    let data: unknown = {};
    if ('dataJson' in item && item.dataJson) {
      try {
        data = JSON.parse(item.dataJson);
      } catch {
        data = {};
      }
    }

    return {
      id: item.id,
      reportType: item.reportTemplateName,
      templateId: item.reportTemplateId,
      data,
      generatedAt: new Date(item.generatedAt),
      generatedBy: 'self'
    };
  }
}

interface CentralReportTemplate {
  id: string;
  name: string;
  description: string;
  type: string;
  category: string;
  configurationJson: string;
  isSystemTemplate: boolean;
  createdByUserId?: string;
  createdAt: string;
  isActive: boolean;
}

interface CentralScheduledReport {
  id: string;
  reportTemplateId: string;
  reportTemplateName: string;
  frequency: string | number;
  dayOfWeek?: string | number | null;
  dayOfMonth?: number | null;
  deliveryTime: string;
  isActive: boolean;
  lastRunAt?: string | null;
  nextRunAt?: string | null;
  successCount: number;
  failureCount: number;
  sendEmail: boolean;
  saveToDashboard: boolean;
  emailRecipients?: string | null;
}

interface CentralReportSnapshot {
  id: string;
  reportTemplateId: string;
  reportTemplateName: string;
  generatedAt: string;
  reportStartDate: string;
  reportEndDate: string;
  pdfFileUrl?: string | null;
  excelFileUrl?: string | null;
  isViewed: boolean;
  viewedAt?: string | null;
}

interface CentralReportSnapshotDetail extends CentralReportSnapshot {
  generatedForUserId: string;
  dataJson: string;
  dataJsonTruncated: boolean;
}

interface ScheduleInput {
  templateId?: string;
  reportTemplateId?: string;
  frequency?: ScheduledReport['frequency'] | number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  time?: string;
  deliveryTime?: string;
  isActive?: boolean;
  sendEmail?: boolean;
  saveToDashboard?: boolean;
  recipients?: string[];
  emailRecipients?: string | null;
  deliveryOptions?: {
    sendEmail?: boolean;
    saveToDashboard?: boolean;
  };
}
