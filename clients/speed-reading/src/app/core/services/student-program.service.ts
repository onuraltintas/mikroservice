import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
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
  measuredLevel?: number | null;
  recommendedTrainingLevel?: number | null;
  selectedTrainingLevel?: number | null;
  selectionPolicyVersion?: string | null;
}

export interface NextProgramRecommendation {
  sourceProgressId: string;
  assessmentAttemptId: string;
  templateId: string;
  templateName: string;
  totalDays: number;
  requiresStaffApproval: boolean;
  recommendedTrainingLevel?: number;
  measuredLevel?: number;
  ruleVersion?: string;
  reason?: string;
  averageWpm?: number;
  comprehension?: number;
  wpmDelta?: number | null;
  comprehensionDelta?: number | null;
  trainingSuccessRate?: number;
  higherOption?: { templateId: string; templateName: string; totalDays: number; level: number } | null;
  requestedTemplateId?: string | null;
  requestedAt?: string | null;
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
  private readonly apiUrl = `${environment.speedReadingApiUrl}/student-program`;

  // Signals for reactive state
  currentProgram = signal<StudentProgramInfo | null>(null);
  allPrograms = signal<StudentProgramInfo[]>([]);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);

  getNextRecommendation(): Observable<NextProgramRecommendation | null> {
    return this.http.get<NextProgramRecommendation | null>(`${this.apiUrl}/next-recommendation`);
  }

  confirmNextProgram(request: { templateId: string; sourceProgressId: string; assessmentAttemptId: string; overrideAcknowledged?: boolean }): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.apiUrl}/confirm-next`, request);
  }

  requestNextProgram(request: { templateId: string; sourceProgressId: string; assessmentAttemptId: string; overrideAcknowledged?: boolean }): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.apiUrl}/request-next`, request);
  }

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
   * Weekly progress: the share of this program week's 7 days already completed (H22, report audit 30.10.2026).
   */
  getProgressPercentage(program: StudentProgramInfo): number {
    const week = Math.max(1, program.currentWeek || 1);
    const doneThisWeek = Math.min(7, Math.max(0, (program.totalDaysCompleted || 0) - (week - 1) * 7));
    return Math.round((doneThisWeek / 7) * 100);
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
