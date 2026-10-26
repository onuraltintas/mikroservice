import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export type TrackingKind = 'study' | 'assignment' | 'review' | 'reflection' | 'decision' | 'weekly-review';
export interface TrackingFilters { fromDate: string; toDate: string; mode: 'all' | TrackingKind; search: string }
export interface TrackingItem { reviewReason?:string|null;
  kind: TrackingKind; recordId: string; studentId: string; studentName: string; title: string;
  planId: string | null; date: string; startMinute: number | null; minutes: number | null; timeZoneId: string;
  weekStart?: string | null;
}
export interface TrackingPage {
  items: TrackingItem[]; totalCount: number; pageNumber: number; pageSize: number;
  overdueStudyCount: number; overdueAssignmentCount: number; awaitingReviewCount: number;
  awaitingWeeklyReviewCount?: number; awaitingReflectionCount: number; overdueDecisionCount: number;
}

// Scoped to one teacher workspace; no student data or cross-account browser storage.
@Injectable()
export class CoachingTeacherTrackingContext {
  draft: TrackingFilters | null = null;
  applied: TrackingFilters | null = null;
  pageNumber = 1;
}

@Injectable({ providedIn: 'root' })
export class CoachingTeacherTrackingService {
  private readonly http = inject(HttpClient);
  get(filters: TrackingFilters, pageNumber = 1) {
    const params = new HttpParams().set('fromDate', filters.fromDate).set('toDate', filters.toDate)
      .set('mode', filters.mode).set('search', filters.search.trim()).set('pageNumber', pageNumber).set('pageSize', 25);
    return this.http.get<TrackingPage>(`${environment.apiUrl}/coaching/teachers/me/tracking`, { params });
  }
}
