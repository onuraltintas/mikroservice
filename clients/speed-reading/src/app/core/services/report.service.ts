import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface KDPDataPoint {
  date: Date;
  kdp: number;
  comprehensionScore: number;
  rawWPM: number;
}

export interface KDPTrend {
  dataPoints: KDPDataPoint[];
  averageKDP: number;
  maxKDP: number;
  minKDP: number;
  trendDirection: string;
}

export interface ExerciseTypeStat {
  type: string;
  averageKDP: number;
  averageComprehension: number;
  count: number;
}

export interface ExerciseResultSummary {
  exerciseTitle: string;
  completedAt: Date;
  weightedKDP: number;
  comprehensionScore: number;
  rawWPM: number;
  timeSpentSeconds: number;
}

export interface DetailedStudentReport {
  studentId: string;
  totalExercisesCompleted: number;
  totalTimeSpentMinutes: number;
  overallAverageKDP: number;
  overallAverageComprehension: number;
  exerciseTypeStatistics: ExerciseTypeStat[];
  detailedResults: ExerciseResultSummary[];
}

export interface ComprehensionTrend {
  dates: string[];
  comprehensionValues: number[];
  average: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/reports`;

  getKDPTrend(studentId: string, days: number = 30): Observable<KDPTrend> {
    return this.http.get<KDPTrend>(`${this.API_URL}/kdp-trend/${studentId}?days=${days}`);
  }

  getDetailedReport(studentId: string, startDate?: Date, endDate?: Date): Observable<DetailedStudentReport> {
    let url = `${this.API_URL}/detailed/${studentId}`;
    const params: string[] = [];

    if (startDate) {
      params.push(`startDate=${startDate.toISOString()}`);
    }
    if (endDate) {
      params.push(`endDate=${endDate.toISOString()}`);
    }

    if (params.length > 0) {
      url += `?${params.join('&')}`;
    }

    return this.http.get<DetailedStudentReport>(url);
  }

  getComprehensionTrend(studentId: string, days: number = 30): Observable<ComprehensionTrend> {
    return this.http.get<ComprehensionTrend>(`${this.API_URL}/comprehension-trend/${studentId}?days=${days}`);
  }
}
