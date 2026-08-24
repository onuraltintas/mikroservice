import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AssessmentExercisesDto,
  AssessmentResultDto
} from '../models/student-panel.model';

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

interface CalculateLevelRequest {
  exerciseResults: {
    exerciseId: string;
    score: number;
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class AssessmentService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/assessment`;

  // Public signals for external components
  assessmentResult = signal<AssessmentResultDto | null>(null);
  hasCompleted = signal(false);

  // Private signals - internal use only
  private assessmentExercises = signal<AssessmentExercisesDto | null>(null);
  private loading = signal(false);
  private error = signal<string | null>(null);

  // Signal to trigger refresh when an exercise is completed
  private refreshTrigger = signal<number>(0);

  /**
   * Notifies that an exercise has been completed
   * This will trigger components to refresh their data
   */
  notifyExerciseCompleted(exerciseId: string): void {
    const oldValue = this.refreshTrigger();
    const newValue = oldValue + 1;

    this.refreshTrigger.set(newValue);
  }

  /**
   * Get the refresh trigger signal for components to subscribe to
   */
  getRefreshTrigger() {
    return this.refreshTrigger.asReadonly();
  }

  /**
   * Gets the 3 exercises for initial assessment
   */
  getAssessmentExercises(): Observable<ApiResponse<AssessmentExercisesDto>> {
    this.loading.set(true);
    this.error.set(null);

    return this.http.get<ApiResponse<AssessmentExercisesDto>>(`${this.apiUrl}/exercises`).pipe(
      tap({
        next: (response: any) => {
          // Handle both wrapped (ApiResponse) and flat responses
          const data = response.data || (response.comprehensionExerciseId ? response : null);

          if (data) {
            this.assessmentExercises.set(data);
          }
          this.loading.set(false);
        },
        error: (error) => {
          this.error.set('Değerlendirme egzersizleri yüklenirken bir hata oluştu.');
          this.loading.set(false);
          console.error('Error loading assessment exercises:', error);
        }
      })
    );
  }

  /**
   * Calculates the student's level based on exercise results
   */
  calculateLevel(exerciseResults: { exerciseId: string; score: number }[]): Observable<ApiResponse<AssessmentResultDto>> {
    this.loading.set(true);
    this.error.set(null);

    const request: CalculateLevelRequest = { exerciseResults };

    return this.http.post<ApiResponse<AssessmentResultDto>>(`${this.apiUrl}/calculate`, request).pipe(
      tap({
        next: (response: any) => {
          // Handle both wrapped (ApiResponse) and flat responses
          const data = response.data || (response.averageScore !== undefined ? response : null);
          const success = response.success || !!data;

          if (success && data) {
            this.assessmentResult.set(data);
            this.hasCompleted.set(true);
          }
          this.loading.set(false);
        },
        error: (error) => {
          this.error.set('Seviye hesaplanırken bir hata oluştu.');
          this.loading.set(false);
          console.error('Error calculating level:', error);
        }
      })
    );
  }

  /**
   * Gets the assessment status (whether completed or not)
   */
  getAssessmentStatus(): Observable<ApiResponse<{ hasCompleted: boolean; assessmentResult?: AssessmentResultDto }>> {
    return this.http.get<ApiResponse<{ hasCompleted: boolean; assessmentResult?: AssessmentResultDto }>>(`${this.apiUrl}/status`).pipe(
      tap({
        next: (response: any) => {
          // Handle both wrapped (ApiResponse) and flat responses
          const data = response.data || (response.hasCompleted !== undefined ? response : null);
          const success = response.success || !!data;

          if (success && data) {
            this.hasCompleted.set(data.hasCompleted);
            if (data.assessmentResult) {
              this.assessmentResult.set(data.assessmentResult);
            }
          }
        },
        error: (error) => {
          console.error('Error getting assessment status:', error);
        }
      })
    );
  }

  /**
   * Skip assessment and assign beginner level
   */
  skipAssessment(): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/skip`, {}).pipe(
      tap({
        next: () => {
          this.hasCompleted.set(true);
        },
        error: (error) => {
          console.error('Error skipping assessment:', error);
        }
      })
    );
  }

  /**
   * Clears all state
   */
  clear(): void {
    this.assessmentExercises.set(null);
    this.assessmentResult.set(null);
    this.hasCompleted.set(false);
    this.loading.set(false);
    this.error.set(null);
  }
}
