import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface DailyPlanSummary {
  progressId?: string;
  currentDay: number;
  totalDays: number;
  completionPercentage: number;
  todayExerciseCount: number;
  todayCompletedCount: number;
  difficultyLevel: number;
  isUnlocked: boolean;
}

export interface WeeklyPerformance {
  weekNumber: number;
  completionRate: number;
  averageScore: number;
  wpmImprovement: number;
  difficultyStatus: 'TooEasy' | 'TooHard' | 'Balanced';
  weakAreas: string[];
  strongAreas: string[];
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly apiUrl = environment.apiUrl; // Already includes /api prefix

  constructor(private http: HttpClient) {}

  /**
   * Get current daily plan summary
   */
  getDailyPlanSummary(studentId: number): Observable<DailyPlanSummary> {
    return this.http.get<DailyPlanSummary>(`${this.apiUrl}/daily-plan-generator/summary/${studentId}`);
  }

  /**
   * Get weekly performance summary
   */
  getWeeklyPerformance(studentId: number): Observable<WeeklyPerformance> {
    return this.http.get<WeeklyPerformance>(`${this.apiUrl}/performance-analysis/weekly-summary/${studentId}`);
  }
}
