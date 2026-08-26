import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Daily Exercise - Günlük egzersiz yapısı
 */
export interface DailyExercise {
  exerciseId: string;
  exerciseTypeId: string;
  exerciseTypeName: string;
  title: string;
  description?: string;
  difficultyLevel: number;
  estimatedDurationMinutes: number;
  isCompleted: boolean;
  completedAt?: string;
  order: number;
  configurationJson?: string;
}

/**
 * Exercise Completion Request (ML-Ready Enhanced)
 */
export interface CompleteExerciseRequest {
  exerciseId: string;
  successRate: number;
  timeSpentSeconds: number;

  // ML-ready enhanced metrics
  correctCount: number;
  incorrectCount: number;
  totalAttempts: number;
  averageResponseTimeMs: number;
  medianResponseTimeMs: number;
  stdDevResponseTimeMs: number;
  pauseCount: number;
  totalPausedSeconds: number;
  devicePlatform?: string;

  // Standardized JSON data (serialized StandardizedResultData)
  resultDataJson?: string;
}

/**
 * Exercise Completion Response
 */
export interface CompleteExerciseResponse {
  success: boolean;
  message: string;
  dayCompleted: boolean;
  weekCompleted: boolean;
  difficultyIncreased: boolean;
  newDifficultyLevel?: number;
  currentStreak: number;
  longestStreak: number;

  // Program completion fields
  programCompleted: boolean;
  recommendedNextProgram?: RecommendedProgramDto;
  completionStats?: ProgramCompletionStats;
}

export interface RecommendedProgramDto {
  templateId: string;
  name: string;
  description: string;
  totalDays: number;
  totalWeeks: number;
  difficultyRange: string;
  recommendationReason: string;
  programType: string;
}

export interface ProgramCompletionStats {
  totalDays: number;
  averageSuccessRate: number;
  longestStreak: number;
  totalExercises: number;
}

/**
 * Student Progress Summary
 */
export interface StudentProgressSummary {
  currentWeek: number;
  currentDay: number;
  currentDifficultyLevel: number;
  exercisesCompleted: number;
  averageSuccessRate: number;
  currentStreak: number;
  longestStreak: number;
  lastCompletionDate?: string;

  // Program details
  templateName: string;
  totalWeeks: number;
  totalDays: number;
  totalExercisesCompleted: number;
}

/**
 * Exercise Program Service
 * 
 * Yeni Duolingo-style egzersiz sistemini yöneten servis.
 * Günlük egzersiz listesi, tamamlama ve ilerleme takibi.
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseProgramService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.speedReadingApiUrl}/daily-progress`;
  private readonly speedReadingApiUrl = environment.speedReadingApiUrl;

  /**
   * Bugünün egzersizlerini getir
   */
  /**
   * Bugünün egzersizlerini getir
   */
  getTodayExercises(options?: { headers?: any }): Observable<DailyExercise[]> {
    return this.http.get<any>(`${this.baseUrl}/today-exercises`, options).pipe(
      map((r: any) => Array.isArray(r) ? r : (r?.exercises ?? []))
    );
  }

  /**
   * Belirli bir günün egzersizlerini getir
   */
  getExercisesByDay(dayNumber: number): Observable<DailyExercise[]> {
    return this.http.get<any>(`${this.baseUrl}/day/${dayNumber}`).pipe(
      map((r: any) => Array.isArray(r) ? r : (r?.exercises ?? []))
    );
  }

  /**
   * Egzersiz tamamlandı olarak işaretle
   */
  completeExercise(request: CompleteExerciseRequest): Observable<CompleteExerciseResponse> {
    return this.http.post<CompleteExerciseResponse>(
      `${this.baseUrl}/complete-exercise`,
      request
    );
  }

  /**
   * Öğrencinin mevcut ilerleme durumunu getir
   */
  getMyProgress(options?: { headers?: any }): Observable<StudentProgressSummary> {
    return this.http.get<LegacyProgramProgress[]>(`${this.speedReadingApiUrl}/progress/programs`, options).pipe(
      map(programs => {
        const program = programs[0];
        return {
          currentWeek: program?.currentWeek ?? 0,
          currentDay: program?.currentDay ?? 0,
          currentDifficultyLevel: program?.currentDifficultyLevel ?? 0,
          exercisesCompleted: program?.exercisesCompleted ?? 0,
          averageSuccessRate: program?.averageSuccessRate ?? 0,
          currentStreak: program?.currentStreak ?? 0,
          longestStreak: program?.longestStreak ?? 0,
          lastCompletionDate: program?.lastCompletionDate,
          templateName: '',
          totalWeeks: 0,
          totalDays: 0,
          totalExercisesCompleted: program?.exercisesCompleted ?? 0
        };
      })
    );
  }

  /**
   * Haftalık istatistikleri getir
   */
  getWeeklyStats(): Observable<any> {
    return this.http.get(`${this.baseUrl}/weekly-stats`);
  }

  /**
   * Takvim görünümü için tamamlanmış günleri getir
   */
  getCalendar(year: number, month: number): Observable<any> {
    return this.http.get(`${this.baseUrl}/calendar`, {
      params: { year: year.toString(), month: month.toString() }
    });
  }
}

interface LegacyProgramProgress {
  currentDay: number;
  currentWeek: number;
  currentDifficultyLevel: number;
  exercisesCompleted: number;
  lastCompletionDate?: string;
  averageSuccessRate: number;
  currentStreak: number;
  longestStreak: number;
}
