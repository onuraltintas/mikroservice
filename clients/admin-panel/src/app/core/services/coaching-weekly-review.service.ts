import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface WeeklyDecisionEffect { decisionId:string; effect:string; note:string }
export interface WeeklyResultStudy { workDays?:{workedOn:string;minutes:number;questions:number|null;isLegacyDate:boolean}[]|null; taskId:string; rootTaskId:string; planId:string; version:number; plannedDate:string; title:string; plannedMinutes:number; actualMinutes:number|null; actualQuestions:number|null; isCompleted:boolean; completedAt:string|null }
export interface WeeklyResultDecision { decisionId:string;version:number;sourceWeekStart:string;title:string;status:string;dueOn:string;completedAt:string|null;work:DecisionWork|null;linkedStudyRootId:string|null;relatedStudy:WeeklyResultStudy[];effect:string|null;effectNote:string|null }
export interface WeeklyResultSnapshot { fingerprint:string;capturedAt:string;summary:WeeklyReview['summary'];decisions:WeeklyResultDecision[];studies:WeeklyResultStudy[] }
export interface WeeklyReflection { version: number; successes: string; obstacles: string; nextFocus: string; rating: number | null; savedAt: string;results?:WeeklyResultSnapshot|null;history?:{version:number;savedAt:string;successes:string;obstacles:string;nextFocus:string;rating:number|null;results:WeeklyResultSnapshot|null}[] }
export interface WeeklyDecisionChange { at: string; title: string; note: string; dueOn: string; status: string; studyTaskId?: string | null; assignmentId?: string | null; action?: string | null; actorUserId?: string | null; previousTeacherId?: string | null; responsibleTeacherId?: string | null; previousDueOn?: string | null;previousStudyTaskId?:string|null; reason?: string | null }
export interface DecisionLinkedWork { id: string; title: string; status: string; isDone: boolean; isOverdue: boolean; canOpen: boolean;
  planId?: string | null; date?: string | null; startMinute?: number | null; minutes?: number | null; actualMinutes?: number | null; score?: number | null;actualQuestions?:number|null;completedAt?:string|null;feedback?:string|null;submittedAt?:string|null }
