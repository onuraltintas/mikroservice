import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingInstitutionReadScope {
  isGlobal: boolean;
  institutionId: string | null;
}

export interface CoachingInstitutionAssignmentSummary {
  id: string;
  teacherId: string;
  institutionId?: string | null;
  title: string;
  status: string;
  dueDate: string;
  studentCount: number;
  submittedStudentCount: number;
  createdAt: string;
}

export interface CoachingInstitutionOverview {
  totalAssignments: number;
  activeAssignments: number;
  completedAssignments: number;
  cancelledAssignments: number;
  totalAssignmentStudents: number;
  submittedAssignmentStudents: number;
  totalExams: number;
  totalExamResults: number;
  totalSessions: number;
  upcomingSessions: number;
  totalGoals: number;
  completedGoals: number;
  recentAssignments: CoachingInstitutionAssignmentSummary[];
}

export interface CoachingInstitutionStudent {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeLevel?: number | null;
  teacherName?: string | null;
  teacherUserId?: string | null;
}

export interface CoachingInstitutionStudentPage {
  students: CoachingInstitutionStudent[];
  totalCount: number;
}

export interface CoachingInstitutionStudentDetail {
  studentId: string;
  totalAssignments: number;
  submittedAssignments: number;
  totalExams: number;
  totalSessions: number;
  totalGoals: number;
  assignments: { id: string; title: string; status: string; dueDate: string; score?: number | null }[];
  exams: { id: string; title: string; score: number; maxScore: number; examDate: string }[];
}

export type CoachingInstitutionHistoryType = 'Assignments' | 'Exams' | 'Sessions' | 'Goals';

export interface CoachingInstitutionStudentHistoryItem {
  id: string;
  type: CoachingInstitutionHistoryType;
  title: string;
  eventDate: string;
  status: string;
  score?: number | null;
  maxScore?: number | null;
  progress?: number | null;
  category?: string | null;
}

export interface CoachingInstitutionStudentHistoryPage {
  items: CoachingInstitutionStudentHistoryItem[];
  totalCount: number;
}

export interface CoachingInstitutionHistoryFilter {
  pageNumber?: number;
  pageSize?: number;
  fromDate?: string;
  toDate?: string;
  status?: string;
  search?: string;
}

export interface CoachingInstitutionTeacher {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface CoachingInstitutionTeacherPage {
  teachers: CoachingInstitutionTeacher[];
  totalCount: number;
}

export interface CoachingInstitutionTeacherOverview {
  teacherId: string;
  totalAssignments: number;
  totalAssignmentStudents: number;
  submittedAssignmentStudents: number;
  totalExams: number;
  totalSessions: number;
}

export interface CoachingInstitutionTeacherPeriod {
  assignments: number;
  exams: number;
  sessions: number;
}

export interface CoachingInstitutionTeacherAnalytics {
  teacherId: string;
  currentPeriod: CoachingInstitutionTeacherPeriod;
  previousPeriod: CoachingInstitutionTeacherPeriod;
  lowResults: number;
  mediumResults: number;
  highResults: number;
}

@Injectable({ providedIn: 'root' })
export class CoachingInstitutionService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching-admin`;

  getReadScope(): Observable<CoachingInstitutionReadScope> {
    return this.http.get<CoachingInstitutionReadScope>(`${this.url}/scope`);
  }

  getOverview(recentLimit = 10): Observable<CoachingInstitutionOverview> {
    const limit = Math.min(50, Math.max(1, Math.floor(Number.isFinite(recentLimit) ? recentLimit : 10)));
    return this.http.get<CoachingInstitutionOverview>(`${this.url}/overview`, {
      params: new HttpParams().set('recentLimit', limit)
    });
  }

  getStudentRoster(
    institutionId: string,
    pageNumber = 1,
    search = '',
    teacherUserId?: string,
    gradeLevel?: number | null,
    pageSize = 25
  ): Observable<CoachingInstitutionStudentPage> {
    const page = this.boundedInteger(pageNumber, 1, 1_000, 1);
    const size = this.boundedInteger(pageSize, 1, 100, 25);
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    if (search.trim()) params = params.set('search', search.trim());
    if (teacherUserId) params = params.set('teacherUserId', teacherUserId);
    if (gradeLevel !== undefined && gradeLevel !== null) params = params.set('gradeLevel', gradeLevel);
    return this.http.get<CoachingInstitutionStudentPage>(
      `${this.url}/institutions/${encodeURIComponent(institutionId)}/students`, { params }
    );
  }

  getTeacherRoster(
    institutionId: string,
    pageNumber = 1,
    search = '',
    pageSize = 25
  ): Observable<CoachingInstitutionTeacherPage> {
    const page = this.boundedInteger(pageNumber, 1, 1_000, 1);
    const size = this.boundedInteger(pageSize, 1, 100, 25);
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<CoachingInstitutionTeacherPage>(
      `${this.url}/institutions/${encodeURIComponent(institutionId)}/teachers`, { params }
    );
  }

  getTeacherOverview(teacherId: string): Observable<CoachingInstitutionTeacherOverview> {
    return this.http.get<CoachingInstitutionTeacherOverview>(
      `${this.url}/teachers/${encodeURIComponent(teacherId)}/overview`
    );
  }

  getTeacherAnalytics(teacherId: string): Observable<CoachingInstitutionTeacherAnalytics> {
    return this.http.get<CoachingInstitutionTeacherAnalytics>(
      `${this.url}/teachers/${encodeURIComponent(teacherId)}/analytics`
    );
  }

  getStudentDetail(studentId: string): Observable<CoachingInstitutionStudentDetail> {
    return this.http.get<CoachingInstitutionStudentDetail>(
      `${this.url}/students/${encodeURIComponent(studentId)}/detail`
    );
  }

  getStudentHistory(
    studentId: string,
    type: CoachingInstitutionHistoryType,
    filter: CoachingInstitutionHistoryFilter = {}
  ): Observable<CoachingInstitutionStudentHistoryPage> {
    let params = new HttpParams()
      .set('type', type)
      .set('pageNumber', this.boundedInteger(filter.pageNumber ?? 1, 1, 1_000, 1))
      .set('pageSize', this.boundedInteger(filter.pageSize ?? 25, 1, 100, 25));
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);
    if (filter.status?.trim()) params = params.set('status', filter.status.trim());
    const search = filter.search?.trim().slice(0, 100);
    if (search) params = params.set('search', search);
    return this.http.get<CoachingInstitutionStudentHistoryPage>(
      `${this.url}/students/${encodeURIComponent(studentId)}/history`, { params }
    );
  }

  private boundedInteger(value: number, min: number, max: number, fallback: number): number {
    return Math.min(max, Math.max(min, Math.floor(Number.isFinite(value) ? value : fallback)));
  }
}
