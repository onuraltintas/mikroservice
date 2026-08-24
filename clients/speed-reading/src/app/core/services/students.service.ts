import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  Student,
  CreateStudentRequest,
  UpdateStudentRequest,
  StudentExerciseResult,
  StudentDashboard,
  ReadingText,
  Exercise,
  LevelUpResult
} from '../models/student.model';

/**
 * Students Service - Refactored for ApiResponse<T> compatibility
 * 
 * CHANGES:
 * - All HTTP calls now work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Services receive clean typed data (Student[], Student, etc.)
 * - Error handling delegated to GlobalErrorHandler
 */
@Injectable({
  providedIn: 'root'
})
export class StudentsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/students`;

  /**
   * Get all students with optional filters
   * Backend returns: ApiResponse<Student[]>
   * Service receives: Student[] (auto-unwrapped by interceptor)
   */
  getStudents(
    searchTerm?: string,
    institutionId?: string,
    currentLevel?: number,
    isActive?: boolean,
    teacherId?: string
  ): Observable<Student[]> {
    let params = new HttpParams();

    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }
    if (institutionId) {
      params = params.set('institutionId', institutionId);
    }
    if (currentLevel !== undefined) {
      params = params.set('currentLevel', currentLevel.toString());
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    if (teacherId) {
      params = params.set('teacherId', teacherId);
    }

    // Backend returns PagedResult<UserDto>; extract items array
    return this.http.get<any>(this.API_URL, { params }).pipe(
      map(result => Array.isArray(result) ? result : (result?.items ?? []))
    );
  }

  /**
   * Get single student by ID
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  getStudentById(id: string): Observable<Student> {
    return this.http.get<Student>(`${this.API_URL}/${id}`);
  }

  /**
   * Create new student
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  createStudent(request: CreateStudentRequest): Observable<Student> {
    return this.http.post<Student>(this.API_URL, request);
  }

  /**
   * Update existing student
   * Backend returns: ApiResponse<Student>
   * Service receives: Student (auto-unwrapped)
   */
  updateStudent(id: string, request: UpdateStudentRequest): Observable<Student> {
    return this.http.put<Student>(`${this.API_URL}/${id}`, request);
  }

  /**
   * Delete student
   * Backend returns: ApiResponse<void>
   * Service receives: void (auto-unwrapped)
   */
  deleteStudent(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  /**
   * Unlink student from institution (Admin only)
   * Backend returns: ApiResponse<void>
   */
  unlinkStudentFromInstitution(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}/unlink-institution`);
  }

  /**
   * Get student exercise results (for teachers)
   * Backend returns: ApiResponse<StudentExerciseResult[]>
   * Service receives: StudentExerciseResult[] (auto-unwrapped)
   */
  getStudentExerciseResults(teacherId: string, studentId: string): Observable<StudentExerciseResult[]> {
    return this.http.get<StudentExerciseResult[]>(
      `${environment.apiUrl}/v1/teachers/${teacherId}/students/${studentId}/results`
    );
  }

  /**
   * Get student results (generic/admin access)
   * Backend returns: ApiResponse<StudentExerciseResult[]>
   */
  getStudentResults(studentId: string): Observable<StudentExerciseResult[]> {
    return this.http.get<StudentExerciseResult[]>(
      `${this.API_URL}/${studentId}/results`
    );
  }

  /**
   * Get comprehensive student dashboard
   * Backend returns: ApiResponse<StudentDashboard>
   * Service receives: StudentDashboard (auto-unwrapped)
   */
  getDashboard(): Observable<StudentDashboard> {
    return this.http.get<StudentDashboard>(`${this.API_URL}/dashboard`);
  }

  /**
   * Get recommended reading texts based on student profile
   * Backend returns: ApiResponse<ReadingText[]>
   * Service receives: ReadingText[] (auto-unwrapped)
   */
  getRecommendedReadingTexts(limit?: number): Observable<ReadingText[]> {
    let params = new HttpParams();
    if (limit) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<ReadingText[]>(`${this.API_URL}/recommended-texts`, { params });
  }

  /**
   * Get recommended exercises based on student profile
   * Backend returns: ApiResponse<Exercise[]>
   * Service receives: Exercise[] (auto-unwrapped)
   */
  getRecommendedExercises(limit?: number): Observable<Exercise[]> {
    let params = new HttpParams();
    if (limit) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<Exercise[]>(`${this.API_URL}/recommended-exercises`, { params });
  }

  /**
   * Check if student is eligible to level up
   * Backend returns: ApiResponse<LevelUpResult>
   * Service receives: LevelUpResult (auto-unwrapped)
   */
  checkLevelUpEligibility(autoLevelUp: boolean = false): Observable<LevelUpResult> {
    let params = new HttpParams();
    if (autoLevelUp) {
      params = params.set('autoLevelUp', 'true');
    }
    return this.http.post<LevelUpResult>(`${this.API_URL}/check-level-up`, {}, { params });
  }

  /**
   * Get student's reading baseline from history
   * Backend returns: ApiResponse<ReadingBaselineDto>
   * Service receives: ReadingBaselineDto (auto-unwrapped)
   */
  getReadingBaseline(studentId: string, sessionCount: number = 10): Observable<import('../models/reading-baseline.model').ReadingBaselineDto> {
    let params = new HttpParams();
    params = params.set('sessionCount', sessionCount.toString());

    return this.http.get<import('../models/reading-baseline.model').ReadingBaselineDto>(
      `${this.API_URL}/${studentId}/reading-baseline`,
      { params }
    );
  }

  importStudents(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.API_URL}/import`, formData);
  }

  getImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.API_URL}/import/template`, { responseType: 'blob' });
  }

  linkStudent(email: string, teacherId: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/link`, { studentEmail: email, teacherId });
  }
}
