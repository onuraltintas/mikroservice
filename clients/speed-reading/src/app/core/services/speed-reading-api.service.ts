import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SpeedReadingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface SpeedReadingReadingStatistics {
  totalSessions: number;
  averageWpm: number;
  averageComprehension: number;
  totalMinutes: number;
  bestWpm: number;
}

export interface SpeedReadingReadingSession {
  id: string;
  readingTextId: string;
  calculatedWpm: number;
  comprehensionRate: number;
  efficiencyScore: number;
  readingTimeSeconds: number;
  correctAnswers: number;
  totalQuestions: number;
  completedAt: string;
}

export interface SpeedReadingExerciseResult {
  id: string;
  exerciseId: string;
  readingTextId?: string;
  wordsRead: number;
  timeSpentSeconds: number;
  rawWpm: number;
  comprehensionScore: number;
  weightedKdp: number;
  completedAt: string;
}

export interface SpeedReadingExerciseSession {
  id: string;
  exerciseId: string;
  readingTextId?: string;
  status: number;
  startTime: string;
  endTime?: string;
  currentStep: number;
  totalSteps: number;
  correctCount: number;
  incorrectCount: number;
  totalPausedSeconds: number;
}

export interface SpeedReadingProgramProgress {
  id: string;
  programTemplateId: string;
  assignedDate: string;
  currentDay: number;
  currentWeek: number;
  currentDifficultyLevel: number;
  daysCompleted: number;
  exercisesCompleted: number;
  lastCompletionDate?: string;
  isActive: boolean;
  completedDate?: string;
  averageSuccessRate: number;
  currentStreak: number;
  longestStreak: number;
}

export interface SpeedReadingPersonalizedPathItem {
  id: string;
  pathIndex: number;
  contentType: string;
  contentId?: string;
  contentTitle: string;
  difficultyLevel: number;
  estimatedDurationMinutes: number;
  isCompleted: boolean;
  completedAt?: string;
  achievedScore?: number;
  recommendationReason?: string;
  isUnlocked: boolean;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.speedReadingApiUrl;

  getReadingStatistics(): Observable<SpeedReadingReadingStatistics> {
    return this.http.get<SpeedReadingReadingStatistics>(`${this.baseUrl}/progress/reading-statistics`);
  }

  getReadingHistory(options?: {
    readingTextId?: string;
    dateFrom?: string;
    dateTo?: string;
  }): Observable<SpeedReadingReadingSession[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(options ?? {})) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<SpeedReadingReadingSession[]>(`${this.baseUrl}/progress/reading-history`, { params });
  }

  getExerciseResults(pageNumber = 1, pageSize = 20): Observable<SpeedReadingPage<SpeedReadingExerciseResult>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingExerciseResult>>(`${this.baseUrl}/progress/exercise-results`, { params });
  }

  getActiveExerciseSessions(): Observable<SpeedReadingExerciseSession[]> {
    return this.http.get<SpeedReadingExerciseSession[]>(`${this.baseUrl}/progress/active-exercise-sessions`);
  }

  getProgramProgress(): Observable<SpeedReadingProgramProgress[]> {
    return this.http.get<SpeedReadingProgramProgress[]>(`${this.baseUrl}/progress/programs`);
  }

  getDailyExerciseLogs(options?: {
    dateFrom?: string;
    dateTo?: string;
    limit?: number;
  }): Observable<unknown[]> {
    let params = new HttpParams();
    if (options?.dateFrom) params = params.set('dateFrom', options.dateFrom);
    if (options?.dateTo) params = params.set('dateTo', options.dateTo);
    if (options?.limit !== undefined) params = params.set('limit', options.limit);
    return this.http.get<unknown[]>(`${this.baseUrl}/progress/daily-exercise-logs`, { params });
  }

  getPersonalizedLearningPath(pageNumber = 1, pageSize = 20): Observable<SpeedReadingPage<SpeedReadingPersonalizedPathItem>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);
    return this.http.get<SpeedReadingPage<SpeedReadingPersonalizedPathItem>>(`${this.baseUrl}/learning-paths/personalized`, { params });
  }
}
