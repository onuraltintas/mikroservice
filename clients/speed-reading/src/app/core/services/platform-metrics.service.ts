import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PlatformMetrics {
  date: string;
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  totalActivities: number;
  totalExercises: number;
  totalReadingSessions: number;
  averageDurationMinutes: number;
  averagePerformance: number;
}

export interface PlatformMetricsTotals {
  totalUsers: number;
  totalNewUsers: number;
  totalActivities: number;
  totalExercises: number;
  totalReadingSessions: number;
  averageDailyActiveUsers: number;
  averageSessionDuration: number;
  averagePerformance: number;
}

export interface PlatformMetricsTrends {
  userGrowthRate: number;
  activeUserRate: number;
  activityGrowthRate: number;
  performanceImprovement: number;
}

export interface PlatformMetricsSummary {
  startDate: string;
  endDate: string;
  dailyMetrics: PlatformMetrics[];
  totals: PlatformMetricsTotals;
  trends: PlatformMetricsTrends;
}

@Injectable({
  providedIn: 'root'
})
export class PlatformMetricsService {
  private readonly apiUrl = `${environment.apiUrl}/v1/admin/metrics`;

  constructor(private http: HttpClient) { }

  /**
   * Get platform metrics for a date range
   */
  getMetrics(startDate?: Date, endDate?: Date): Observable<PlatformMetricsSummary> {
    let params = new HttpParams();

    if (startDate) {
      params = params.set('startDate', startDate.toISOString());
    }
    if (endDate) {
      params = params.set('endDate', endDate.toISOString());
    }

    return this.http.get<PlatformMetricsSummary>(this.apiUrl, { params });
  }

  /**
   * Manually trigger metrics collection for a specific date
   */
  collectMetrics(date?: Date): Observable<{ message: string }> {
    let params = new HttpParams();

    if (date) {
      params = params.set('date', date.toISOString());
    }

    return this.http.post<{ message: string }>(`${this.apiUrl}/collect`, null, { params });
  }

  /**
   * Get metrics for last 30 days
   */
  getLastThirtyDays(): Observable<PlatformMetricsSummary> {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 30);

    return this.getMetrics(startDate, endDate);
  }

  /**
   * Get metrics for last 7 days
   */
  getLastSevenDays(): Observable<PlatformMetricsSummary> {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 7);

    return this.getMetrics(startDate, endDate);
  }
}
