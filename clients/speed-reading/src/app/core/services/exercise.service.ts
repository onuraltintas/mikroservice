import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Exercise, ReadingText, ExerciseResult } from '../models/exercise.model';
import { PagedResult } from '../models/paged-result.model';

/**
 * Exercise Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (Exercise[], PagedResult<Exercise>, etc.)
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.speedReadingApiUrl}`;
  private readonly LEGACY_API_URL = `${environment.apiUrl}`;

  /**
   * Get all exercises with pagination
   * Backend returns: ApiResponse<PagedResult<Exercise>>
   * Service receives: PagedResult<Exercise> (auto-unwrapped)
   */
  getExerciseTypes(): Observable<PagedResult<any>> {
    // Note: This endpoint returns all exercise types, we ask for a large page size
    return this.http.get<PagedResult<any>>(`${this.API_URL}/exercise-types`, {
      params: { pageSize: '100' }
    });
  }

  getExercises(exerciseTypeId?: string, difficultyLevel?: number, targetAgeGroupId?: string, pageNumber = 1, pageSize = 10): Observable<PagedResult<Exercise>> {
    let url = `${this.API_URL}/exercises`;
    const params: string[] = [];
    if (exerciseTypeId) params.push(`exerciseTypeId=${exerciseTypeId}`);
    if (difficultyLevel !== undefined) params.push(`difficultyLevel=${difficultyLevel}`);
    if (targetAgeGroupId) params.push(`targetAgeGroupId=${targetAgeGroupId}`);
    params.push(`pageNumber=${pageNumber}`);
    params.push(`pageSize=${pageSize}`);
    if (params.length > 0) url += `?${params.join('&')}`;
    return this.http.get<PagedResult<Exercise>>(url);
  }

  /**
   * Get exercise by ID
   * Backend returns: ApiResponse<Exercise>
   * Service receives: Exercise (auto-unwrapped)
   */
  getExerciseById(id: string): Observable<Exercise> {
    return this.http.get<Exercise>(`${this.API_URL}/exercises/${id}`);
  }

  /**
   * Get first exercise by exercise type ID
   * Backend returns: ApiResponse<Exercise>
   * Service receives: Exercise (auto-unwrapped)
   */
  getExerciseByTypeId(exerciseTypeId: string): Observable<Exercise> {
    return this.getExercises(exerciseTypeId, undefined, undefined, 1, 1).pipe(
      map(result => {
        if (result.items.length > 0) return result.items[0];
        throw new Error(`No exercise found for type ${exerciseTypeId}`);
      })
    );
  }

  /**
   * Get exercise by exercise type ID and difficulty level
   * Backend returns: ApiResponse<PagedResult<Exercise>>
   * Service receives: PagedResult<Exercise>, then extracts first item
   */
  getExerciseByTypeAndDifficulty(exerciseTypeId: string, difficultyLevel: number): Observable<Exercise> {
    return this.getExercises(exerciseTypeId, difficultyLevel, undefined, 1, 1).pipe(
      map(result => {
        if (result.items && result.items.length > 0) {
          return result.items[0];
        }
        throw new Error(`No exercise found for type ${exerciseTypeId} with difficulty ${difficultyLevel}`);
      })
    );
  }

  /**
   * Create new exercise
   * Backend returns: ApiResponse<Exercise>
   * Service receives: Exercise (auto-unwrapped)
   */
  createExercise(exercise: Partial<Exercise>): Observable<Exercise> {
    return this.http.post<Exercise>(`${this.LEGACY_API_URL}/v1/exercises`, exercise);
  }

  /**
   * Update existing exercise
   * Backend returns: ApiResponse<Exercise>
   * Service receives: Exercise (auto-unwrapped)
   */
  updateExercise(id: string, exercise: Partial<Exercise>): Observable<Exercise> {
    return this.http.put<Exercise>(`${this.LEGACY_API_URL}/v1/exercises/${id}`, exercise);
  }

  /**
   * Delete exercise
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteExercise(id: string): Observable<void> {
    return this.http.delete<void>(`${this.LEGACY_API_URL}/v1/exercises/${id}`);
  }

  // ============ Reading Text Methods ============

  /**
   * Get reading texts with filters
   * Backend returns: ApiResponse<ReadingText[]>
   * Service receives: ReadingText[] (auto-unwrapped)
   */
  /**
   * Get reading texts with filters
   * Backend returns: ApiResponse<ReadingText[]>
   * Service receives: ReadingText[] (auto-unwrapped)
   */
  getReadingTexts(exerciseId?: string, category?: string, difficultyLevel?: number): Observable<ReadingText[]> {
    let url = `${this.API_URL}/reading-texts`;
    const params: string[] = [];
    if (exerciseId) params.push(`exerciseId=${exerciseId}`);
    if (category) params.push(`category=${category}`);
    if (difficultyLevel !== undefined) params.push(`difficultyLevel=${difficultyLevel}`);
    if (params.length > 0) url += `?${params.join('&')}`;
    return this.http.get<ReadingText[]>(url);
  }

  /**
   * Get reading text by ID
   * Backend returns: ApiResponse<ReadingText>
   * Service receives: ReadingText (auto-unwrapped)
   */
  getReadingText(id: string): Observable<ReadingText> {
    return this.http.get<ReadingText>(`${this.API_URL}/reading-texts/${id}?includeQuestions=true`).pipe(
      map(text => ({ ...text, questions: text.questions ?? [] }))
    );
  }

  /**
   * Get reading text by ID (alias)
   * Backend returns: ApiResponse<ReadingText>
   * Service receives: ReadingText (auto-unwrapped)
   */
  getReadingTextById(id: string): Observable<ReadingText> {
    return this.getReadingText(id);
  }

  /**
   * Create reading text
   * Backend returns: ApiResponse<ReadingText>
   * Service receives: ReadingText (auto-unwrapped)
   */
  createReadingText(readingText: Partial<ReadingText>): Observable<ReadingText> {
    return this.http.post<ReadingText>(`${this.LEGACY_API_URL}/v1/reading-texts/exercise`, readingText);
  }

  /**
   * Update reading text
   * Backend returns: ApiResponse<ReadingText>
   * Service receives: ReadingText (auto-unwrapped)
   */
  updateReadingText(id: string, readingText: Partial<ReadingText>): Observable<ReadingText> {
    return this.http.put<ReadingText>(`${this.LEGACY_API_URL}/v1/reading-texts/exercise/${id}`, readingText);
  }

  /**
   * Delete reading text
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteReadingText(id: string): Observable<void> {
    return this.http.delete<void>(`${this.LEGACY_API_URL}/v1/reading-texts/exercise/${id}`);
  }

  /**
   * Submit exercise result
   * Backend returns: ApiResponse<any>
   * Service receives: any (auto-unwrapped)
   */
  submitExerciseResult(result: ExerciseResult): Observable<any> {
    return this.http.post<any>(`${this.LEGACY_API_URL}/v1/exercise-results`, result);
  }
}
