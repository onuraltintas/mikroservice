import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTeacherGoal {
  id: string;
  studentId: string;
  title: string;
  description?: string;
  category: string;
  targetDate?: string;
  targetScore?: number;
  targetExamType?: string;
  targetSubject?: string;
  progress: number;
  isCompleted: boolean;
  completedAt?: string;
}

export interface PagedCoachingTeacherGoals {
  items: CoachingTeacherGoal[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
}

export interface CoachingTeacherGoalCreateRequest {
  teacherId: string;
  studentId: string;
  title: string;
  category: number;
  description: string | null;
  targetDate: string | null;
  targetScore: number | null;
}

export interface CoachingTeacherGoalUpdateRequest {
  goalId: string;
  title: string;
  category: number;
  description: string | null;
  targetDate: string | null;
  targetScore: number | null;
  targetExamType: number | null;
  targetSubject: string | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherGoalsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/goals`;

  getTeacherGoals(teacherId: string, pageNumber = 1, pageSize = 25): Observable<PagedCoachingTeacherGoals> {
    const page = Math.min(1_000, Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)));
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    const params = new HttpParams().set('pageNumber', page).set('pageSize', size);
    return this.http.get<PagedCoachingTeacherGoals>(
      `${this.url}/teacher/${encodeURIComponent(teacherId)}`,
      { params }
    );
  }

  createGoal(
    request: CoachingTeacherGoalCreateRequest,
    idempotencyKey: string
  ): Observable<{ goalId: string }> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<{ goalId: string }>(this.url, {
      ...request,
      title: request.title.trim(),
      description: request.description?.trim() || null,
      targetDate: request.targetDate || null,
      targetScore: request.targetScore ?? null
    }, { headers });
  }

  updateGoal(
    goalId: string,
    request: CoachingTeacherGoalUpdateRequest
  ): Observable<{ goalId: string; title: string }> {
    return this.http.put<{ goalId: string; title: string }>(
      `${this.url}/${encodeURIComponent(goalId)}`,
      {
        ...request,
        goalId,
        title: request.title.trim(),
        description: request.description?.trim() || null,
        targetDate: request.targetDate || null,
        targetScore: request.targetScore ?? null,
        targetSubject: request.targetSubject?.trim() || null
      }
    );
  }
}
