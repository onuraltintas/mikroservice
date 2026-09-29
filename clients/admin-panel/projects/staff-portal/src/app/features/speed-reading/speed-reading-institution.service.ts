import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SpeedReadingTeacherClassOverview } from './speed-reading-teacher.service';

export type SpeedReadingInstitutionMemberRole = 'Student' | 'Teacher';

export interface SpeedReadingInstitution {
  institutionId: string;
  institutionName: string;
}

export interface SpeedReadingInstitutionMember {
  userId: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  role: SpeedReadingInstitutionMemberRole;
  isActive: boolean;
  createdAt: string;
  currentLevel?: number | null;
  gradeLevel?: number | null;
  targetWpm?: number | null;
  targetComprehension?: number | null;
  dailyGoalMinutes?: number | null;
  learningStyle?: string | null;
  teacherUserId?: string | null;
  teacherName?: string | null;
  studentCount: number;
}

export interface SpeedReadingInstitutionMemberPage {
  items: SpeedReadingInstitutionMember[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingInstitutionMemberFilters {
  searchTerm?: string;
  gradeLevel?: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingInstitutionService {
  private readonly http = inject(HttpClient);
  private readonly speedReadingUrl = `${environment.apiUrl}/speed-reading`;

  getMyInstitution(): Observable<SpeedReadingInstitution> {
    return this.http.get<SpeedReadingInstitution>(`${environment.apiUrl}/institution/speed-reading/me`);
  }

  getMembers(
    institutionId: string,
    role: SpeedReadingInstitutionMemberRole,
    pageNumber = 1,
    pageSize = 25,
    filters: SpeedReadingInstitutionMemberFilters = {}
  ): Observable<SpeedReadingInstitutionMemberPage> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams()
      .set('pageNumber', page)
      .set('pageSize', size)
      .set('role', role);
    const search = filters.searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);
    if (filters.gradeLevel !== undefined && role === 'Student') {
      params = params.set('gradeLevel', filters.gradeLevel);
    }
    if (filters.isActive !== undefined) params = params.set('isActive', filters.isActive);
    return this.http.get<SpeedReadingInstitutionMemberPage>(
      `${this.speedReadingUrl}/institutions/${encodeURIComponent(institutionId)}/members`,
      { params }
    );
  }

  getClassOverview(
    institutionId: string,
    dateFrom: Date,
    dateTo: Date
  ): Observable<SpeedReadingTeacherClassOverview> {
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return this.http.get<SpeedReadingTeacherClassOverview>(
      `${this.speedReadingUrl}/analytics/institutions/${encodeURIComponent(institutionId)}/class-overview`,
      { params }
    );
  }
}
