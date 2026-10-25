import {inject,Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {map} from 'rxjs';
import {environment} from '../../../environments/environment';
import type {WeeklySlot} from '../../features/coaching-portal/pages/weekly-calendar';

export interface WeeklyLesson {id:string;name:string;examCode:string|null;gradeNumber:number|null}
export interface WeeklyDraft {version:number;timeZoneId:string;slots:WeeklySlot[]}
export interface WeeklyDraftPage {draft:WeeklyDraft|null;lessons:WeeklyLesson[]}
@Injectable({providedIn:'root'})
export class CoachingWeeklyDraftService {
  private readonly http=inject(HttpClient);private readonly url=`${environment.apiUrl}/coaching/study-planning/weekly-draft`;
  private scopedUrl(studentId?:string){return studentId?`${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/weekly-draft`:this.url;}
  get(studentId?:string){return this.http.get<{data:WeeklyDraftPage}>(this.scopedUrl(studentId)).pipe(map(r=>r.data));}
  save(expectedVersion:number|null,timeZoneId:string,slots:WeeklySlot[],studentId?:string){
    return this.http.put<{data:WeeklyDraft}>(this.scopedUrl(studentId),{expectedVersion,timeZoneId,slots:slots.map(({id,lessonId,day,startMinute,durationMinutes})=>({id,lessonId,day,startMinute,durationMinutes}))}).pipe(map(r=>r.data));
  }
}
