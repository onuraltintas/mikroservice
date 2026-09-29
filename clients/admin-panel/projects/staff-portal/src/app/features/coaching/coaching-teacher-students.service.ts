import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherStudent {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gradeLevel?: number;
  institutionName?: string;
  subject?: string;
  assignmentStartDate: string;
}

export interface PagedCoachingResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export interface CoachingStudentProgressSummary {
  studentId: string;
  totalAssignments: number;
  submittedAssignments: number;
  gradedAssignments: number;
  averageAssignmentPercentage?: number | null;
  totalExams: number;
  averageExamPercentage?: number | null;
  totalGoals: number;
  completedGoals: number;
  averageGoalProgress: number;
  totalSessions: number;
  upcomingSessions: number;
  attendedSessions: number;
  attendancePercentage?: number | null;
}

export type CoachingStudentHistoryType = 'Assignments' | 'Exams' | 'Sessions' | 'Goals';

export interface CoachingStudentHistoryItem {
  id: string;
  type: CoachingStudentHistoryType;
  title: string;
  eventDate: string;
  status: string;
  score?: number | null;
  maxScore?: number | null;
  progress?: number | null;
  category?: string | null;
}

export interface CoachingStudentHistoryFilter {
  fromDate?: string;
  toDate?: string;
  status?: string;
  search?: string;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherStudentsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/teachers/me/students`;

  getMyStudents(
    pageNumber = 1,
    pageSize = 25,
    searchTerm?: string,
    studentUserIds?: readonly string[]
  ): Observable<PagedCoachingResponse<CoachingTeacherStudent>> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    const search = searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);
    for (const studentUserId of studentUserIds ?? []) {
      params = params.append('studentUserIds', studentUserId);
    }

    return this.http.get<PagedCoachingResponse<CoachingTeacherStudent>>(this.url, { params });
  }

  getStudentProgress(studentId: string): Observable<CoachingStudentProgressSummary> {
    return this.http.get<CoachingStudentProgressSummary>(
      `${environment.apiUrl}/reports/student/${encodeURIComponent(studentId)}/progress`
    );
  }

  getStudentHistory(
    studentId: string,
    type: CoachingStudentHistoryType,
    pageNumber = 1,
    pageSize = 10,
    filter?: CoachingStudentHistoryFilter
  ): Observable<PagedCoachingResponse<CoachingStudentHistoryItem>> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 10)));
    let params = new HttpParams()
      .set('pageNumber', page)
      .set('pageSize', size)
      .set('type', type);
    if (filter?.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter?.toDate) params = params.set('toDate', filter.toDate);
    if (filter?.status) params = params.set('status', filter.status);
    const search = filter?.search?.trim();
    if (search) params = params.set('search', search);

    return this.http.get<PagedCoachingResponse<CoachingStudentHistoryItem>>(
      `${environment.apiUrl}/reports/student/${encodeURIComponent(studentId)}/history`,
      { params }
    );
  }
}
