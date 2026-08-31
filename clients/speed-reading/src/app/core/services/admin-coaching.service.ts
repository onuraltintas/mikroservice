import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  Assignment,
  CoachingSession,
  ExamResult,
  Goal,
  PagedResult
} from './coaching.service';

const ADMIN_BASE = `${environment.apiUrl}/v1/coaching-admin`;

interface AdminPagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class AdminCoachingService {
  private readonly http = inject(HttpClient);

  getAssignments(params: { status?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<Assignment>> {
    return this.http.get<AdminPagedResult<any>>(`${ADMIN_BASE}/assignments`, {
      params: this.pageParams(params.page, params.pageSize, params.status)
    }).pipe(map(result => this.toPage(result, item => ({
      id: item.id,
      studentId: '',
      studentName: `${item.studentCount ?? 0} öğrenci`,
      assignedById: item.teacherId,
      title: item.title,
      description: item.bookTitle ?? item.source,
      dueDate: item.dueDate,
      status: String(item.status) as Assignment['status'],
      targetQuestions: item.studentCount,
      isApprovedByCoach: null,
      createdAt: item.createdAt
    }))));
  }

  cancelAssignment(id: string): Observable<void> {
    return this.http.post<void>(`${ADMIN_BASE}/assignments/${id}/cancel`, {});
  }

  getSessions(params: { status?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<CoachingSession>> {
    return this.http.get<AdminPagedResult<any>>(`${ADMIN_BASE}/sessions`, {
      params: this.pageParams(params.page, params.pageSize, params.status)
    }).pipe(map(result => this.toPage(result, item => ({
      id: item.id,
      coachId: item.teacherId,
      studentId: '',
      coachingRelationshipId: '',
      studentName: `${item.studentCount ?? 0} öğrenci`,
      scheduledAt: item.scheduledDate,
      plannedDurationMinutes: item.durationMinutes,
      status: String(item.status) as CoachingSession['status'],
      sessionType: String(item.sessionType) as CoachingSession['sessionType'],
      sharedNotes: item.title,
      createdAt: item.createdAt
    }))));
  }

  cancelSession(id: string): Observable<void> {
    return this.http.post<void>(`${ADMIN_BASE}/sessions/${id}/cancel`, {});
  }

  getGoals(params: { level?: string; status?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<Goal>> {
    const completed = params.status === 'Completed'
      ? 'true'
      : params.status === 'Active' ? 'false' : undefined;
    let query = this.pageParams(params.page, params.pageSize);
    if (completed !== undefined) query = query.set('completed', completed);

    return this.http.get<AdminPagedResult<any>>(`${ADMIN_BASE}/goals`, { params: query })
      .pipe(map(result => this.toPage(result, item => ({
        id: item.id,
        studentId: item.studentId,
        coachId: item.setByTeacherId,
        level: String(item.category) as Goal['level'],
        title: item.title,
        targetDate: item.targetDate ?? '',
        status: item.isCompleted ? 'Completed' : 'Active',
        completionPercentage: item.currentProgress ?? 0,
        isCreatedByCoach: true,
        subGoalCount: 0
      }))));
  }

  deleteGoal(id: string): Observable<void> {
    return this.http.delete<void>(`${ADMIN_BASE}/goals/${id}`);
  }

  getExams(params: { examType?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<ExamResult>> {
    let query = this.pageParams(params.page, params.pageSize);
    if (params.examType) query = query.set('examType', params.examType);

    return this.http.get<AdminPagedResult<any>>(`${ADMIN_BASE}/exams`, { params: query })
      .pipe(map(result => this.toPage(result, item => ({
        id: item.id,
        studentId: '',
        studentName: `${item.resultCount ?? 0} sonuç`,
        examName: item.title,
        examDate: item.examDate,
        examType: String(item.examType),
        recordedById: item.createdByTeacherId,
        isRecordedByCoach: true,
        subjectCount: item.resultCount ?? 0,
        createdAt: item.createdAt
      }))));
  }

  deleteExam(id: string): Observable<void> {
    return this.http.delete<void>(`${ADMIN_BASE}/exams/${id}`);
  }

  private pageParams(page = 1, pageSize = 20, status?: string): HttpParams {
    let params = new HttpParams()
      .set('pageNumber', page)
      .set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return params;
  }

  private toPage<TSource, TResult>(
    result: AdminPagedResult<TSource>,
    mapItem: (item: TSource) => TResult
  ): PagedResult<TResult> {
    return {
      items: (result.items ?? []).map(mapItem),
      total: result.totalCount ?? 0,
      page: result.pageNumber ?? 1,
      pageSize: result.pageSize ?? 20
    };
  }
}
