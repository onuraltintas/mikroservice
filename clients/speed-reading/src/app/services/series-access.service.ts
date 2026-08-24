import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SeriesAccessDto,
  PrerequisiteCheckResult,
  UnlockSeriesResult
} from '../models/student-panel.model';

@Injectable({
  providedIn: 'root'
})
export class SeriesAccessService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/v1/series-access`;

  // Signals for reactive state
  availableSeries = signal<SeriesAccessDto[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  /**
   * Gets all available series with access information
   */
  getAvailableSeries(): Observable<any> {
    this.loading.set(true);
    this.error.set(null);

    return this.http.get<any>(`${this.apiUrl}/available`).pipe(
      tap({
        next: (response) => {
          if (response.success && response.data) {
            this.availableSeries.set(response.data);
          }
          this.loading.set(false);
        },
        error: (error) => {
          this.error.set('Seriler yüklenirken bir hata oluştu.');
          this.loading.set(false);
          console.error('Error loading available series:', error);
        }
      })
    );
  }

  /**
   * Checks if student has access to a specific series
   */
  checkAccess(seriesId: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${seriesId}/access`);
  }

  /**
   * Checks prerequisites for a specific series
   */
  checkPrerequisites(seriesId: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${seriesId}/prerequisites`);
  }

  /**
   * Unlocks a series for the current student
   */
  unlockSeries(seriesId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${seriesId}/unlock`, {}).pipe(
      tap({
        next: (response) => {
          if (response.success) {
            // Refresh available series after unlock
            this.getAvailableSeries().subscribe();
          }
        },
        error: (error) => {
          console.error('Error unlocking series:', error);
        }
      })
    );
  }

  /**
   * Gets unlocked series for quick filtering
   */
  getUnlockedSeries(): SeriesAccessDto[] {
    return this.availableSeries().filter(s => s.isUnlocked);
  }

  /**
   * Gets locked series that can be unlocked
   */
  getUnlockableSeries(): SeriesAccessDto[] {
    return this.availableSeries().filter(s => !s.isUnlocked && s.canUnlock);
  }

  /**
   * Gets locked series that cannot be unlocked yet
   */
  getLockedSeries(): SeriesAccessDto[] {
    return this.availableSeries().filter(s => !s.isUnlocked && !s.canUnlock);
  }
}
