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
  plannedStartMinute?:number|null;
}
export interface StudyTask extends StudyTaskInput {
  id: string;
  isCompleted: boolean;
  actualMinutes: number | null;
  actualQuestions?: number | null;
  completedAt: string | null;
  outcomes?: { outcomeId: string; code: string; name: string; estimateId: string; estimateNumber: number; allocatedMinutes: number; origin: string }[];
  plannedStartMinute?:number|null;weeklyLessonId?:string|null;lessonName?:string|null;
}
export interface StudyPlanSummary { id: string; version: number; title: string; status: StudyPlanStatus }
export interface WeeklyPlanSource {draftVersion:number;learningContextVersion:number|null;startDate:string;endDate:string;reuseFreedSlots:boolean;fingerprint:string;activeRevisionId:string|null;activeRevisionVersion:number|null;timeZoneId?:string}
export interface StudyPlan extends StudyPlanSummary { tasks: StudyTask[];weeklySource?:WeeklyPlanSource|null }
export interface StudyPlanInput { title: string; tasks: StudyTaskInput[] }
export interface StudyPlanPage { items: StudyPlanSummary[]; totalCount: number; pageNumber: number; pageSize: number }
export interface TargetPage<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number }
export interface TeacherStudyWork {taskId:string;planId:string;title:string;plannedDate:string;plannedStartMinute:number|null;plannedMinutes:number;isCompleted:boolean;isOverdue:boolean;actualMinutes:number|null}
export interface TeacherStudyStudent {studentId:string;fullName:string;hasActivePlan:boolean;hasDraft:boolean;completedTasks:number;pendingTasks:number;overdueTasks:number;actualMinutes:number;tasks?:TeacherStudyWork[]}
export interface TeacherStudyOverview {items:TeacherStudyStudent[];totalCount:number;pageNumber:number;pageSize:number}
export type StudyDay = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export interface StudyWindow { day: StudyDay; startMinute: number; endMinute: number }
export interface StudyAvailability { version: number; timeZoneId: string; windows: StudyWindow[] }
export interface StudyAvailabilityUpdate { expectedVersion: number | null; timeZoneId: string; windows: StudyWindow[] }
interface ApiResult<T> { success: boolean; data: T }
export interface GoalTarget { goalId: string; version: number; targetSchoolId: string | null; targetUniversityProgramId: string | null; canEdit: boolean; catalogTarget?: { name: string; detail: string; isActive: boolean; scoreType?:string|null } | null; scoreTarget?: { targetScore: number | null; maxScore: number | null; examType: string | null; subject: string | null } | null; netTarget?: {examCode:string|null;targetNet:number|null}|null; netTargets?:{examCode:string|null;targetNet:number|null}[] }
export interface StudyTopic { id: string; lessonId?: string; name: string; lessonName: string; unitName: string; gradeNumber: number | null; examCode: string | null; estimatedMinutes: number | null }
export interface PublishedStudyOutcome { outcomeId: string; code: string; name: string; estimateId: string; estimateNumber: number; learningMinutes: number; practiceMinutes: number; checkMinutes: number; totalMinutes: number; editionCode: string; officialLessonPeriods: number | null; sourceKind?: 'Official' | 'ExamSkill' }
export interface AutomaticStudyRequest { startDate: string; days: number; expectedAvailabilityVersion: number; topics: { topicId: string; requiredMinutes: number | null; outcomes?: { outcomeId: string; estimateId: string; requiredMinutes: number | null }[] }[]; expectedLearningContextVersion?: number }
export interface AutomaticStudyDraftRequest { title: string; preview: AutomaticStudyRequest; expectedActiveRevisionId: string | null; expectedActiveRevisionVersion: number | null }
export interface AutomaticStudyPreview {
  availabilityVersion: number; timeZoneId: string; activeRevisionId: string | null; activeRevisionVersion: number | null;
  protectedTasks: { taskId: string; plannedDate: string; title: string; plannedMinutes: number; isPinned: boolean; isCompleted: boolean }[];
  schedule: { tasks: { topicId: string; plannedDate: string; plannedMinutes: number }[];
    unscheduledTopics: { topicId: string; remainingMinutes: number }[];
    availableMinutes: number; scheduledMinutes: number; unscheduledMinutes: number; unusedMinutes: number };
}

