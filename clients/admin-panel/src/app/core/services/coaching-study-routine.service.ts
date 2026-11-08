import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StudyDay, StudyWindow } from './coaching-study-planning.service';

/** Paket H3: sabahçı, öğlenci, tam gün, or no school (mezun, açık öğretim). */
export type StudySchoolShift = 'Morning' | 'Afternoon' | 'FullDay' | 'NoSchool';
export type StudyWeekendDays = 'Both' | 'Saturday' | 'Sunday' | 'None';
/** Paket H3 (K6): a YKS student's TYT/AYT weighting, as the routine stores it. */
export type StudyRoutineExamFocus = 'Tyt' | 'Balanced' | 'Ayt';
/** Paket H4: how fast the topics are covered; null means Relaxed. */
export type StudyTempo = 'Relaxed' | 'Balanced' | 'Intense';

/** Paket H3: the "günlük düzen" answers. Every time is a minute of the day (Europe/Istanbul) on :00 or :30. */
export interface StudyRoutineAnswers {
  schoolShift: StudySchoolShift | null;
  schoolStart: number | null; schoolEnd: number | null; afterSchoolStart: number | null; latestEnd: number | null;
  morningStudy: boolean; morningStart: number | null; dayStart: number | null;
  dinnerStart: number | null; dinnerEnd: number | null;
  busyDays: StudyDay[]; busyDaysOff: boolean; busyStart: number | null;
  weekdayMinutes: number | null;
  weekend: StudyWeekendDays | null; weekendStart: number | null; weekendEnd: number | null; weekendMinutes: number | null;
  examFocus: StudyRoutineExamFocus | null;
  tempo?: StudyTempo | null;
}
export interface StudyDayBudget { day: StudyDay; minutes: number }
/** availabilityMatches: the free hours are still the ones the routine wrote (false after a hand edit). */
export interface StudyRoutineView {
  version: number; schemaVersion: number; answers: StudyRoutineAnswers; windows: StudyWindow[]; dayBudgets: StudyDayBudget[];
  daysWithoutWindows: StudyDay[]; availabilityVersion: number; currentAvailabilityVersion: number | null; availabilityMatches: boolean;
  updatedAt: string | null;
}
export interface StudyRoutineUpdate { expectedVersion: number | null; expectedAvailabilityVersion: number | null; answers: StudyRoutineAnswers }

@Injectable({ providedIn: 'root' })
export class CoachingStudyRoutineService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/study-planning/routine`;

  /** The student's routine, or a teacher's read of it; null when there is none (the student's 404). */
  get(studentId: string | null = null) {
    const url = studentId ? `${environment.apiUrl}/coaching/staff/students/${encodeURIComponent(studentId)}/study/routine` : this.url;
    return this.http.get<{ data: StudyRoutineView | null }>(url).pipe(map(r => r.data ?? null),
      catchError(e => e?.status === 404 ? of(null) : throwError(() => e)));
  }
  /** Student only: saves the answers and replaces the free hours with the windows they give. */
  save(update: StudyRoutineUpdate) {
    return this.http.put<{ data: StudyRoutineView }>(this.url, update).pipe(map(r => r.data));
  }
}
