import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ExerciseType, ExerciseTypeCategory, PagedResult } from '../models/exercise.model';

/**
 * Exercise Type Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (PagedResult<ExerciseType>, ExerciseType[], etc.)
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseTypeService {
  private http = inject(HttpClient);
  /** Read operations are served by the dedicated Speed Reading service. */
  private apiUrl = `${environment.speedReadingApiUrl}/exercise-types`;

  /**
   * Get exercise types with pagination and filters
   * Backend returns: ApiResponse<PagedResult<ExerciseType>>
   * Service receives: PagedResult<ExerciseType> (auto-unwrapped)
   */
  getExerciseTypes(
    categoryId?: string,
    isActive?: boolean,
    pageNumber: number = 1,
    pageSize: number = 10
  ): Observable<PagedResult<ExerciseType>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<PagedResult<ExerciseType>>(this.apiUrl, { params });
  }

  /**
   * Get exercise type by ID
   * Backend returns: ApiResponse<ExerciseType>
   * Service receives: ExerciseType (auto-unwrapped)
   */
  getExerciseTypeById(id: string): Observable<ExerciseType> {
    return this.http.get<ExerciseType>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create new exercise type
   * Backend returns: ApiResponse<ExerciseType>
   * Service receives: ExerciseType (auto-unwrapped)
   */
  createExerciseType(exerciseType: Partial<ExerciseType>, idempotencyKey?: string): Observable<ExerciseType> {
    return this.http.post<ExerciseType>(this.apiUrl, this.toExerciseTypeRequest(exerciseType), {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  /**
   * Update existing exercise type
   * Backend returns: ApiResponse<ExerciseType>
   * Service receives: ExerciseType (auto-unwrapped)
   */
  updateExerciseType(id: string, exerciseType: Partial<ExerciseType>, idempotencyKey?: string): Observable<ExerciseType> {
    return this.http.put<ExerciseType>(`${this.apiUrl}/${id}`, this.toExerciseTypeRequest(exerciseType), {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  /**
   * Delete exercise type
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteExerciseType(id: string, idempotencyKey?: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, {
      headers: this.idempotencyHeaders(idempotencyKey)
    });
  }

  /**
   * Get active exercise types (convenience method)
   * Backend returns: ApiResponse<PagedResult<ExerciseType>>
   * Service receives: PagedResult<ExerciseType> (auto-unwrapped)
   */
  getActiveExerciseTypes(): Observable<PagedResult<ExerciseType>> {
    return this.getExerciseTypes(undefined, true, 1, 100);
  }

  /**
   * Get exercise type categories
   * Backend returns: ApiResponse<ExerciseTypeCategory[]>
   * Service receives: ExerciseTypeCategory[] (auto-unwrapped)
   */
  getCategories(): Observable<ExerciseTypeCategory[]> {
    return this.http.get<ExerciseTypeCategory[]>(`${this.apiUrl}/categories`);
  }

  /**
   * Get exercise type ID by name
   * Helper method that searches active exercise types
   * @param name Exercise type name to search for
   * @returns Exercise type ID or null if not found
   */
  getExerciseTypeIdByName(name: string): Observable<string | null> {
    return new Observable<string | null>(observer => {
      this.getActiveExerciseTypes().subscribe({
        next: (result) => {
          const exerciseType = result.items.find(
            type => type.name.toLowerCase() === name.toLowerCase()
          );
          observer.next(exerciseType?.id || null);
          observer.complete();
        },
        error: (err) => {
          observer.error(err);
        }
      });
    });
  }

  newIdempotencyKey(): string {
    return `speed-reading-${globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`}`;
  }

  private idempotencyHeaders(idempotencyKey?: string) {
    return { 'Idempotency-Key': idempotencyKey ?? this.newIdempotencyKey() };
  }

  private toExerciseTypeRequest(exerciseType: Partial<ExerciseType>): Record<string, unknown> {
    return {
      name: exerciseType.name ?? '',
      displayName: exerciseType.displayName ?? '',
      description: exerciseType.description ?? null,
      iconName: exerciseType.iconName ?? null,
      colorCode: exerciseType.colorCode ?? null,
      sortOrder: exerciseType.sortOrder ?? 0,
      isActive: exerciseType.isActive ?? true,
      engineType: exerciseType.engineType?.trim()
        || exerciseType.name?.trim()
        || 'custom',
      categoryId: exerciseType.categoryId ?? null
    };
  }
}