export interface CalendarTaskSave {expectedVersion:number;title:string;plannedDate:string;startMinute:number;plannedMinutes:number;lessonId:string|null}

@Injectable({ providedIn: 'root' })
export class CoachingStudyPlanningService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/plans`;
  private readonly availabilityUrl = `${environment.apiUrl}/coaching/study-planning/availability`;

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

  getPrimaryGoal() { return this.http.get<ApiResult<{goalId:string|null;version:number|null}>>(`${environment.apiUrl}/coaching/study-planning/primary-goal`).pipe(map(x=>x.data)); }
  selectPrimaryGoal(goalId:string, expectedVersion:number|null) { return this.http.put<ApiResult<{goalId:string|null;version:number|null}>>(`${environment.apiUrl}/coaching/study-planning/primary-goal`,{goalId,expectedVersion}).pipe(map(x=>x.data)); }
  private goalTargetUrl(goalId: string, studentId?: string) {
    const root = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study` : `${environment.apiUrl}/coaching/study-planning`;
    return `${root}/goals/${encodeURIComponent(goalId)}/target`;
  }
  getGoalTarget(goalId: string, studentId?: string) {
    return this.http.get<ApiResult<GoalTarget>>(this.goalTargetUrl(goalId, studentId)).pipe(map(result => result.data));
  }
  practiceProfiles() { return this.http.get<import('../models/practice-exam').PracticeExamProfile[]>(`${environment.apiUrl}/exams/profiles`); }
  saveGoalNetTarget(goalId:string, expectedVersion:number, examCode:string|null, targetNet:number|null, studentId?:string) {
    return this.http.put<ApiResult<GoalTarget>>(`${this.goalTargetUrl(goalId,studentId)}/net`, {expectedVersion,examCode,targetNet}).pipe(map(x=>x.data));
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
  restore(id:string,expectedVersion:number,active:{id:string;version:number}|null,studentId:string|null=null){
    return this.http.post<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/restore`,{expectedVersion,expectedActiveRevisionId:active?.id??null,expectedActiveRevisionVersion:active?.version??null}).pipe(map(r=>r.data));
  }
  complete(id: string, taskId: string, expectedVersion: number, actualMinutes: number, actualQuestions: number | null = null) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.url}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/completion`,
      { expectedVersion, actualMinutes, actualQuestions }).pipe(map(result => result.data));
  }
  reschedule(id: string, taskId: string, expectedVersion: number, plannedDate: string, studentId: string | null = null) {
    return this.http.put<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/schedule`,
      { expectedVersion, plannedDate }).pipe(map(result => result.data));
  }
  move(id:string,expectedVersion:number,taskIds:string[],plannedDate:string,startMinute:number,studentId:string|null=null){
    return this.http.put<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/calendar-move`,
      {expectedVersion,taskIds,plannedDate,startMinute}).pipe(map(r=>r.data));
  }
  correctTitle(id:string,taskId:string,expectedVersion:number,title:string,studentId:string){
    return this.http.put<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}/title`,{expectedVersion,title}).pipe(map(r=>r.data));
  }
  saveCalendarTask(id:string,taskId:string|null,request:CalendarTaskSave,studentId:string|null=null){
    const url=`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/tasks`;
    return (taskId?this.http.put<ApiResult<StudyPlan>>(`${url}/${encodeURIComponent(taskId)}`,request):this.http.post<ApiResult<StudyPlan>>(url,request)).pipe(map(r=>r.data));
  }
  deleteCalendarTask(id:string,taskId:string,expectedVersion:number,studentId:string|null=null){
    return this.http.delete<ApiResult<StudyPlan>>(`${this.plansUrl(studentId)}/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}`,{body:{expectedVersion}}).pipe(map(r=>r.data));
  }
  teacherOverview(fromDate:string,toDate:string,pageNumber=1,search=''){
    const params=new HttpParams().set('fromDate',fromDate).set('toDate',toDate).set('pageNumber',pageNumber).set('pageSize',25).set('search',search.trim());
    return this.http.get<TeacherStudyOverview>(`${environment.apiUrl}/coaching/teachers/me/study-overview`,{params});
  }
}
