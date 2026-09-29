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

  private boundedInteger(value: number, min: number, max: number, fallback: number): number {
    return Math.min(max, Math.max(min, Math.floor(Number.isFinite(value) ? value : fallback)));
  }
}