export interface DecisionWork { study: DecisionLinkedWork | null; assignment: DecisionLinkedWork | null }
export interface DecisionWorkPlan { id: string; version: number; startDate: string | null; endDate: string | null; timeZoneId: string }
export interface DecisionWorkOptions { items: DecisionLinkedWork[]; totalCount: number; pageNumber: number; plan: DecisionWorkPlan | null }
export interface DecisionStudyProposal { expectedDecisionVersion: number; expectedPlanVersion: number; planId: string; taskId: string | null; date: string; startMinute: number; minutes: number }
export interface DecisionStudyPreview { proposal: DecisionStudyProposal; fingerprint: string; title: string; timeZoneId: string; previousDate: string | null; previousStartMinute: number | null; otherWorkCount: number; dayMinutes: number; untimedWorkCount: number }
export interface WeeklyDecision {
  sourceReflection?:{reflectionId:string;reflectionVersion:number;weekStart:string;successes:string;obstacles:string;nextFocus:string;rating:number|null;results:WeeklyResultSnapshot|null;currentResults?:WeeklyResultSnapshot|null}|null;
  previousDecisionId?:string|null;previousDecision?:WeeklyResultDecision|null;
  id: string; version: number; reviewWeekStart: string; title: string; note: string; dueOn: string;
  status: string; completedAt: string | null; isOverdue: boolean; canEdit: boolean; history: WeeklyDecisionChange[]; work?: DecisionWork | null; createdByTeacherId?: string | null; responsibleTeacherId?: string | null; initialDueOn?: string | null;
}
export interface WeeklyReview {
  results?:WeeklyResultSnapshot;
  weekStart: string; today: string; reflection: WeeklyReflection | null;
  summary: { scheduledTasks: number; completedTasks: number; plannedMinutes: number; actualMinutes: number | null;legacyDateActualMinutes?:number|null };
  reviews?: {teacherId:string;reflectionVersion:number;note:string;reviewedAt:string;evidenceFingerprint?:string|null;results?:WeeklyResultSnapshot|null;id?:string|null}[]; needsTeacherReview?:boolean;reviewReason?:string|null;
  decisions: WeeklyDecision[]; totalDecisions: number; decisionPage: number; canReflect: boolean; canDecide: boolean;
}
export interface ReflectionSave { expectedResultsFingerprint?:string;decisionEffects?:WeeklyDecisionEffect[];expectedVersion: number | null; successes: string; obstacles: string; nextFocus: string; rating: number | null }
export interface DecisionSave { sourceReflectionVersion?:number;previousDecisionId?:string|null;expectedResultsFingerprint?:string; expectedVersion: number | null; title: string; note: string; dueOn: string; status: string }
export interface WeeklyHome { missingReflectionCount?:number;
  weekStart: string; currentWeekStart: string; needsReflection: boolean; openDecisions: number; overdueDecisions: number;
  decisions: { id: string; weekStart: string; title: string; dueOn: string; isOverdue: boolean }[];
}
@Injectable({ providedIn: 'root' })
export class CoachingWeeklyReviewService {
  private readonly http = inject(HttpClient);
  private url(student: string | null) { return `${environment.apiUrl}/coaching/${student ? 'staff/students/' + encodeURIComponent(student) + '/study' : 'study-planning'}/weekly-review`; }
  home() { return this.http.get<{ data: WeeklyHome }>(`${this.url(null)}/home`).pipe(map(x => x.data)); }
  get(student: string | null, week: string, page = 1, decisionId?: string | null) {
    let params = new HttpParams().set('weekStart', week).set('decisionPage', page);
    if (decisionId) params = params.set('decisionId', decisionId);
    return this.http.get<{ data: WeeklyReview }>(this.url(student), { params }).pipe(map(x => x.data));
  }
  reviewReflection(student:string,week:string,expectedReflectionVersion:number,note:string,expectedResultsFingerprint?:string) {
    return this.http.post(`${this.url(student)}/${week}/review`,{expectedReflectionVersion,note,...(expectedResultsFingerprint?{expectedResultsFingerprint}:{})});
  }
  reflect(week: string, body: ReflectionSave) { return this.http.put<{ data: WeeklyReflection }>(`${this.url(null)}/${week}`, body).pipe(map(x => x.data)); }
  decide(student: string, week: string, id: string, body: DecisionSave) {
    return this.http.put<{ data: WeeklyDecision }>(`${this.url(student)}/${week}/decisions/${encodeURIComponent(id)}`, body).pipe(map(x => x.data));
  }
  postpone(student: string, week: string, id: string, body: { expectedVersion: number; dueOn: string; reason: string }) {
    return this.http.post<{data:WeeklyDecision}>(`${this.url(student)}/${week}/decisions/${encodeURIComponent(id)}/postpone`,body).pipe(map(x=>x.data));
  }
  changeDeadline(student:string,week:string,id:string,body:{expectedVersion:number;dueOn:string;reason:string}) {
    return this.http.post<{data:WeeklyDecision}>(`${this.url(student)}/${week}/decisions/${encodeURIComponent(id)}/deadline-changes`,body).pipe(map(x=>x.data));
  }
  private workUrl(student: string, row: WeeklyDecision) { return `${this.url(student)}/${row.reviewWeekStart}/decisions/${encodeURIComponent(row.id)}`; }
  workOptions(student: string, row: WeeklyDecision, kind: 'study'|'assignment', search = '', page = 1) {
    return this.http.get<{data:DecisionWorkOptions}>(`${this.workUrl(student,row)}/work-options`,{params:{kind,search,page}}).pipe(map(x=>x.data));
  }
  linkWork(student: string, row: WeeklyDecision, studyTaskId: string|null, assignmentId: string|null) {
    return this.http.put<{data:WeeklyDecision}>(`${this.workUrl(student,row)}/work-links`,{expectedVersion:row.version,studyTaskId,assignmentId}).pipe(map(x=>x.data));
  }
  previewWork(student: string,row:WeeklyDecision,proposal:DecisionStudyProposal) {
    return this.http.post<{data:DecisionStudyPreview}>(`${this.workUrl(student,row)}/study-preview`,proposal).pipe(map(x=>x.data));
  }
  applyWork(student:string,row:WeeklyDecision,preview:DecisionStudyPreview,requestId:string) {
    return this.http.post<{data:WeeklyDecision}>(`${this.workUrl(student,row)}/study-apply`,{proposal:preview.proposal,fingerprint:preview.fingerprint,confirmed:true,requestId}).pipe(map(x=>x.data));
  }
  createWorkAssignment(student:string,row:WeeklyDecision,dueAt:string,estimatedMinutes:number|null,requestId:string) {
    return this.http.post<{data:WeeklyDecision}>(`${this.workUrl(student,row)}/assignment`,{expectedVersion:row.version,dueAt,estimatedMinutes,requestId}).pipe(map(x=>x.data));
  }
}
