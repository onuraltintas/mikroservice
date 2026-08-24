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
  /** Write routes stay on the legacy contract until the command slice is migrated. */
  private legacyApiUrl = `${environment.apiUrl}/exercisetypes`;

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
  createExerciseType(exerciseType: Partial<ExerciseType>): Observable<ExerciseType> {
    return this.http.post<ExerciseType>(this.legacyApiUrl, exerciseType);
  }

  /**
   * Update existing exercise type
   * Backend returns: ApiResponse<ExerciseType>
   * Service receives: ExerciseType (auto-unwrapped)
   */
  updateExerciseType(id: string, exerciseType: Partial<ExerciseType>): Observable<ExerciseType> {
    return this.http.put<ExerciseType>(`${this.legacyApiUrl}/${id}`, exerciseType);
  }

  /**
   * Delete exercise type
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteExerciseType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.legacyApiUrl}/${id}`);
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
    return this.http.get<ExerciseTypeCategory[]>(`${this.legacyApiUrl}/categories`);
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
}
