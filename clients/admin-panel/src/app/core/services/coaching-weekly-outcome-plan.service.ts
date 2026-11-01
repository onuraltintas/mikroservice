import {inject,Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {map} from 'rxjs';
import {environment} from '../../../environments/environment';

/** Roadmap O7: engineVersion 2 applies the rule-based plan engine; prioritizeWhenShort saves a plan that does not fit, important topics first. */
export interface WeeklyOutcomeRequest {startDate:string;endDate:string;expectedWeeklyDraftVersion:number;reuseFreedSlots:boolean;engineVersion?:number;prioritizeWhenShort?:boolean}
export type WeeklyPlanKind='Learn'|'Review'|'Refresh'|'Spaced';
export const weeklyPlanKindLabels:Record<string,string>={Learn:'Yeni konu',Review:'Tekrar',Refresh:'Kısa tekrar',Spaced:'Aralıklı tekrar'};
export function weeklyPlanKindLabel(kind?:string|null){return (kind&&weeklyPlanKindLabels[kind])||'Çalışma';}
export interface WeeklyOutcomeOptions {startDate:string;endDate:string;weeklyDraftVersion:number;timeZoneId:string;exams:{name:string;date:string|null}[];examDate?:string|null;endDateSource?:'Student'|'Session'|'Catalog'|'Default';catalogExamDate?:string|null}
export interface WeeklyOutcomeTask {date:string;startMinute:number;lessonId:string;lessonName:string;minutes:number;reusedSlot:boolean;kind?:string;reason?:string|null;plannedQuestions?:number|null;outcome:{topicId:string;topicName:string;outcomeId:string;code:string;name:string;estimateId:string;estimateNumber:number;minutes:number}}
export interface WeeklyOutcomePreview {
  request:WeeklyOutcomeRequest;timeZoneId:string;fingerprint:string;canCreate:boolean;protectedTaskCount:number;
  issues:{lessonId:string;lessonName:string;message:string}[];
  protectedTasks?:{date:string;startMinute:number|null;title:string;minutes:number}[];
  leftOutTopics?:{lessonId:string;lessonName:string;topicId:string;topicName:string;minutes:number}[]|null;
  schedule:{tasks:WeeklyOutcomeTask[];freeMinutes:number;recommendedEndDate:string|null;lessons:{lessonId:string;lessonName:string;outcomeCount:number;weeklyMinutes:number;requiredMinutes:number;scheduledMinutes:number;remainingMinutes:number;finishDate:string|null;extraWeeklyMinutes:number}[]};
}
@Injectable({providedIn:'root'})
export class CoachingWeeklyOutcomePlanService {
  private readonly http=inject(HttpClient);private readonly url=`${environment.apiUrl}/coaching/study-planning/weekly-outcome-plans`;
  private scopedUrl(studentId?:string){return studentId?`${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/weekly-outcome-plans`:this.url;}
  options(studentId?:string){return this.http.get<{data:WeeklyOutcomeOptions}>(this.scopedUrl(studentId)+'/options').pipe(map(r=>r.data));}
  preview(request:WeeklyOutcomeRequest,studentId?:string){return this.http.post<{data:WeeklyOutcomePreview}>(this.scopedUrl(studentId)+'/preview',request).pipe(map(r=>r.data));}
  /** Student only: her own exam date; null returns to the date the admin set. */
  setExamDate(examDate:string|null){return this.http.put<{data:WeeklyOutcomeOptions}>(this.url+'/exam-date',{examDate}).pipe(map(r=>r.data));}
  create(request:{title:string;preview:WeeklyOutcomeRequest;fingerprint:string},studentId?:string){return this.http.post<{data:{id:string;version:number}}>(this.scopedUrl(studentId)+'/drafts',request).pipe(map(r=>r.data));}
}
