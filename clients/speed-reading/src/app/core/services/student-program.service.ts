import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Program Type Enum
 */
export enum ProgramType {
  Standard = 0,
  ExamPrep = 1
}

/**
 * Age Group Enum
 */
// AgeGroup Enum removed - Use AgeGroupConfiguration IDs
// export enum AgeGroup { ... }

/**
 * Student Program Information
 * Contains all details about assigned program
 */
export interface StudentProgramInfo {
  progressId: string;
  templateId: string;
  templateName: string;
  templateDescription: string;
  programType: ProgramType;
  programTypeName: string;
  examType: string | null;
  targetAgeGroupId: string;
  targetAgeGroupName: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  currentWeek: number;
  currentDay: number;
  currentDifficultyLevel: number;
  maxDifficultyLevel: number;
  totalDaysCompleted: number;
  totalExercisesCompleted: number;
  averageSuccessRate: number;
  currentStreak: number;
  longestStreak: number;
  assignedDate: string;
  lastCompletionDate: string | null;
  isActive: boolean;
  completedDate: string | null;
}

/**
 * Student Program Service - Refactored for ApiResponse<T> compatibility
 * 
 * Manages student's program information and progress
 * 
 * CHANGES:
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (StudentProgramInfo, etc.)
 * - Removed manual ApiResponse handling from tap operators
 * - Signals updated with unwrapped data directly
 */
@Injectable({
  providedIn: 'root'
})
export class StudentProgramService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/v1/student-program`;

  // Signals for reactive state
  currentProgram = signal<StudentProgramInfo | null>(null);
  allPrograms = signal<StudentProgramInfo[]>([]);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);

  /**
   * Get student's active program information
   * Backend returns: ApiResponse<StudentProgramInfo>
   * Service receives: StudentProgramInfo (auto-unwrapped)
   */
  getMyProgram(): Observable<StudentProgramInfo> {
    this.loading.set(true);
    this.error.set(null);

    return this.http.get<StudentProgramInfo>(`${this.apiUrl}/my-program`).pipe(
      tap({
        next: (data) => {
          this.currentProgram.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading program info:', err);
          this.error.set(err.error?.message || 'Program bilgisi yüklenirken hata oluştu');
          this.currentProgram.set(null);
          this.loading.set(false);
        }
      })
    );
  }

  /**
   * Get all programs assigned to student (including inactive)
   * Backend returns: ApiResponse<StudentProgramInfo[]>
   * Service receives: StudentProgramInfo[] (auto-unwrapped)
   */
  getMyPrograms(): Observable<StudentProgramInfo[]> {
    this.loading.set(true);
    this.error.set(null);

    return this.http.get<StudentProgramInfo[]>(`${this.apiUrl}/my-programs`).pipe(
      tap({
        next: (data) => {
          this.allPrograms.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Error loading programs:', err);
          this.error.set(err.error?.message || 'Program listesi yüklenirken hata oluştu');
          this.allPrograms.set([]);
          this.loading.set(false);
        }
      })
    );
  }

  /**
   * Helper: Check if program is exam preparation
   */
  isExamPrep(program: StudentProgramInfo): boolean {
    return program.programType === ProgramType.ExamPrep;
  }

  /**
   * Helper: Get program type badge color
   */
  getProgramTypeColor(programType: ProgramType): string {
    return programType === ProgramType.ExamPrep ? 'accent' : 'primary';
  }

  /**
   * Helper: Get difficulty level description
   */
  getDifficultyDescription(level: number): string {
    const descriptions: { [key: number]: string } = {
      1: 'Çok Kolay',
      2: 'Kolay',
      3: 'Orta',
      4: 'Zor',
      5: 'Çok Zor'
    };
    return descriptions[level] || 'Bilinmiyor';
  }

  /**
   * Start a new program manually
   */
  startProgram(templateId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/start`, { templateId });
  }

  /**
   * Helper: Calculate progress percentage
   */
  getProgressPercentage(program: StudentProgramInfo): number {
    // Assuming each week has 7 days
    const estimatedTotalDays = program.currentWeek * 7;
    if (estimatedTotalDays === 0) return 0;
    return Math.min(100, Math.round((program.totalDaysCompleted / estimatedTotalDays) * 100));
  }

  /**
   * Clear all state
   */
  clear(): void {
    this.currentProgram.set(null);
    this.allPrograms.set([]);
    this.loading.set(false);
    this.error.set(null);
  }
}
