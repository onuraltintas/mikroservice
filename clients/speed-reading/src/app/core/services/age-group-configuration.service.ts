import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  AgeGroupConfiguration,
  CreateAgeGroupConfiguration,
  UpdateAgeGroupConfiguration,
  AgeRecommendations
} from '../models/age-group-configuration.model';

@Injectable({
  providedIn: 'root'
})
export class AgeGroupConfigurationService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.speedReadingApiUrl}/age-group-configurations`;

  /**
   * Get all age group configurations (Admin only)
   * @param activeOnly Optional filter to show only active age groups
   * @returns Observable of age group configuration array
   */
  getAll(activeOnly?: boolean): Observable<AgeGroupConfiguration[]> {
    let params = new HttpParams();
    if (activeOnly !== undefined) {
      params = params.set('activeOnly', activeOnly.toString());
    }
    return this.http.get<AgeGroupConfiguration[]>(this.API_URL, { params });
  }

  /**
   * Get active age group configurations (Public - for dropdowns)
   * @returns Observable of active age group configuration array
   */
  getActive(): Observable<AgeGroupConfiguration[]> {
    return this.http.get<AgeGroupConfiguration[]>(`${this.API_URL}/active`);
  }

  /**
   * Get age group configuration by ID
   * @param id Age group ID
   * @returns Observable of age group configuration
   */
  getById(id: string): Observable<AgeGroupConfiguration> {
    return this.http.get<AgeGroupConfiguration>(`${this.API_URL}/${id}`);
  }

  /**
   * Get age group for a specific age (Public)
   * @param age User's age
   * @returns Observable of matching age group configuration
   */
  getByAge(age: number): Observable<AgeGroupConfiguration> {
    return this.http.get<AgeGroupConfiguration>(`${this.API_URL}/by-age/${age}`);
  }

  /**
   * Get recommended values for a specific age (Public)
   * @param age User's age
   * @returns Observable of recommended values
   */
  getRecommendations(age: number): Observable<AgeRecommendations> {
    return this.http.get<AgeRecommendations>(`${this.API_URL}/recommendations/${age}`);
  }

  /**
   * Create new age group configuration (Admin only)
   * @param request Create age group configuration data
   * @returns Observable of created age group ID
   */
  create(request: CreateAgeGroupConfiguration): Observable<string> {
    return this.http.post<any>(this.API_URL, request).pipe(
      map(response => response?.id ?? response?.data?.id ?? response)
    );
  }

  /**
   * Update existing age group configuration (Admin only)
   * @param id Age group ID
   * @param request Update age group configuration data
   * @returns Observable of void
   */
  update(id: string, request: UpdateAgeGroupConfiguration): Observable<void> {
    return this.http.put<void>(`${this.API_URL}/${id}`, request);
  }

  /**
   * Delete age group configuration (Admin only - soft delete)
   * @param id Age group ID
   * @returns Observable of void
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  /**
   * Helper method to format age range for display
   * @param ageGroup Age group configuration
   * @returns Formatted age range string (e.g., "7-11 yaş" or "18+ yaş")
   */
  formatAgeRange(ageGroup: AgeGroupConfiguration): string {
    if (ageGroup.maxAge !== null) {
      return `${ageGroup.minAge}-${ageGroup.maxAge} yaş`;
    }
    return `${ageGroup.minAge}+ yaş`;
  }
}
