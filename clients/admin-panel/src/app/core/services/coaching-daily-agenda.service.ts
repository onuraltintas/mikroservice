import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export type AgendaMode = 'today' | 'upcoming' | 'overdue';
export interface DailyAgendaItem {
  kind: 'study' | 'assignment' | 'decision'; recordId: string; title: string; date: string;
  startMinute: number | null; minutes: number | null; timeZoneId: string;
  planId: string | null; weekStart: string | null;
  /** Live test 22.11.2026: the study task's lesson, for its colour label (absent for assignments and decisions). */
  lessonName?: string | null;
  /** KocApp uyumu (22.11.2026): the resource and range of a study task ("Limit Soru Bankası, test 12–15"). */
  resourceText?: string | null;
}
export interface DailyAgendaPage {
  mode: AgendaMode; items: DailyAgendaItem[]; totalCount: number; pageNumber: number; pageSize: number;
  todayCount: number; upcomingCount: number; overdueCount: number; generatedAt: string;
}
@Injectable({ providedIn: 'root' })
export class CoachingDailyAgendaService {
  private readonly http = inject(HttpClient);
  get(mode: AgendaMode, pageNumber = 1) {
    const params = new HttpParams().set('mode', mode).set('pageNumber', pageNumber);
    return this.http.get<DailyAgendaPage>(`${environment.apiUrl}/coaching/study-planning/daily-agenda`, { params });
  }
}
