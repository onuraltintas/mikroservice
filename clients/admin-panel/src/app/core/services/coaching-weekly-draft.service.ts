import {inject,Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {map} from 'rxjs';
import {environment} from '../../../environments/environment';
import type {WeeklySlot} from '../../features/coaching-portal/pages/weekly-calendar';

export interface WeeklyLesson {id:string;name:string;examCode:string|null;gradeNumber:number|null;topicCount?:number}
export interface WeeklyDraft {version:number;timeZoneId:string;slots:WeeklySlot[]}
/** Paket H1 (K5): maxDayMinutes is the most study one day of the weekly routine may hold for this student. */
export interface WeeklyDraftPage {draft:WeeklyDraft|null;lessons:WeeklyLesson[];maxDayMinutes?:number}
/** Paket H2: Fill keeps the saved cards and proposes only in free time; Replace proposes the whole week. */
export type WeeklyProposalMode='Fill'|'Replace';
/** Paket H2 (K6): which YKS exam the week leans to. */
export type WeeklyExamFocus='tyt'|'balanced'|'ayt';
/** Minutes per week: saved (kept in Fill mode), proposed and needed (rounded up to half an hour). */
export interface WeeklyProposalLesson {lessonId:string;lessonName:string;examCode:string|null;existingMinutes:number;proposedMinutes:number;neededMinutes:number;noTime:boolean}
/** coveredMinutes: what the week covers of the need (each course counted up to its need); draftVersion: the weekly draft it was made from. */
export interface WeeklyProposal {mode:WeeklyProposalMode;examFocus:WeeklyExamFocus|null;slots:WeeklySlot[];lessons:WeeklyProposalLesson[];freeMinutes:number;
  neededMinutes:number;shortageMinutes:number;coveredMinutes:number;maxDayMinutes:number;weeksUntilEnd:number;endDate:string;middleSchool:boolean;draftVersion:number|null;
  /** Paket H4: the finish date the need was computed for (null when none was given). */
  finishBy?:string|null;
  /** Review of PR #136: Fill only: a whole new week (Replace) would give time to these lessons, which have a need but no time now. */
  rebuildWouldHelp?:boolean;rebuildLessonIds?:string[]}
/** Paket H4: one tempo with its finish date, the weekly minutes it needs and the weekly minutes the student has. */
export interface WeeklyTempoOption {tempo:'Relaxed'|'Balanced'|'Intense';finishBy:string;neededWeeklyMinutes:number;capacityWeeklyMinutes:number;fits:boolean}
export interface WeeklyTempoOptions {planEnd:string;options:WeeklyTempoOption[]}
/** A study time a teacher gives for a student who has saved none (never saved). */
export interface WeeklyProposalWindow {day:string;startMinute:number;endMinute:number}
@Injectable({providedIn:'root'})
export class CoachingWeeklyDraftService {
  private readonly http=inject(HttpClient);private readonly url=`${environment.apiUrl}/coaching/study-planning/weekly-draft`;
  private scopedUrl(studentId?:string){return studentId?`${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/weekly-draft`:this.url;}
  get(studentId?:string){return this.http.get<{data:WeeklyDraftPage}>(this.scopedUrl(studentId)).pipe(map(r=>r.data));}
  /** Paket H2: the server's need-based weekly proposal from the saved availability (or a teacher's answers); nothing is saved. */
  proposal(mode:WeeklyProposalMode,examFocus:WeeklyExamFocus|null,studentId?:string,windows?:WeeklyProposalWindow[],finishBy?:string|null){
    // Paket H4: finishBy (the chosen tempo's finish date) is sent only when there is one.
    const body={mode,examFocus,...(windows?{windows}:{}),...(finishBy?{finishBy}:{})};
    return this.http.post<{data:WeeklyProposal}>(`${this.scopedUrl(studentId)}/proposal`,body).pipe(map(r=>r.data));
  }
  /** Paket H4: Rahat / Dengeli / Yoğun with their finish dates and weekly hours (a teacher may send windows, as for the proposal). */
  /** mode: the way the summary will propose (Fill when the weekly draft has cards, Replace when it is empty); fits follows that proposal. */
  tempoOptions(examFocus:WeeklyExamFocus|null,studentId?:string,windows?:WeeklyProposalWindow[],mode:WeeklyProposalMode='Fill'){
    return this.http.post<{data:WeeklyTempoOptions}>(`${this.scopedUrl(studentId)}/tempo-options`,windows?{examFocus,mode,windows}:{examFocus,mode}).pipe(map(r=>r.data));
  }
  save(expectedVersion:number|null,timeZoneId:string,slots:WeeklySlot[],studentId?:string){
    return this.http.put<{data:WeeklyDraft}>(this.scopedUrl(studentId),{expectedVersion,timeZoneId,slots:slots.map(({id,lessonId,day,startMinute,durationMinutes})=>({id,lessonId,day,startMinute,durationMinutes}))}).pipe(map(r=>r.data));
  }
}
