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
export type StudyDay = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export interface StudyWindow { day: StudyDay; startMinute: number; endMinute: number }
export interface StudyAvailability { version: number; timeZoneId: string; windows: StudyWindow[] }
export interface StudyAvailabilityUpdate { expectedVersion: number | null; timeZoneId: string; windows: StudyWindow[] }
interface ApiResult<T> { success: boolean; data: T }
export interface SchoolTarget { id: string; name: string; city: string; district: string; minimumScore: number | null; scoreYear: number | null }
export interface UniversityTarget { id: string; name: string; universityName: string; programCode: string | null; scoreType: string | null; minimumScore: number | null; scoreYear: number | null }
export interface TargetPage<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }
export interface GoalTarget { goalId: string; version: number; targetSchoolId: string | null; targetUniversityProgramId: string | null; canEdit: boolean; catalogTarget?: { name: string; detail: string; isActive: boolean } | null }
export interface StudyTopic { id: string; lessonId?: string; name: string; lessonName: string; unitName: string; gradeNumber: number | null; examCode: string | null; estimatedMinutes: number | null }
export interface AutomaticStudyRequest { startDate: string; days: number; expectedAvailabilityVersion: number; topics: { topicId: string; requiredMinutes: number | null }[] }
export interface AutomaticStudyDraftRequest { title: string; preview: AutomaticStudyRequest; expectedActiveRevisionId: string | null; expectedActiveRevisionVersion: number | null }
export interface AutomaticStudyPreview {
  availabilityVersion: number; timeZoneId: string; activeRevisionId: string | null; activeRevisionVersion: number | null;
  protectedTasks: { taskId: string; plannedDate: string; title: string; plannedMinutes: number; isPinned: boolean; isCompleted: boolean }[];
  schedule: { tasks: { topicId: string; plannedDate: string; plannedMinutes: number }[];
    unscheduledTopics: { topicId: string; remainingMinutes: number }[];
    availableMinutes: number; scheduledMinutes: number; unscheduledMinutes: number; unusedMinutes: number };
}

@Injectable({ providedIn: 'root' })
export class CoachingStudyPlanningService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/plans`;
  private readonly availabilityUrl = `${environment.apiUrl}/coaching/study-planning/availability`;
  private readonly targetUrl = `${environment.apiUrl}/coaching/study-planning/targets`;

  searchTopics(search: string, gradeNumber: number | null, examCode: string, pageNumber = 1) {
    let params = new HttpParams().set('search', search.trim()).set('pageNumber', pageNumber).set('pageSize', 20);
    if (gradeNumber !== null) params = params.set('gradeNumber', gradeNumber);
    if (examCode.trim()) params = params.set('examCode', examCode.trim());
    return this.http.get<ApiResult<TargetPage<StudyTopic>>>(`${environment.apiUrl}/coaching/study-planning/topics`, { params }).pipe(map(result => result.data));
  }
  previewAutomatic(request: AutomaticStudyRequest) {
    return this.http.post<ApiResult<AutomaticStudyPreview>>(`${environment.apiUrl}/coaching/study-planning/automatic-preview`, request).pipe(map(result => result.data));
  }
  saveAutomaticDraft(request: AutomaticStudyDraftRequest) {
    return this.http.post<ApiResult<StudyPlan>>(`${environment.apiUrl}/coaching/study-planning/automatic-drafts`, request).pipe(map(result => result.data));
  }

  searchSchools(search: string, city: string, district: string, pageNumber = 1) {
    const params = new HttpParams().set('search', search.trim()).set('city', city.trim()).set('district', district.trim()).set('pageNumber', pageNumber).set('pageSize', 20);
    return this.http.get<ApiResult<TargetPage<SchoolTarget>>>(`${this.targetUrl}/schools`, { params }).pipe(map(result => result.data));
  }
  searchPrograms(search: string, scoreType: string, pageNumber = 1) {
    const params = new HttpParams().set('search', search.trim()).set('scoreType', scoreType.trim()).set('pageNumber', pageNumber).set('pageSize', 20);
    return this.http.get<ApiResult<TargetPage<UniversityTarget>>>(`${this.targetUrl}/university-programs`, { params }).pipe(map(result => result.data));
  }
  getGoalTarget(goalId: string) {
    return this.http.get<ApiResult<GoalTarget>>(`${environment.apiUrl}/coaching/study-planning/goals/${encodeURIComponent(goalId)}/target`).pipe(map(result => result.data));
  }
  saveGoalTarget(goalId: string, expectedVersion: number, targetUniversityProgramId: string | null, targetSchoolId: string | null) {
    return this.http.put<ApiResult<GoalTarget>>(`${environment.apiUrl}/coaching/study-planning/goals/${encodeURIComponent(goalId)}/target`,
      { expectedVersion, targetUniversityProgramId, targetSchoolId }).pipe(map(result => result.data));
  }

  getAvailability() {
    return this.http.get<ApiResult<StudyAvailability>>(this.availabilityUrl).pipe(map(result => result.data));
  }
  saveAvailability(update: StudyAvailabilityUpdate) {
    return this.http.put<ApiResult<StudyAvailability>>(this.availabilityUrl, update).pipe(map(result => result.data));
  }

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
  archive(id: string, expectedVersion: number) {
    return this.http.post<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/archive`, { expectedVersion }).pipe(map(result => result.data));
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
