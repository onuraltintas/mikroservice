import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export type StudyPlanStatus = 'Draft' | 'Active' | 'Archived';
export interface StudyTaskInput {
  plannedDate: string;
  title: string;
  plannedMinutes: number;
  topicId: string | null;
  isPinned: boolean;
}
export interface StudyTask extends StudyTaskInput {
  id: string;
  isCompleted: boolean;
  actualMinutes: number | null;
  completedAt: string | null;
}
export interface StudyPlanSummary { id: string; version: number; title: string; status: StudyPlanStatus }
export interface StudyPlan extends StudyPlanSummary { tasks: StudyTask[] }
export interface StudyPlanInput { title: string; tasks: StudyTaskInput[] }
export interface StudyPlanPage { items: StudyPlanSummary[]; totalCount: number; pageNumber: number; pageSize: number }
interface ApiResult<T> { success: boolean; data: T }

@Injectable({ providedIn: 'root' })
export class CoachingStudyPlanningService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/plans`;

  list(pageNumber = 1, status?: StudyPlanStatus) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20);
    if (status) params = params.set('status', status);
    return this.http.get<ApiResult<StudyPlanPage>>(this.url, { params }).pipe(map(result => result.data));
  }
  get(id: string) {
    return this.http.get<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}`).pipe(map(result => result.data));
  }
  create(plan: StudyPlanInput) {
    return this.http.post<ApiResult<StudyPlan>>(this.url, plan).pipe(map(result => result.data));
  }
  replace(id: string, expectedVersion: number, plan: StudyPlanInput) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}`, { expectedVersion, plan }).pipe(map(result => result.data));
  }
  publish(id: string, expectedVersion: number) {
    return this.http.post<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/publish`, { expectedVersion }).pipe(map(result => result.data));
  }
  complete(id: string, taskId: string, expectedVersion: number, actualMinutes: number) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/completion`,
      { expectedVersion, actualMinutes }).pipe(map(result => result.data));
  }
  reschedule(id: string, taskId: string, expectedVersion: number, plannedDate: string) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/schedule`,
      { expectedVersion, plannedDate }).pipe(map(result => result.data));
  }
}
