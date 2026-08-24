import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

/**
 * Series Exercise Helper Service - Stub Version
 * 
 * This service has been stubbed after removing the legacy TrainingSeries system.
 * All methods are now no-ops that redirect to dashboard.
 * 
 * This stub exists to prevent breaking changes in exercise components that
 * still reference this service but are not actively used in the new system.
 */

export interface SeriesContext {
  seriesProgressId: string;
  seriesItemId: string;
  exerciseStartTime: number;
  [key: string]: any;
}

export interface DailyLearningContext {
  dailyProgressId: string;
  trainingSeriesItemId: string;
  seriesProgressId?: string;
  [key: string]: any;
}

@Injectable({
  providedIn: 'root'
})
export class SeriesExerciseHelperService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = `${environment.apiUrl}/v1/daily-progress`;

  detectSeriesMode(state: any): SeriesContext | null {
    // Legacy method - always returns null
    return null;
  }

  detectDailyLearningMode(state: any): DailyLearningContext | null {
    // Check if navigation came from daily exercises
    if (state?.['fromDailyExercises']) {
      return {
        dailyProgressId: state['exercise']?.exerciseId || '',
        itemId: state['exercise']?.exerciseId || '',
        exerciseId: state['exercise']?.exerciseId || '',
        dayNumber: 1,
        trainingSeriesItemId: state['exercise']?.exerciseId || ''
      };
    }
    return null;
  }

  completeSeriesItem(context: SeriesContext, score: number, timeSpent?: number): void {
    // Legacy method - redirect to dashboard
    console.warn('[SeriesExerciseHelperService] Legacy method called - redirecting to dashboard');
    this.router.navigate(['/student/dashboard']);
  }

  completeDailyExercise(exerciseId: string, successRate: number, timeSpentSeconds: number) {

    const completionData = {
      exerciseId: exerciseId, // Backend will parse as Guid
      successRate: Math.round(successRate * 100) / 100, // 2 decimal places
      timeSpentSeconds: Math.round(timeSpentSeconds),
      correctCount: Math.round(successRate), // Approximate
      incorrectCount: Math.round(100 - successRate),
      totalAttempts: 100,
      averageResponseTimeMs: 0,
      medianResponseTimeMs: 0,
      stdDevResponseTimeMs: 0,
      pauseCount: 0,
      totalPausedSeconds: 0,
      devicePlatform: 'web-desktop'
    };


    return this.http.post(`${this.apiUrl}/complete-exercise`, completionData);
  }

  completeDailyLearningItemAndReturn(context: DailyLearningContext, score: number, timeSpent?: number): void {

    // Navigate back to daily exercises page to show updated status
    this.router.navigate(['/student/daily-exercises']);
  }

  navigateBack(seriesContext: SeriesContext | null, dailyContext: DailyLearningContext | null): void {
    if (dailyContext) {
      this.router.navigate(['/student/daily-exercises']);
    } else {
      this.router.navigate(['/student/dashboard']);
    }
  }
}
