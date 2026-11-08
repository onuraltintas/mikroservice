import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StudyRoutineExamFocus, StudyTempo } from './coaching-study-routine.service';

/**
 * Review of PR 135 (S6), H4 contract item 6: the plan wizard's exam focus (YKS) and tempo, the single source of both. PUT replaces
 * both values (an omitted one is cleared) and never touches the free hours. Every field is null while nothing is saved.
 */
export interface StudyPlanPreferences { examFocus: StudyRoutineExamFocus | null; tempo: StudyTempo | null; version?: number | null; updatedAt?: string | null }

@Injectable({ providedIn: 'root' })
export class CoachingPlanPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/plan-preferences`;

  /** The saved choices (nulls when none are saved); null when they cannot be read, so the wizard falls back to the routine's answers. */
  get(studentId: string | null = null) {
    const url = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/plan-preferences` : this.url;
    return this.http.get<{ data: StudyPlanPreferences | null }>(url).pipe(map(r => r.data ?? null), catchError(() => of(null)));
  }
  /** Student only; both values are sent, since PUT replaces both. */
  save(update: { examFocus: StudyRoutineExamFocus | null; tempo: StudyTempo | null }) {
    return this.http.put<{ data: StudyPlanPreferences }>(this.url, update).pipe(map(r => r.data));
  }
}
