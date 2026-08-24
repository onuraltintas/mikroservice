import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Target Age Group Enum
 */
// TargetAgeGroup Enum removed - Use AgeGroupConfiguration IDs
// export enum TargetAgeGroup { ... }

/**
 * Program Type Enum
 */
export enum ProgramType {
  Standard = 0,
  ExamPrep = 1
}

/**
 * Exercise Pattern for weekly configuration
 */
export interface ExercisePattern {
  type: string;              // Exercise type name (required for backend)
  count: number;             // Repetition count
  difficulty: number;        // Difficulty level

  // Extended fields with full exercise details
  exerciseId?: string;       // Exercise ID
  title?: string;            // Exercise title
  description?: string;      // Exercise description
  configurationJson?: string; // Full configuration JSON
  targetAgeGroupId?: string; // Age group ID
  exerciseTypeDisplayName?: string; // Display name of exercise type

  // Parsed configuration metadata (for display)
  metadata?: {
    estimatedMinutes?: number;
    xpReward?: number;
    category?: string;
    points?: number;          // For Schulte, Tachistoscope
    speedWpm?: number;        // For speed reading
    holdMs?: number;          // For fixation
    [key: string]: any;       // Other config fields
  };
}

/**
 * Daily Pattern structure
 */
export interface DailyPattern {
  [key: string]: ExercisePattern[]; // day1, day2...
}

/**
 * Weekly Pattern structure (Dynamic weeks)
 */
export interface WeeklyPattern {
  [key: string]: DailyPattern; // week1, week2...
}

/**
 * Exercise Program Template
 */
export interface ExerciseProgramTemplate {
  id: string;
  name: string;
  description: string;
  targetAgeGroupId: string;
  targetAgeGroupName?: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string; // JSON string
  weeklyPattern?: WeeklyPattern; // Parsed object
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  displayOrder: number;
  isActive: boolean;
  isDeleted: boolean;
  programType: ProgramType;
  examType?: string | null;
  isAssessment: boolean;
  createdAt?: string;
  updatedAt?: string;
}

/**
 * Create/Update Template Request
 */
export interface SaveProgramTemplateRequest {
  name: string;
  description: string;
  targetAgeGroupId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  weeklyPatternJson: string;
  initialDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  maxDifficultyLevel: number;
  displayOrder: number;
  isActive: boolean;
  programType: ProgramType;
  examType?: string | null;
  isAssessment?: boolean;
}

/**
 * Program Template Service
 * Manages exercise program templates for admin panel
 */
