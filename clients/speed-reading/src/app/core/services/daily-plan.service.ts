import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

/**
 * Daily Plan Service - Deprecated Stub
 * 
 * DEPRECATED: This service is no longer used in the new ExerciseProgramTemplate system.
 * 
 * The new system uses:
 * - ExerciseProgramService for daily exercises
 * - DailyProgressController API for completion tracking
 * 
 * This stub exists only to prevent compilation errors in old exercise components
 * that still have unused imports. All methods return empty observables.
 */
@Injectable({
  providedIn: 'root'
})
export class DailyPlanService {
  /**
   * @deprecated Use ExerciseProgramService.completeExercise() instead
   */
  completeItem(dailyProgressId: string, userId: string, trainingSeriesItemId: string): Observable<any> {
    // No-op - this method does nothing
    return of(null);
  }
}
