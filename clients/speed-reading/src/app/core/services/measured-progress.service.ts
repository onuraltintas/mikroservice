import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Raw daily-progress/my-progress response (only the fields the student screens use). */
export interface MyProgressResponse {
  currentReadingSpeed?: number | null;
  initialReadingSpeed?: number | null;
  averageSuccessRate?: number | null;
  completedExercises?: number | null;
  eyeMovementScore?: number | null;
  concentrationScore?: number | null;
}

/** The student's latest measured reading, shown the same way on the home page and in reports. */
export interface MeasuredProgress {
  readingSpeed: number | null;
  initialReadingSpeed: number | null;
  comprehension: number | null;
  completedExercises: number;
  raw: MyProgressResponse | null;
}

/**
 * Live test 19.11.2026: the home page, "Raporlarım" and the parent panel showed three different reading speeds
 * (312 kelime/dk, "Veri yok", "—"). The student screens now read one source: the measured speed and comprehension
 * from daily-progress/my-progress. One request is shared by every widget on the page.
 */
@Injectable({ providedIn: 'root' })
export class MeasuredProgressService {
  private readonly http = inject(HttpClient);
  private cached$: Observable<MeasuredProgress> | null = null;
  private cachedAt = 0;
  /** Widgets that open together share one request; a later visit asks again so new results show up. */
  private static readonly ShareWindowMs = 10_000;

  load(refresh = false, now = Date.now()): Observable<MeasuredProgress> {
    if (!this.cached$ || refresh || now - this.cachedAt > MeasuredProgressService.ShareWindowMs) {
      this.cachedAt = now;
      this.cached$ = this.http.get<MyProgressResponse>(`${environment.speedReadingApiUrl}/daily-progress/my-progress`, {
        headers: { 'X-Skip-Error-Toast': 'true' }
      }).pipe(
        map(raw => toMeasuredProgress(raw)),
        catchError(() => of(toMeasuredProgress(null))),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.cached$;
  }
}

export function toMeasuredProgress(raw: MyProgressResponse | null): MeasuredProgress {
  const positive = (value: number | null | undefined) => (typeof value === 'number' && value > 0 ? value : null);
  return {
    readingSpeed: positive(raw?.currentReadingSpeed) === null ? null : Math.round(raw!.currentReadingSpeed!),
    initialReadingSpeed: positive(raw?.initialReadingSpeed) === null ? null : Math.round(raw!.initialReadingSpeed!),
    comprehension: positive(raw?.averageSuccessRate),
    completedExercises: raw?.completedExercises ?? 0,
    raw
  };
}