@Injectable({
  providedIn: 'root'
})
export class ProgramTemplateService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/program-templates`;

  /**
   * Extract data from ApiResponse wrapper
   * Handles both 'data' (camelCase) and 'Data' (PascalCase)
   */
  private extractData<T>(response: any): T {
    return response?.data ?? response?.Data ?? response;
  }

  /**
   * Get all program templates
   */
  getAll(): Observable<ExerciseProgramTemplate[]> {
    return this.http.get<any>(this.baseUrl).pipe(
      map(response => this.extractData<ExerciseProgramTemplate[]>(response) || [])
    );
  }

  /**
   * Get template by ID
   */
  getById(id: string): Observable<ExerciseProgramTemplate> {
    return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(
      map(response => this.extractData<ExerciseProgramTemplate>(response))
    );
  }

  /**
   * Create new template
   */
  create(request: SaveProgramTemplateRequest): Observable<ExerciseProgramTemplate> {
    return this.http.post<any>(this.baseUrl, request).pipe(
      map(response => this.extractData<ExerciseProgramTemplate>(response))
    );
  }

  /**
   * Update existing template
   */
  update(id: string, request: SaveProgramTemplateRequest): Observable<ExerciseProgramTemplate> {
    return this.http.put<any>(`${this.baseUrl}/${id}`, request).pipe(
      map(response => this.extractData<ExerciseProgramTemplate>(response))
    );
  }

  /**
   * Delete template (soft delete)
   */
  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/${id}`).pipe(
      map(() => void 0)
    );
  }

  /**
   * Clone a template
   */
  clone(id: string): Observable<ExerciseProgramTemplate> {
    return this.http.post<any>(`${this.baseUrl}/${id}/clone`, {}).pipe(
      map(response => this.extractData<ExerciseProgramTemplate>(response))
    );
  }

  /**
   * Get student count using this template
   * Note: Backend endpoint not implemented yet, returning 0
   */
  getStudentCount(id: string): Observable<number> {
    // return this.http.get<number>(`${this.baseUrl}/${id}/student-count`);
    return new Observable(observer => {
      observer.next(0);
      observer.complete();
    });
  }

  /**
   * Parse weekly pattern JSON to object
   * Handles both PascalCase (C# backend) and camelCase (frontend) property names
   * Dynamic weeks support
   * Normalizes legacy (weekly) patterns to new (daily) format
   */
  parseWeeklyPattern(json: string): WeeklyPattern | null {
    try {
      const parsed = JSON.parse(json);
      const result: WeeklyPattern = {};

      // Convert PascalCase to camelCase for each exercise
      // PRESERVE ALL FIELDS from JSON
      const convertExercise = (ex: any): ExercisePattern => ({
        // Required fields
        type: ex.type || ex.Type,
        count: ex.count || ex.Count,
        difficulty: ex.difficulty || ex.Difficulty,

        // Extended fields (preserve if exists)
        ...(ex.exerciseId && { exerciseId: ex.exerciseId }),
        ...(ex.title && { title: ex.title }),
        ...(ex.description && { description: ex.description }),
        ...(ex.configurationJson && { configurationJson: ex.configurationJson }),
        ...(ex.targetAgeGroupId && { targetAgeGroupId: ex.targetAgeGroupId }),
        ...(ex.exerciseTypeDisplayName && { exerciseTypeDisplayName: ex.exerciseTypeDisplayName }),
        ...(ex.metadata && { metadata: ex.metadata })
      });

      Object.keys(parsed).forEach(weekKey => {
        if (weekKey.startsWith('week')) {
          const weekData = parsed[weekKey];

          if (Array.isArray(weekData)) {
            // LEGACY FORMAT: Convert to Daily Format (replicate for 7 days)
            const dailyExercises = weekData.map(convertExercise);
            result[weekKey] = {};
            for (let i = 1; i <= 7; i++) {
              result[weekKey][`day${i}`] = [...dailyExercises]; // Copy array
            }
          } else {
            // NEW FORMAT: Already has days
            result[weekKey] = {};
            Object.keys(weekData).forEach(dayKey => {
              if (dayKey.startsWith('day') && Array.isArray(weekData[dayKey])) {
                result[weekKey][dayKey] = weekData[dayKey].map(convertExercise);
              }
            });
          }
        }
      });

      return result;
    } catch (e) {
      console.error('Failed to parse weekly pattern:', e);
      return null;
    }
  }

  /**
   * Stringify weekly pattern object to JSON
   */
  stringifyWeeklyPattern(pattern: WeeklyPattern): string {
    return JSON.stringify(pattern, null, 2);
  }

  /**
   * Create default weekly pattern (4 weeks by default)
   */
  createDefaultWeeklyPattern(weekCount: number = 4): WeeklyPattern {
    const pattern: WeeklyPattern = {};

    for (let i = 1; i <= weekCount; i++) {
      const dailyExercises = [
        { type: 'Saccade', count: Math.min(2 + Math.floor(i / 2), 3), difficulty: Math.min(i, 3) },
        { type: 'EyeTracking', count: Math.min(2 + Math.floor(i / 2), 3), difficulty: Math.min(i, 3) },
        { type: 'Fixation', count: 1, difficulty: Math.min(i, 3) }
      ];

      pattern[`week${i}`] = {};
      for (let d = 1; d <= 7; d++) {
        pattern[`week${i}`][`day${d}`] = [...dailyExercises];
      }
    }

    return pattern;
  }

  /**
   * Get sorted week keys from pattern
   */
  getWeekKeys(pattern: WeeklyPattern): string[] {
    return Object.keys(pattern)
      .filter(key => key.startsWith('week'))
      .sort((a, b) => {
        const numA = parseInt(a.replace('week', ''));
        const numB = parseInt(b.replace('week', ''));
        return numA - numB;
      });
  }

  /**
   * Get sorted day keys from week pattern
   */
  getDayKeys(dailyPattern: DailyPattern): string[] {
    return Object.keys(dailyPattern)
      .filter(key => key.startsWith('day'))
      .sort((a, b) => {
        const numA = parseInt(a.replace('day', ''));
        const numB = parseInt(b.replace('day', ''));
        return numA - numB;
      });
  }

  /**
   * Get week number from key (week1 → 1)
   */
  getWeekNumber(weekKey: string): number {
    return parseInt(weekKey.replace('week', '')) || 0;
  }

  /**
   * Get day number from key (day1 → 1)
   */
  getDayNumber(dayKey: string): number {
    return parseInt(dayKey.replace('day', '')) || 0;
  }

  /**
   * Get age group display name
   */
  /**
   * Get age group display name
   * @deprecated Use targetAgeGroupName property from object
   */
  getAgeGroupName(ageGroup: any): string {
    // Legacy support or fallback
    return 'Loading...';
  }

  /**
   * Validate weekly pattern
   */
  validateWeeklyPattern(pattern: WeeklyPattern): { valid: boolean; errors: string[] } {
    const errors: string[] = [];

    // Check if all weeks exist
    const weekKeys = Object.keys(pattern).filter(k => k.startsWith('week'));
    if (weekKeys.length === 0) {
      errors.push('En az 1 hafta tanımlanmalı');
    }

    weekKeys.forEach(weekKey => {
      const weekData = pattern[weekKey];
      const dayKeys = Object.keys(weekData).filter(k => k.startsWith('day'));

      if (dayKeys.length === 0) {
        errors.push(`${weekKey} için gün tanımı yok`);
      }

      dayKeys.forEach(dayKey => {
        const exercises = weekData[dayKey];
        if (!Array.isArray(exercises)) {
          errors.push(`${weekKey} ${dayKey} geçersiz format`);
        } else {
          exercises.forEach((ex, index) => {
            if (!ex.type || typeof ex.type !== 'string') {
              errors.push(`${weekKey} ${dayKey} Egzersiz ${index + 1}: Tip eksik`);
            }
            if (ex.count === undefined || ex.count < 1 || ex.count > 10) {
              errors.push(`${weekKey} ${dayKey} Egzersiz ${index + 1}: Count 1-10 arasında olmalı`);
            }
            if (ex.difficulty === undefined || ex.difficulty < 1 || ex.difficulty > 5) {
              errors.push(`${weekKey} ${dayKey} Egzersiz ${index + 1}: Difficulty 1-5 arasında olmalı`);
            }
          });
        }
      });
    });

    return {
      valid: errors.length === 0,
      errors
    };
  }
}
