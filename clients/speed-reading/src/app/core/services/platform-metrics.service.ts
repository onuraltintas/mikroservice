import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface PlatformMetrics {
  date: string | Date;
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
  private readonly apiUrl = `${environment.speedReadingApiUrl}/analytics/admin/platform-usage`;

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

    return this.http.get<any>(this.apiUrl, { params }).pipe(
      map(response => this.toPlatformMetricsSummary(response))
    );
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

  private toPlatformMetricsSummary(value: any): PlatformMetricsSummary {
    const activeByDate = this.chartValues(value.dailyActiveUsers);
    const activityByDate = this.chartValues(value.activityVolume);
    const dates = [...new Set([...activeByDate.keys(), ...activityByDate.keys()])].sort();
    const dailyMetrics = dates.map(date => ({
      date: new Date(date),
      totalUsers: value.totalUsers ?? 0,
      activeUsers: activeByDate.get(date) ?? 0,
      newUsers: 0,
      totalActivities: activityByDate.get(date) ?? 0,
      totalExercises: 0,
      totalReadingSessions: 0,
      averageDurationMinutes: value.averageSessionDuration ?? 0,
      averagePerformance: 0
    }));
    const averageDailyActiveUsers = dailyMetrics.length === 0
      ? 0
      : dailyMetrics.reduce((total, day) => total + day.activeUsers, 0) / dailyMetrics.length;
    const totalExercises = value.featureUsageStats?.exercise ?? 0;

    return {
      startDate: value.dateFrom,
      endDate: value.dateTo,
      dailyMetrics,
      totals: {
        totalUsers: value.totalUsers ?? 0,
        totalNewUsers: value.newUsers ?? 0,
        totalActivities: value.totalActivities ?? 0,
        totalExercises,
        totalReadingSessions: value.totalReadingSessions ?? 0,
        averageDailyActiveUsers,
        averageSessionDuration: value.averageSessionDuration ?? 0,
        averagePerformance: 0
      },
      trends: {
        userGrowthRate: value.userGrowthRate ?? 0,
        activeUserRate: value.engagementRate ?? 0,
        activityGrowthRate: 0,
        performanceImprovement: 0
      }
    };
  }

  private chartValues(charts: any[] | undefined): Map<string, number> {
    return new Map((charts ?? []).map(chart => [
      chart.name,
      Number(chart.series?.[0]?.value ?? 0)
    ]));
  }
}
