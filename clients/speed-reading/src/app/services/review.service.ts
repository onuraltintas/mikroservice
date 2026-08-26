import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ReviewExerciseDto,
  ReviewStatisticsDto,
  SubmitReviewResult,
  ReviewHistoryDto
} from '../models/student-panel.model';

@Injectable({
  providedIn: 'root'
})
export class ReviewService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.speedReadingApiUrl}/review`;

  // Signals for reactive state
  dueReviews = signal<ReviewExerciseDto[]>([]);
  statistics = signal<ReviewStatisticsDto | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  /**
   * Gets all exercises due for review today
   */
  getDueReviews(seriesId?: string): Observable<ReviewExerciseDto[]> {
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams();
    if (seriesId) {
      params = params.set('seriesId', seriesId);
    }

    return this.http.get<any>(`${this.apiUrl}/due`, { params }).pipe(
      map(response => Array.isArray(response) ? response : (response?.items ?? response?.data ?? [])),
      tap({
        next: (data) => {
          this.dueReviews.set(Array.isArray(data) ? data : []);
          this.loading.set(false);
        },
        error: (error) => {
          this.error.set('Tekrar egzersizleri yüklenirken bir hata oluştu.');
          this.loading.set(false);
          console.error('Error loading due reviews:', error);
        }
      })
    );
  }

  /**
   * Gets review statistics for the current student
   */
  getStatistics(seriesId?: string): Observable<ReviewStatisticsDto> {
    let params = new HttpParams();
    if (seriesId) {
      params = params.set('seriesId', seriesId);
    }

    return this.http.get<any>(`${this.apiUrl}/statistics`, { params }).pipe(
      map(response => response?.data ?? response),
      tap({
        next: (data) => {
          this.statistics.set(data);
        },
        error: (error) => {
          console.error('Error loading review statistics:', error);
        }
      })
    );
  }

  /**
   * Submits a review result
   */
  submitReview(reviewItemId: string, score: number): Observable<SubmitReviewResult> {
    return this.http.post<any>(`${this.apiUrl}/${reviewItemId}/submit`, { score }).pipe(
      map(response => response?.data ?? response),
      tap({
        next: () => {
          this.getDueReviews().subscribe();
          this.getStatistics().subscribe();
        },
        error: (error) => {
          console.error('Error submitting review:', error);
        }
      })
    );
  }

  /**
   * Gets review history for a specific exercise
   */
  getReviewHistory(exerciseId: string): Observable<ReviewHistoryDto[]> {
    return this.http.get<any>(`${this.apiUrl}/exercise/${exerciseId}/history`).pipe(
      map(response => Array.isArray(response) ? response : (response?.data ?? []))
    );
  }

  /**
   * Adds an exercise to review queue
   */
  addToReviewQueue(exerciseId: string, trainingSeriesId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/add`, {
      exerciseId,
      trainingSeriesId
    }).pipe(
      tap({
        next: () => {
          this.getStatistics().subscribe();
        },
        error: (error) => {
          console.error('Error adding to review queue:', error);
        }
      })
    );
  }

  /**
   * Updates daily progress with review information
   */
  updateDailyProgress(dailyProgressId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/update-daily-progress/${dailyProgressId}`, {});
  }

  /**
   * Gets overdue reviews count
   */
  getOverdueCount(): number {
    return this.dueReviews().filter(r => r.isOverdue).length;
  }

  /**
   * Gets today's due reviews count
   */
  getDueTodayCount(): number {
    return this.dueReviews().filter(r => !r.isOverdue).length;
  }

  /**
   * Clears all state
   */
  clear(): void {
    this.dueReviews.set([]);
    this.statistics.set(null);
    this.loading.set(false);
    this.error.set(null);
  }
}
