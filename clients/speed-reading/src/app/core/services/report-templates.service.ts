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
  private legacyApiUrl = `${environment.apiUrl}/v1/reporttemplates`;

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
    return this.http.get<ScheduledReport[]>(`${this.legacyApiUrl}/scheduled`);
  }

  getScheduledReportById(scheduleId: string): Observable<ScheduledReport> {
    return this.http.get<ScheduledReport>(`${this.legacyApiUrl}/scheduled/${scheduleId}`);
  }

  createScheduledReport(schedule: Omit<ScheduledReport, 'id' | 'lastRun' | 'nextRun'>): Observable<ScheduledReport> {
    return this.http.post<ScheduledReport>(`${this.legacyApiUrl}/scheduled`, schedule);
  }

  updateScheduledReport(scheduleId: string, schedule: Partial<ScheduledReport>): Observable<ScheduledReport> {
    return this.http.put<ScheduledReport>(`${this.legacyApiUrl}/scheduled/${scheduleId}`, schedule);
  }

  deleteScheduledReport(scheduleId: string): Observable<void> {
    return this.http.delete<void>(`${this.legacyApiUrl}/scheduled/${scheduleId}`);
  }

  toggleScheduledReport(scheduleId: string, isActive: boolean): Observable<ScheduledReport> {
    return this.http.patch<ScheduledReport>(`${this.legacyApiUrl}/scheduled/${scheduleId}/toggle`, { isActive });
  }

  // ==================== SNAPSHOTS ====================

  getSnapshots(reportType?: string): Observable<ReportSnapshot[]> {
    let params = new HttpParams();
    if (reportType) {
      params = params.set('reportType', reportType);
    }
    return this.http.get<ReportSnapshot[]>(`${this.legacyApiUrl}/snapshots`, { params });
  }

  getSnapshotById(snapshotId: string): Observable<ReportSnapshot> {
    return this.http.get<ReportSnapshot>(`${this.legacyApiUrl}/snapshots/${snapshotId}`);
  }

  createSnapshot(snapshot: Omit<ReportSnapshot, 'id' | 'generatedAt'>): Observable<ReportSnapshot> {
    return this.http.post<ReportSnapshot>(`${this.legacyApiUrl}/snapshots`, snapshot);
  }

  deleteSnapshot(snapshotId: string): Observable<void> {
    return this.http.delete<void>(`${this.legacyApiUrl}/snapshots/${snapshotId}`);
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
