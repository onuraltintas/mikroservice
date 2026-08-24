import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  StudentDashboardReport,
  StudentReadingSpeedReport,
  StudentComprehensionReport,
  StudentSeriesReport,
  StudentActivityReport,
  TeacherClassOverviewReport,
  TeacherStudentDetailReport,
  TeacherAssignmentReport,
  TeacherContentAnalysisReport,
  TeacherTimeBasedProgressReport,
  AdminInstitutionReport,
  AdminPlatformUsageReport,
  AdminContentAnalysisReport,
  AdminSystemHealthReport,
  AdaptivePerformanceReport
} from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/reports`;

  // ==================== STUDENT REPORTS ====================

  getStudentDashboardReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentDashboardReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentDashboardReport>(`${this.apiUrl}/student/dashboard`, { params });
  }

  getStudentReadingSpeedReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentReadingSpeedReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentReadingSpeedReport>(`${this.apiUrl}/student/reading-speed`, { params });
  }

  getStudentComprehensionReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentComprehensionReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentComprehensionReport>(`${this.apiUrl}/student/comprehension`, { params });
  }

  getStudentSeriesReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentSeriesReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentSeriesReport>(`${this.apiUrl}/student/series`, { params });
  }

  getStudentActivityReport(studentId: string, startDate: Date, endDate: Date): Observable<StudentActivityReport> {
    const params = new HttpParams()
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<StudentActivityReport>(`${this.apiUrl}/student/activity`, { params });
  }

  getAdaptivePerformanceReport(
    studentId: string,
    startDate?: Date,
    endDate?: Date,
    exerciseType?: string
  ): Observable<AdaptivePerformanceReport> {
    let params = new HttpParams();
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());
    if (exerciseType) params = params.set('exerciseType', exerciseType);

    return this.http.get<AdaptivePerformanceReport>(
      `${this.apiUrl}/student/adaptive-performance/${studentId}`,
      { params }
    );
  }

  // ==================== TEACHER REPORTS ====================

  getTeacherClassOverviewReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherClassOverviewReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherClassOverviewReport>(`${this.apiUrl}/teacher/class-overview`, { params });
  }

  getTeacherStudentDetailReport(teacherId: string, studentId: string, startDate: Date, endDate: Date): Observable<TeacherStudentDetailReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('studentId', studentId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherStudentDetailReport>(`${this.apiUrl}/teacher/student-detail`, { params });
  }

  getTeacherAssignmentReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherAssignmentReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherAssignmentReport>(`${this.apiUrl}/teacher/assignments`, { params });
  }

  getTeacherContentAnalysisReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherContentAnalysisReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherContentAnalysisReport>(`${this.apiUrl}/teacher/content-analysis`, { params });
  }

  getTeacherTimeBasedProgressReport(teacherId: string, startDate: Date, endDate: Date): Observable<TeacherTimeBasedProgressReport> {
    const params = new HttpParams()
      .set('teacherId', teacherId)
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<TeacherTimeBasedProgressReport>(`${this.apiUrl}/teacher/time-progress`, { params });
  }

  // ==================== ADMIN REPORTS ====================

  getAdminInstitutionReport(startDate: Date, endDate: Date): Observable<AdminInstitutionReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminInstitutionReport>(`${this.apiUrl}/admin/institutions`, { params });
  }

  getAdminPlatformUsageReport(startDate: Date, endDate: Date): Observable<AdminPlatformUsageReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminPlatformUsageReport>(`${this.apiUrl}/admin/platform-usage`, { params });
  }

  getAdminContentAnalysisReport(startDate: Date, endDate: Date): Observable<AdminContentAnalysisReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminContentAnalysisReport>(`${this.apiUrl}/admin/content-analysis`, { params });
  }

  getAdminSystemHealthReport(startDate: Date, endDate: Date): Observable<AdminSystemHealthReport> {
    const params = new HttpParams()
      .set('startDate', startDate.toISOString())
      .set('endDate', endDate.toISOString());
    return this.http.get<AdminSystemHealthReport>(`${this.apiUrl}/admin/system-health`, { params });
  }

  // ==================== EXPORT ====================

  exportReportToPdf(reportData: any): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/pdf`, reportData, { responseType: 'blob' });
  }

  exportReportToExcel(reportData: any): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/export/excel`, reportData, { responseType: 'blob' });
  }
}
