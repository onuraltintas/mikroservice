import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ReportTemplate,
  ScheduledReport,
  ReportSnapshot
} from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportTemplatesService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/reporttemplates`;

  // ==================== TEMPLATES ====================

  getTemplates(category?: 'student' | 'teacher' | 'admin'): Observable<ReportTemplate[]> {
    let params = new HttpParams();
    if (category) {
      params = params.set('category', category);
    }
    return this.http.get<ReportTemplate[]>(this.apiUrl, { params });
  }

  getTemplateById(templateId: string): Observable<ReportTemplate> {
    return this.http.get<ReportTemplate>(`${this.apiUrl}/${templateId}`);
  }

  createTemplate(template: Omit<ReportTemplate, 'id' | 'createdAt' | 'updatedAt'>): Observable<ReportTemplate> {
    return this.http.post<ReportTemplate>(this.apiUrl, template);
  }

  updateTemplate(templateId: string, template: Partial<ReportTemplate>): Observable<ReportTemplate> {
    return this.http.put<ReportTemplate>(`${this.apiUrl}/${templateId}`, template);
  }

  deleteTemplate(templateId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${templateId}`);
  }

  // ==================== SCHEDULED REPORTS ====================

  getScheduledReports(): Observable<ScheduledReport[]> {
    return this.http.get<ScheduledReport[]>(`${this.apiUrl}/scheduled`);
  }

  getScheduledReportById(scheduleId: string): Observable<ScheduledReport> {
    return this.http.get<ScheduledReport>(`${this.apiUrl}/scheduled/${scheduleId}`);
  }

  createScheduledReport(schedule: Omit<ScheduledReport, 'id' | 'lastRun' | 'nextRun'>): Observable<ScheduledReport> {
    return this.http.post<ScheduledReport>(`${this.apiUrl}/scheduled`, schedule);
  }

  updateScheduledReport(scheduleId: string, schedule: Partial<ScheduledReport>): Observable<ScheduledReport> {
    return this.http.put<ScheduledReport>(`${this.apiUrl}/scheduled/${scheduleId}`, schedule);
  }

  deleteScheduledReport(scheduleId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/scheduled/${scheduleId}`);
  }

  toggleScheduledReport(scheduleId: string, isActive: boolean): Observable<ScheduledReport> {
    return this.http.patch<ScheduledReport>(`${this.apiUrl}/scheduled/${scheduleId}/toggle`, { isActive });
  }

  // ==================== SNAPSHOTS ====================

  getSnapshots(reportType?: string): Observable<ReportSnapshot[]> {
    let params = new HttpParams();
    if (reportType) {
      params = params.set('reportType', reportType);
    }
    return this.http.get<ReportSnapshot[]>(`${this.apiUrl}/snapshots`, { params });
  }

  getSnapshotById(snapshotId: string): Observable<ReportSnapshot> {
    return this.http.get<ReportSnapshot>(`${this.apiUrl}/snapshots/${snapshotId}`);
  }

  createSnapshot(snapshot: Omit<ReportSnapshot, 'id' | 'generatedAt'>): Observable<ReportSnapshot> {
    return this.http.post<ReportSnapshot>(`${this.apiUrl}/snapshots`, snapshot);
  }

  deleteSnapshot(snapshotId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/snapshots/${snapshotId}`);
  }
}
