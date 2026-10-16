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
  sourceName?: string | null;
  plannedQuestions?: number | null;
}
export interface StudyTask extends StudyTaskInput {
  id: string;
  isCompleted: boolean;
  actualMinutes: number | null;
  actualQuestions?: number | null;
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
export interface GoalTarget { goalId: string; version: number; targetSchoolId: string | null; targetUniversityProgramId: string | null; canEdit: boolean; catalogTarget?: { name: string; detail: string; isActive: boolean } | null; scoreTarget?: { targetScore: number | null; maxScore: number | null; examType: string | null; subject: string | null } | null }
export interface StudyTopic { id: string; lessonId?: string; name: string; lessonName: string; unitName: string; gradeNumber: number | null; examCode: string | null; estimatedMinutes: number | null }
export interface PublishedStudyOutcome { outcomeId: string; code: string; name: string; estimateId: string; estimateNumber: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; totalMinutes: number; editionCode: string; officialLessonPeriods: number }
export interface AutomaticStudyRequest { startDate: string; days: number; expectedAvailabilityVersion: number; topics: { topicId: string; requiredMinutes: number | null; outcomes?: { outcomeId: string; estimateId: string; requiredMinutes: number | null }[] }[]; expectedLearningContextVersion?: number }
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
  getTopicOutcomes(topicId: string) {
    return this.http.get<ApiResult<PublishedStudyOutcome[]>>(`${environment.apiUrl}/coaching/study-planning/topics/${encodeURIComponent(topicId)}/outcomes`).pipe(map(r => r.data));
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
  private goalTargetUrl(goalId: string, studentId?: string) {
    const root = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study` : `${environment.apiUrl}/coaching/study-planning`;
    return `${root}/goals/${encodeURIComponent(goalId)}/target`;
  }
  getGoalTarget(goalId: string, studentId?: string) {
    return this.http.get<ApiResult<GoalTarget>>(this.goalTargetUrl(goalId, studentId)).pipe(map(result => result.data));
  }
  saveGoalTarget(goalId: string, expectedVersion: number, targetUniversityProgramId: string | null, targetSchoolId: string | null, studentId?: string) {
    return this.http.put<ApiResult<GoalTarget>>(this.goalTargetUrl(goalId, studentId),
      { expectedVersion, targetUniversityProgramId, targetSchoolId }).pipe(map(result => result.data));
  }
  saveGoalScoreTarget(goalId: string, expectedVersion: number, targetScore: number | null, maxScore: number | null, examType: string | null, studentId?: string) {
    return this.http.put<ApiResult<GoalTarget>>(`${this.goalTargetUrl(goalId, studentId)}/score`,
      { expectedVersion, targetScore, maxScore, examType }).pipe(map(result => result.data));
  }

  getAvailability(studentId: string | null = null) {
    const url = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/availability` : this.availabilityUrl;
    return this.http.get<ApiResult<StudyAvailability | null>>(url).pipe(map(result => result.data));
  }
  saveAvailability(update: StudyAvailabilityUpdate) {
    return this.http.put<ApiResult<StudyAvailability>>(this.availabilityUrl, update).pipe(map(result => result.data));
  }

  private plansUrl(studentId: string | null = null) {
    return studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/plans` : this.url;
  }

  list(pageNumber = 1, status?: StudyPlanStatus, studentId: string | null = null) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20);
    if (status) params = params.set('status', status);
    return this.http.get<ApiResult<StudyPlanPage>>(this.plansUrl(studentId), { params }).pipe(map(result => result.data));
  }
  get(id: string, studentId: string | null = null) {
    return this.http.get<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}`).pipe(map(result => result.data));
  }
  create(plan: StudyPlanInput, studentId: string | null = null) {
    return this.http.post<ApiResult<StudyPlan>>(this.plansUrl(studentId), plan).pipe(map(result => result.data));
  }
  replace(id: string, expectedVersion: number, plan: StudyPlanInput, studentId: string | null = null) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}`, { expectedVersion, plan }).pipe(map(result => result.data));
  }
  publish(id: string, expectedVersion: number, studentId: string | null = null) {
    return this.http.post<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/publish`, { expectedVersion }).pipe(map(result => result.data));
  }
  archive(id: string, expectedVersion: number, studentId: string | null = null) {
    return this.http.post<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/archive`, { expectedVersion }).pipe(map(result => result.data));
  }
  complete(id: string, taskId: string, expectedVersion: number, actualMinutes: number, actualQuestions: number | null = null) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/completion`,
      { expectedVersion, actualMinutes, actualQuestions }).pipe(map(result => result.data));
  }
  reschedule(id: string, taskId: string, expectedVersion: number, plannedDate: string, studentId: string | null = null) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/schedule`,
      { expectedVersion, plannedDate }).pipe(map(result => result.data));
  }
}
