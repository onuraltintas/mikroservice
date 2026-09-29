import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { SpeedReadingStudentReport } from './speed-reading-institution.service';

export interface SpeedReadingTeacherStudent {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId?: string | null;
  institutionName?: string | null;
  currentLevel: number;
  gradeLevel?: number | null;
  targetWpm?: number | null;
  targetComprehension?: number | null;
  dailyGoalMinutes?: number | null;
  learningStyle?: string | null;
  isActive: boolean;
  createdAt: string;
  teacherId: string;
}

export interface SpeedReadingTeacherRosterPage {
  items: SpeedReadingTeacherStudent[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingTeacherClassOverview {
  dateFrom: string;
  dateTo: string;
  totalStudents: number;
  activeStudents: number;
  activeStudentsDataAvailable: boolean;
  classAverageWpmDataAvailable: boolean;
  classAverageComprehensionDataAvailable: boolean;
  classAverageWpm: number;
  classAverageComprehension: number;
  totalActivitiesCompleted: number;
  studentsAboveAverage: number;
  studentsAtAverage: number;
  studentsBelowAverage: number;
  topPerformers: SpeedReadingTeacherPerformance[];
  studentsNeedingSupport: SpeedReadingTeacherPerformance[];
}

export interface SpeedReadingTeacherPerformance {
  studentIdentifier: string;
  averageWpm: number;
  averageComprehension: number;
  activitiesCompleted: number;
  totalMinutes: number;
  performanceLevel: string;
}

export interface SpeedReadingTeacherRosterFilters {
  searchTerm?: string;
  gradeLevel?: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingTeacherService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/speed-reading`;

  getMyStudents(
    pageNumber = 1,
    pageSize = 25,
    filters: SpeedReadingTeacherRosterFilters = {},
  ): Observable<SpeedReadingTeacherRosterPage> {
    const page = Math.min(
      1_000,
      Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)),
    );
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    const search = filters.searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);
    if (filters.gradeLevel !== undefined) params = params.set('gradeLevel', filters.gradeLevel);
    if (filters.isActive !== undefined) params = params.set('isActive', filters.isActive);

    return this.http.get<SpeedReadingTeacherRosterPage>(`${this.baseUrl}/teachers/me/students`, {
      params,
    });
  }

  inviteStudent(email: string): Observable<{ invitationId: string; status?: string; message?: string }> {
    return this.http.post<{ invitationId: string; status?: string; message?: string }>(
      `${this.baseUrl}/invitations/teachers/me`,
      { email: email.trim() },
    );
  }

  getClassOverview(dateFrom: Date, dateTo: Date): Observable<SpeedReadingTeacherClassOverview> {
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return this.http.get<SpeedReadingTeacherClassOverview>(
      `${this.baseUrl}/analytics/teacher/class-overview`,
      { params },
    );
  }

  getStudentReport(
    studentUserId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingStudentReport> {
    const baseUrl = `${this.baseUrl}/analytics/teacher/students/${encodeURIComponent(studentUserId)}`;
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return forkJoin({
      summary: this.http.get<SpeedReadingStudentReport['summary']>(`${baseUrl}/summary`, {
        params,
      }),
      readingSpeed: this.http.get<SpeedReadingStudentReport['readingSpeed']>(
        `${baseUrl}/reading-speed`,
        { params },
      ),
      comprehension: this.http.get<SpeedReadingStudentReport['comprehension']>(
        `${baseUrl}/comprehension`,
        { params },
      ),
      activity: this.http.get<SpeedReadingStudentReport['activity']>(`${baseUrl}/activity`, {
        params,
      }),
    });
  }
}
